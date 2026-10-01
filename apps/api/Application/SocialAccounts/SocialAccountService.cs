using Api.Application.AuditLogs;
using Api.Application.Authentication;
using Api.Application.Security;
using Api.Configuration;
using Api.Contracts.Social;
using Api.Domain.Entities;
using Api.Domain.Enums;
using Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Application.SocialAccounts;

public sealed class SocialAccountService : ISocialAccountService
{
    private readonly AppDbContext _dbContext;
    private readonly CurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;
    private readonly ISocialPlatformProviderResolver _providerResolver;
    private readonly ISocialOAuthStateStore _stateStore;
    private readonly ISocialCredentialProtector _protector;
    private readonly ILogger<SocialAccountService> _logger;
    private readonly IOptions<SocialPlatformOptions> _socialOptions;

    public SocialAccountService(
        AppDbContext dbContext,
        CurrentUserService currentUser,
        IAuditLogService auditLog,
        ISocialPlatformProviderResolver providerResolver,
        ISocialOAuthStateStore stateStore,
        ISocialCredentialProtector protector,
        ILogger<SocialAccountService> logger,
        IOptions<SocialPlatformOptions> socialOptions)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _auditLog = auditLog;
        _providerResolver = providerResolver;
        _stateStore = stateStore;
        _protector = protector;
        _logger = logger;
        _socialOptions = socialOptions;
    }

    public async Task<IReadOnlyList<SocialAccountResponse>> GetAsync(
        CancellationToken cancellationToken)
    {
        var workspaceId = _currentUser.WorkspaceId;

        var accounts = await _dbContext.SocialAccounts
            .AsNoTracking()
            .Where(account => account.WorkspaceId == workspaceId)
            .OrderBy(account => account.Platform)
            .ThenBy(account => account.CreatedAt)
            .ToListAsync(cancellationToken);

        return accounts.Select(Map).ToList();
    }

    public async Task<SocialConnectResponse> CreateAuthorizationAsync(
        string platform,
        CancellationToken cancellationToken)
    {
        var parsedPlatform = ParsePlatform(platform);
        var provider = GetConfiguredProvider(parsedPlatform);
        var workspaceId = _currentUser.WorkspaceId;

        var (state, codeVerifier) = SocialOAuthStateFactory.Create(parsedPlatform);

        await _stateStore.StoreAsync(
            state,
            new SocialOAuthState(workspaceId, parsedPlatform, codeVerifier),
            cancellationToken);

        var codeChallenge = codeVerifier is null
            ? null
            : SocialPkce.CreateCodeChallenge(codeVerifier);

        var authorizationUri = provider.BuildAuthorizationUri(state, codeChallenge);

        _logger.LogInformation(
            "Social authorization started for {Platform} in workspace {WorkspaceId}",
            parsedPlatform,
            workspaceId);

        return new SocialConnectResponse
        {
            Platform = parsedPlatform.ToString(),
            AuthorizationUrl = authorizationUri.ToString(),
            ExpiresAt = DateTime.UtcNow.AddMinutes(_socialOptions.Value.StateLifetimeMinutes)
        };
    }

    public async Task<SocialAccountResponse?> CompleteAsync(
        string platform,
        string code,
        string state,
        CancellationToken cancellationToken)
    {
        var parsedPlatform = ParsePlatform(platform);

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new InvalidSocialOAuthStateException(
                "The OAuth callback did not include an authorization code.");
        }

        var pendingState = await _stateStore.ConsumeAsync(state, cancellationToken);
        if (pendingState is null || pendingState.Platform != parsedPlatform)
        {
            throw new InvalidSocialOAuthStateException(
                "The OAuth state is unknown, expired, or already used.");
        }

        var provider = GetConfiguredProvider(parsedPlatform);

        var tokens = await provider.ExchangeAuthorizationCodeAsync(
            code,
            pendingState.CodeVerifier,
            cancellationToken);
        var profile = await provider.GetProfileAsync(tokens.AccessToken, cancellationToken);

        var encryptedAccessToken = _protector.ProtectAccessToken(tokens.AccessToken);
        var encryptedRefreshToken = tokens.RefreshToken is null
            ? null
            : _protector.ProtectRefreshToken(tokens.RefreshToken);

        var now = DateTime.UtcNow;
        var account = await _dbContext.SocialAccounts.SingleOrDefaultAsync(
            existing =>
                existing.WorkspaceId == pendingState.WorkspaceId &&
                existing.Platform == parsedPlatform &&
                existing.PlatformAccountId == profile.PlatformAccountId,
            cancellationToken);

        if (account is null)
        {
            account = new SocialAccount
            {
                Id = Guid.NewGuid(),
                WorkspaceId = pendingState.WorkspaceId,
                Platform = parsedPlatform,
                PlatformAccountId = profile.PlatformAccountId,
                CreatedAt = now
            };
            _dbContext.SocialAccounts.Add(account);
        }

        account.Username = profile.Username;
        account.DisplayName = profile.DisplayName;
        account.ProfileUrl = profile.ProfileUrl;
        account.AccessTokenEncrypted = encryptedAccessToken;
        account.RefreshTokenEncrypted = encryptedRefreshToken;
        account.TokenExpiresAt = tokens.ExpiresAt;
        account.Scopes = tokens.Scopes.ToList();
        account.Status = SocialAccountStatus.Connected;
        account.ConnectedAt = now;
        account.UpdatedAt = now;

        // Queued, not saved: written by the same SaveChanges as the change.
        // The workspace comes from the signed state, not the caller: this
        // endpoint is anonymous, so there is no token and no user to attribute
        // the connect to. Reconnecting an existing account is recorded as a
        // connect too, which is what happened. No token or credential value is
        // ever recorded.
        await _auditLog.RecordAsync(
            pendingState.WorkspaceId,
            userId: null,
            AuditAction.AccountConnected,
            "SocialAccount",
            account.Id,
            oldValues: null,
            newValues: new { status = SocialAccountStatus.Connected.ToString(), platform = parsedPlatform.ToString() },
            cancellationToken: cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Social account {SocialAccountId} connected for {Platform} in workspace {WorkspaceId}",
            account.Id,
            account.Platform,
            account.WorkspaceId);

        return Map(account);
    }

    public async Task<SocialAccountResponse?> DisconnectAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var workspaceId = _currentUser.WorkspaceId;

        var account = await _dbContext.SocialAccounts.SingleOrDefaultAsync(
            existing => existing.Id == id && existing.WorkspaceId == workspaceId,
            cancellationToken);

        if (account is null)
        {
            return null;
        }

        if (!SocialAccountStatusTransitions.CanDisconnect(account.Status))
        {
            throw new InvalidSocialAccountTransitionException(
                $"A {account.Status} social account cannot be disconnected.");
        }

        account.Status = SocialAccountStatus.Disconnected;

        // Disconnecting must destroy the stored credentials, not just hide them.
        account.AccessTokenEncrypted = null;
        account.RefreshTokenEncrypted = null;
        account.TokenExpiresAt = null;
        account.Scopes = [];
        account.UpdatedAt = DateTime.UtcNow;

        // Queued, not saved: written by the same SaveChanges as the change.
        // Only the platform and status are recorded. Credentials are never put
        // in an audit row, encrypted or otherwise.
        await _auditLog.RecordAsync(
            AuditAction.AccountDisconnected,
            "SocialAccount",
            account.Id,
            new { status = SocialAccountStatus.Connected.ToString() },
            new { status = SocialAccountStatus.Disconnected.ToString(), platform = account.Platform.ToString() },
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Social account {SocialAccountId} disconnected for {Platform} in workspace {WorkspaceId}",
            account.Id,
            account.Platform,
            account.WorkspaceId);

        return Map(account);
    }

    private static SocialAccountResponse Map(SocialAccount account) => new()
    {
        Id = account.Id,
        Platform = account.Platform.ToString(),
        Username = account.Username,
        DisplayName = account.DisplayName,
        ProfileUrl = account.ProfileUrl,
        Status = account.Status.ToString(),
        TokenExpiresAt = account.TokenExpiresAt,
        ConnectedAt = account.ConnectedAt,
        UpdatedAt = account.UpdatedAt
    };

    private static ContentPlatform ParsePlatform(string platform)
    {
        if (!SocialPlatformParser.TryParse(platform, out var parsed))
        {
            throw new UnsupportedSocialPlatformException(
                $"'{platform}' is not a supported social platform. Supported platforms are LinkedIn and X.");
        }

        return parsed;
    }

    private ISocialPlatformProvider GetConfiguredProvider(ContentPlatform platform)
    {
        var provider = _providerResolver.Get(platform);
        if (!provider.IsConfigured)
        {
            throw new SocialPlatformNotConfiguredException(
                $"The {platform} social platform is not configured on this deployment.");
        }

        return provider;
    }
}
