using System.Security.Claims;
using System.Text.Json;
using Api.Application.Authentication;
using Api.Application.Security;
using Api.Application.SocialAccounts;
using Api.Configuration;
using Api.Contracts.Social;
using Api.Domain.Entities;
using Api.Domain.Enums;
using Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Api.Tests;

public sealed class SocialAccountServiceTests
{
    private const string AccessToken = "secret-access-token-value";
    private const string RefreshToken = "secret-refresh-token-value";
    private const string PlatformAccountId = "platform-user-42";

    [Fact]
    public async Task A_successful_callback_connects_the_account()
    {
        var workspaceId = Guid.NewGuid();
        using var harness = new Harness(workspaceId);
        var state = await harness.BeginAsync("linkedin");

        var result = await harness.Service.CompleteAsync(
            "linkedin",
            "auth-code",
            state,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("LinkedIn", result!.Platform);
        Assert.Equal("Connected", result.Status);
        Assert.Equal("mayorx", result.Username);
        Assert.Equal("Mayorx Tech", result.DisplayName);

        var stored = harness.DbContext.SocialAccounts.AsNoTracking().Single();
        Assert.Equal(workspaceId, stored.WorkspaceId);
        Assert.Equal(ContentPlatform.LinkedIn, stored.Platform);
        Assert.Equal(PlatformAccountId, stored.PlatformAccountId);
        Assert.Equal(SocialAccountStatus.Connected, stored.Status);
    }

    /// <summary>
    /// The full encrypt, persist, retrieve, decrypt path.
    /// </summary>
    [Fact]
    public async Task An_access_token_is_encrypted_at_rest_and_can_be_decrypted_afterwards()
    {
        using var harness = new Harness(Guid.NewGuid());
        var state = await harness.BeginAsync("linkedin");

        await harness.Service.CompleteAsync("linkedin", "auth-code", state, CancellationToken.None);

        var stored = harness.DbContext.SocialAccounts.AsNoTracking().Single();
        Assert.NotNull(stored.AccessTokenEncrypted);
        Assert.NotEqual(AccessToken, stored.AccessTokenEncrypted);
        Assert.Equal(AccessToken, harness.Protector.UnprotectAccessToken(stored.AccessTokenEncrypted!));
    }

    [Fact]
    public async Task A_refresh_token_is_encrypted_at_rest_and_can_be_decrypted_afterwards()
    {
        using var harness = new Harness(Guid.NewGuid());
        var state = await harness.BeginAsync("x");

        await harness.Service.CompleteAsync("x", "auth-code", state, CancellationToken.None);

        var stored = harness.DbContext.SocialAccounts.AsNoTracking().Single();
        Assert.NotNull(stored.RefreshTokenEncrypted);
        Assert.NotEqual(RefreshToken, stored.RefreshTokenEncrypted);
        Assert.Equal(RefreshToken, harness.Protector.UnprotectRefreshToken(stored.RefreshTokenEncrypted!));
    }

    /// <summary>
    /// No column on the stored row may hold a usable credential in the clear.
    /// </summary>
    [Fact]
    public async Task The_database_never_stores_a_plaintext_token()
    {
        using var harness = new Harness(Guid.NewGuid());
        var state = await harness.BeginAsync("x");

        await harness.Service.CompleteAsync("x", "auth-code", state, CancellationToken.None);

        var stored = harness.DbContext.SocialAccounts.AsNoTracking().Single();
        var values = new[]
        {
            stored.AccessTokenEncrypted,
            stored.RefreshTokenEncrypted,
            stored.Username,
            stored.DisplayName,
            stored.ProfileUrl,
            stored.PlatformAccountId
        };

        Assert.All(
            values,
            value => Assert.DoesNotContain(AccessToken, value ?? string.Empty, StringComparison.Ordinal));
        Assert.All(
            values,
            value => Assert.DoesNotContain(RefreshToken, value ?? string.Empty, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Stored_scopes_and_expiry_come_from_the_token_response()
    {
        using var harness = new Harness(Guid.NewGuid());
        var state = await harness.BeginAsync("x");

        await harness.Service.CompleteAsync("x", "auth-code", state, CancellationToken.None);

        var stored = harness.DbContext.SocialAccounts.AsNoTracking().Single();
        Assert.Equal(["tweet.read", "offline.access"], stored.Scopes);
        Assert.NotNull(stored.TokenExpiresAt);
    }

    [Fact]
    public async Task An_x_connection_uses_the_pkce_verifier_stored_with_its_state()
    {
        using var harness = new Harness(Guid.NewGuid());
        var state = await harness.BeginAsync("x");

        await harness.Service.CompleteAsync("x", "auth-code", state, CancellationToken.None);

        var storedVerifier = harness.StateStore.LastStored!.CodeVerifier;
        Assert.False(string.IsNullOrWhiteSpace(storedVerifier));
        Assert.Equal(storedVerifier, harness.XProvider.UsedCodeVerifier);
    }

    [Fact]
    public async Task A_linkedin_connection_does_not_use_pkce()
    {
        using var harness = new Harness(Guid.NewGuid());
        var state = await harness.BeginAsync("linkedin");

        await harness.Service.CompleteAsync("linkedin", "auth-code", state, CancellationToken.None);

        Assert.Null(harness.LinkedInProvider.UsedCodeVerifier);
    }

    [Fact]
    public async Task Listing_accounts_returns_only_the_calling_workspaces_connections()
    {
        var workspaceId = Guid.NewGuid();
        using var harness = new Harness(workspaceId);
        SeedAccount(harness.DbContext, workspaceId, PlatformAccountId, username: "own-account");
        SeedAccount(harness.DbContext, Guid.NewGuid(), PlatformAccountId, username: "other-account");

        var accounts = await harness.Service.GetAsync(CancellationToken.None);

        var account = Assert.Single(accounts);
        Assert.Equal("own-account", account.Username);
    }

    [Fact]
    public async Task An_account_from_another_workspace_cannot_be_disconnected()
    {
        var workspaceId = Guid.NewGuid();
        using var harness = new Harness(workspaceId);
        var foreign = SeedAccount(harness.DbContext, Guid.NewGuid(), PlatformAccountId);

        var result = await harness.Service.DisconnectAsync(foreign.Id, CancellationToken.None);

        Assert.Null(result);
        var unchanged = harness.DbContext.SocialAccounts
            .AsNoTracking()
            .Single(account => account.Id == foreign.Id);
        Assert.Equal(SocialAccountStatus.Connected, unchanged.Status);
        Assert.NotNull(unchanged.AccessTokenEncrypted);
    }

    [Fact]
    public async Task Disconnecting_marks_the_account_disconnected_and_destroys_its_credentials()
    {
        using var harness = new Harness(Guid.NewGuid());
        var state = await harness.BeginAsync("x");
        var connected = await harness.Service.CompleteAsync(
            "x",
            "auth-code",
            state,
            CancellationToken.None);
        Assert.NotNull(
            harness.DbContext.SocialAccounts.AsNoTracking().Single().AccessTokenEncrypted);

        var result = await harness.Service.DisconnectAsync(connected!.Id, CancellationToken.None);

        Assert.Equal("Disconnected", result!.Status);
        var stored = harness.DbContext.SocialAccounts.AsNoTracking().Single();
        Assert.Null(stored.AccessTokenEncrypted);
        Assert.Null(stored.RefreshTokenEncrypted);
        Assert.Null(stored.TokenExpiresAt);
        Assert.Empty(stored.Scopes);
    }

    [Fact]
    public async Task Disconnecting_an_unknown_account_returns_nothing()
    {
        using var harness = new Harness(Guid.NewGuid());

        Assert.Null(await harness.Service.DisconnectAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task An_already_disconnected_account_cannot_be_disconnected_again()
    {
        var workspaceId = Guid.NewGuid();
        using var harness = new Harness(workspaceId);
        var account = SeedAccount(
            harness.DbContext,
            workspaceId,
            PlatformAccountId,
            SocialAccountStatus.Disconnected);

        var exception = await Assert.ThrowsAsync<InvalidSocialAccountTransitionException>(
            () => harness.Service.DisconnectAsync(account.Id, CancellationToken.None));

        Assert.Contains("Disconnected", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_revoked_account_cannot_be_disconnected()
    {
        var workspaceId = Guid.NewGuid();
        using var harness = new Harness(workspaceId);
        var account = SeedAccount(
            harness.DbContext,
            workspaceId,
            PlatformAccountId,
            SocialAccountStatus.Revoked);

        await Assert.ThrowsAsync<InvalidSocialAccountTransitionException>(
            () => harness.Service.DisconnectAsync(account.Id, CancellationToken.None));
    }

    [Theory]
    [InlineData(SocialAccountStatus.Connected)]
    [InlineData(SocialAccountStatus.Expired)]
    [InlineData(SocialAccountStatus.Error)]
    public async Task A_live_or_recoverable_account_can_be_disconnected(
        SocialAccountStatus status)
    {
        var workspaceId = Guid.NewGuid();
        using var harness = new Harness(workspaceId);
        var account = SeedAccount(harness.DbContext, workspaceId, PlatformAccountId, status);

        var result = await harness.Service.DisconnectAsync(account.Id, CancellationToken.None);

        Assert.Equal("Disconnected", result!.Status);
    }

    [Fact]
    public async Task Re_authorising_updates_the_existing_account_instead_of_duplicating_it()
    {
        using var harness = new Harness(Guid.NewGuid());
        var first = await harness.BeginAsync("linkedin");
        var original = await harness.Service.CompleteAsync(
            "linkedin",
            "auth-code",
            first,
            CancellationToken.None);

        var second = await harness.BeginAsync("linkedin");
        var reconnected = await harness.Service.CompleteAsync(
            "linkedin",
            "auth-code",
            second,
            CancellationToken.None);

        Assert.Equal(original!.Id, reconnected!.Id);
        Assert.Single(harness.DbContext.SocialAccounts);
    }

    [Fact]
    public async Task A_disconnected_account_can_be_connected_again()
    {
        var workspaceId = Guid.NewGuid();
        using var harness = new Harness(workspaceId);
        SeedAccount(harness.DbContext, workspaceId, PlatformAccountId, SocialAccountStatus.Disconnected);
        var state = await harness.BeginAsync("linkedin");

        var result = await harness.Service.CompleteAsync(
            "linkedin",
            "auth-code",
            state,
            CancellationToken.None);

        Assert.Equal("Connected", result!.Status);
        Assert.Single(harness.DbContext.SocialAccounts);
    }

    [Fact]
    public async Task The_same_platform_account_can_be_connected_by_two_workspaces()
    {
        var firstWorkspaceId = Guid.NewGuid();
        var secondWorkspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var stateStore = new FakeSocialOAuthStateStore();
        var provider = new FakeSocialPlatformProvider { Platform = ContentPlatform.LinkedIn };
        var accessor = new StubHttpContextAccessor { HttpContext = CreateHttpContext(firstWorkspaceId) };
        var service = new SocialAccountService(
            dbContext,
            new CurrentUserService(accessor),
            AuditLogTestContext.Create(dbContext, accessor),
            new SocialPlatformProviderResolver([provider]),
            stateStore,
            CreateProtector(),
            new CapturingLogger<SocialAccountService>(),
            Options.Create(new SocialPlatformOptions()));

        await service.CreateAuthorizationAsync("linkedin", CancellationToken.None);
        var firstState = stateStore.LastKey!;

        accessor.HttpContext = CreateHttpContext(secondWorkspaceId);

        await service.CompleteAsync("linkedin", "code", firstState, CancellationToken.None);

        await service.CreateAuthorizationAsync("linkedin", CancellationToken.None);
        var secondState = stateStore.LastKey!;

        await service.CompleteAsync("linkedin", "code", secondState, CancellationToken.None);

        var rows = dbContext.SocialAccounts.AsNoTracking().ToList();
        Assert.Equal(2, rows.Count);
        Assert.All(rows, row => Assert.Equal("platform-user-42", row.PlatformAccountId));

        var workspaceIds = rows.Select(row => row.WorkspaceId).ToHashSet();
        Assert.Equal(2, workspaceIds.Count);
        Assert.Contains(firstWorkspaceId, workspaceIds);
        Assert.Contains(secondWorkspaceId, workspaceIds);
    }

    [Theory]
    [InlineData("facebook")]
    [InlineData("instagram")]
    [InlineData("tiktok")]
    [InlineData("")]
    public async Task An_unsupported_platform_cannot_start_a_connection(string platform)
    {
        using var harness = new Harness(Guid.NewGuid());

        await Assert.ThrowsAsync<UnsupportedSocialPlatformException>(
            () => harness.Service.CreateAuthorizationAsync(platform, CancellationToken.None));
    }

    [Fact]
    public async Task An_unsupported_platform_cannot_complete_a_callback()
    {
        using var harness = new Harness(Guid.NewGuid());

        await Assert.ThrowsAsync<UnsupportedSocialPlatformException>(
            () => harness.Service.CompleteAsync("facebook", "code", "state", CancellationToken.None));
    }

    [Fact]
    public async Task An_unconfigured_platform_cannot_start_a_connection()
    {
        using var harness = new Harness(Guid.NewGuid(), isConfigured: false);

        await Assert.ThrowsAsync<SocialPlatformNotConfiguredException>(
            () => harness.Service.CreateAuthorizationAsync("linkedin", CancellationToken.None));
    }

    [Fact]
    public async Task An_unconfigured_platform_cannot_complete_a_callback()
    {
        using var harness = new Harness(Guid.NewGuid(), isConfigured: false);
        var state = await harness.SeedStateAsync("linkedin");

        await Assert.ThrowsAsync<SocialPlatformNotConfiguredException>(
            () => harness.Service.CompleteAsync("linkedin", "auth-code", state, CancellationToken.None));
    }

    [Fact]
    public async Task An_unknown_state_is_rejected()
    {
        using var harness = new Harness(Guid.NewGuid());

        await Assert.ThrowsAsync<InvalidSocialOAuthStateException>(
            () => harness.Service.CompleteAsync(
                "linkedin",
                "auth-code",
                "never-issued",
                CancellationToken.None));
    }

    /// <summary>
    /// State is single use, so a replayed callback must fail.
    /// </summary>
    [Fact]
    public async Task A_state_can_only_be_used_once()
    {
        using var harness = new Harness(Guid.NewGuid());
        var state = await harness.BeginAsync("linkedin");
        await harness.Service.CompleteAsync("linkedin", "auth-code", state, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidSocialOAuthStateException>(
            () => harness.Service.CompleteAsync("linkedin", "auth-code", state, CancellationToken.None));
    }

    [Fact]
    public async Task A_state_issued_for_one_platform_cannot_be_used_for_another()
    {
        using var harness = new Harness(Guid.NewGuid());
        var state = await harness.BeginAsync("linkedin");

        await Assert.ThrowsAsync<InvalidSocialOAuthStateException>(
            () => harness.Service.CompleteAsync("x", "auth-code", state, CancellationToken.None));
    }

    [Fact]
    public async Task A_callback_without_an_authorization_code_is_rejected()
    {
        using var harness = new Harness(Guid.NewGuid());
        var state = await harness.BeginAsync("linkedin");

        await Assert.ThrowsAsync<InvalidSocialOAuthStateException>(
            () => harness.Service.CompleteAsync("linkedin", string.Empty, state, CancellationToken.None));
    }

    /// <summary>
    /// The OAuth callback is anonymous, so the workspace must come from the
    /// stored state rather than the caller. The harness gives the service an
    /// authenticated context for a different workspace, so trusting the caller
    /// would attach the connection to the wrong workspace.
    /// </summary>
    [Fact]
    public async Task A_callback_binds_the_connection_to_the_state_workspace_not_the_caller()
    {
        var stateWorkspaceId = Guid.NewGuid();
        var callerWorkspaceId = Guid.NewGuid();
        using var harness = new Harness(callerWorkspaceId, stateWorkspaceId);
        var state = await harness.SeedStateAsync("linkedin");

        var result = await harness.Service.CompleteAsync(
            "linkedin",
            "auth-code",
            state,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(
            stateWorkspaceId,
            harness.DbContext.SocialAccounts.AsNoTracking().Single().WorkspaceId);
        Assert.NotEqual(
            callerWorkspaceId,
            harness.DbContext.SocialAccounts.AsNoTracking().Single().WorkspaceId);
    }

    [Fact]
    public async Task A_returned_account_never_contains_a_token()
    {
        using var harness = new Harness(Guid.NewGuid());
        var state = await harness.BeginAsync("x");

        var result = await harness.Service.CompleteAsync(
            "x",
            "auth-code",
            state,
            CancellationToken.None);
        var json = JsonSerializer.Serialize(result);

        Assert.DoesNotContain(AccessToken, json, StringComparison.Ordinal);
        Assert.DoesNotContain(RefreshToken, json, StringComparison.Ordinal);
        Assert.DoesNotContain("access_token", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("refresh_token", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AccessTokenEncrypted", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RefreshTokenEncrypted", json, StringComparison.OrdinalIgnoreCase);

        var propertyNames = typeof(SocialAccountResponse)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();
        Assert.DoesNotContain(
            propertyNames,
            name => name.Contains("AccessToken", StringComparison.OrdinalIgnoreCase)
                || name.Contains("RefreshToken", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("TokenExpiresAt", propertyNames);
    }

    [Fact]
    public async Task Connecting_never_writes_a_token_to_the_log()
    {
        using var harness = new Harness(Guid.NewGuid());
        var state = await harness.BeginAsync("x");

        await harness.Service.CompleteAsync("x", "auth-code", state, CancellationToken.None);

        Assert.NotEmpty(harness.Logger.Messages);
        Assert.All(harness.Logger.Messages, message =>
        {
            Assert.DoesNotContain(AccessToken, message, StringComparison.Ordinal);
            Assert.DoesNotContain(RefreshToken, message, StringComparison.Ordinal);
            Assert.DoesNotContain("auth-code", message, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Disconnecting_never_writes_a_token_to_the_log()
    {
        using var harness = new Harness(Guid.NewGuid());
        var state = await harness.BeginAsync("x");
        var account = await harness.Service.CompleteAsync(
            "x",
            "auth-code",
            state,
            CancellationToken.None);

        await harness.Service.DisconnectAsync(account!.Id, CancellationToken.None);

        Assert.All(
            harness.Logger.Messages,
            message => Assert.DoesNotContain(AccessToken, message, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Starting_a_connection_returns_an_authorization_url_for_the_platform()
    {
        using var harness = new Harness(Guid.NewGuid());

        var connect = await harness.Service.CreateAuthorizationAsync(
            "linkedin",
            CancellationToken.None);

        Assert.Equal("LinkedIn", connect.Platform);
        Assert.Contains(
            "provider.example.com",
            connect.AuthorizationUrl,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            harness.StateStore.LastKey!,
            connect.AuthorizationUrl,
            StringComparison.Ordinal);
        Assert.True(connect.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task Starting_a_connection_binds_the_state_to_the_calling_workspace()
    {
        var workspaceId = Guid.NewGuid();
        using var harness = new Harness(workspaceId);

        await harness.Service.CreateAuthorizationAsync("linkedin", CancellationToken.None);

        Assert.Equal(workspaceId, harness.StateStore.LastStored!.WorkspaceId);
    }

    [Fact]
    public async Task The_authorization_url_never_contains_a_client_secret()
    {
        using var harness = new Harness(Guid.NewGuid());

        var connect = await harness.Service.CreateAuthorizationAsync("x", CancellationToken.None);

        Assert.DoesNotContain("test-client-secret", connect.AuthorizationUrl, StringComparison.Ordinal);
    }

    private static AppDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"social-{Guid.NewGuid()}")
            .Options);

    private static ISocialCredentialProtector CreateProtector() =>
        new DataProtectionSocialCredentialProtector(
            DataProtectionProvider.Create(
                new DirectoryInfo(Path.Combine(Path.GetTempPath(), "mayorx-social-tests")),
                builder => builder.SetApplicationName("mayorx-social-service-tests")));

    private static DefaultHttpContext CreateHttpContext(Guid workspaceId) =>
        new()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim("workspace_id", workspaceId.ToString())
            ], "test"))
        };

    /// <summary>
    /// <see cref="HttpContextAccessor"/> stores the current context in a static
    /// ambient slot, so two accessors in the same flow observe the same value
    /// and parallel test classes can clobber it. This stub keeps the context in
    /// instance state so each identity stays independent and deterministic.
    /// </summary>
    private sealed class StubHttpContextAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }

    private static CurrentUserService CreateCurrentUser(Guid workspaceId) =>
        new(new StubHttpContextAccessor { HttpContext = CreateHttpContext(workspaceId) });

    private static SocialAccount SeedAccount(
        AppDbContext dbContext,
        Guid workspaceId,
        string platformAccountId,
        SocialAccountStatus status = SocialAccountStatus.Connected,
        string? username = null)
    {
        var account = new SocialAccount
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Platform = ContentPlatform.LinkedIn,
            PlatformAccountId = platformAccountId,
            Username = username,
            DisplayName = username,
            AccessTokenEncrypted = "encrypted-access",
            RefreshTokenEncrypted = "encrypted-refresh",
            Scopes = ["openid"],
            Status = status,
            ConnectedAt = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };
        dbContext.SocialAccounts.Add(account);
        dbContext.SaveChanges();

        return account;
    }

    private sealed class Harness : IDisposable
    {
        private readonly Guid _stateWorkspaceId;

        public Harness(
            Guid callerWorkspaceId,
            Guid? stateWorkspaceId = null,
            bool isConfigured = true)
        {
            _stateWorkspaceId = stateWorkspaceId ?? callerWorkspaceId;
            DbContext = CreateDbContext();
            LinkedInProvider = new FakeSocialPlatformProvider
            {
                Platform = ContentPlatform.LinkedIn,
                IsConfigured = isConfigured
            };
            XProvider = new FakeSocialPlatformProvider
            {
                Platform = ContentPlatform.X,
                IsConfigured = isConfigured
            };
            StateStore = new FakeSocialOAuthStateStore();
            Logger = new CapturingLogger<SocialAccountService>();
            Service = new SocialAccountService(
                DbContext,
                CreateCurrentUser(callerWorkspaceId),
                AuditLogTestContext.Create(DbContext, callerWorkspaceId),
                new SocialPlatformProviderResolver([LinkedInProvider, XProvider]),
                StateStore,
                Protector,
                Logger,
                Options.Create(new SocialPlatformOptions()));
        }

        public AppDbContext DbContext { get; }

        public FakeSocialPlatformProvider LinkedInProvider { get; }

        public FakeSocialPlatformProvider XProvider { get; }

        public FakeSocialOAuthStateStore StateStore { get; }

        public CapturingLogger<SocialAccountService> Logger { get; }

        public ISocialCredentialProtector Protector { get; } = CreateProtector();

        public SocialAccountService Service { get; }

        /// <summary>
        /// Runs the real authorization step so the state under test is one the
        /// service itself produced.
        /// </summary>
        public async Task<string> BeginAsync(string platform)
        {
            await Service.CreateAuthorizationAsync(platform, CancellationToken.None);
            Assert.NotNull(StateStore.LastKey);

            return StateStore.LastKey!;
        }

        public Task<string> SeedStateAsync(string platform)
        {
            StateStore.Seed(
                Guid.NewGuid().ToString("N"),
                _stateWorkspaceId,
                platform == "x" ? ContentPlatform.X : ContentPlatform.LinkedIn,
                platform == "x" ? "pkce-verifier" : null);

            return Task.FromResult(StateStore.LastKey!);
        }

        public void Dispose() => DbContext.Dispose();
    }

    private sealed class FakeSocialPlatformProvider : ISocialPlatformProvider
    {
        public ContentPlatform Platform { get; set; } = ContentPlatform.LinkedIn;

        public bool IsConfigured { get; set; } = true;

        public string? UsedCodeVerifier { get; private set; }

        public Uri BuildAuthorizationUri(string state, string? codeChallenge) =>
            new($"https://provider.example.com/authorize?state={state}");

        public Task<SocialTokenResponse> ExchangeAuthorizationCodeAsync(
            string code,
            string? codeVerifier,
            CancellationToken cancellationToken)
        {
            UsedCodeVerifier = codeVerifier;

            return Task.FromResult(new SocialTokenResponse(
                AccessToken,
                RefreshToken,
                DateTime.UtcNow.AddHours(1),
                Platform == ContentPlatform.X
                    ? ["tweet.read", "offline.access"]
                    : ["openid", "profile"]));
        }

        public Task<SocialAccountProfile> GetProfileAsync(
            string accessToken,
            CancellationToken cancellationToken) =>
            Task.FromResult(new SocialAccountProfile(
                PlatformAccountId,
                "mayorx",
                "Mayorx Tech",
                "https://provider.example.com/mayorx"));
    }

    private sealed class FakeSocialOAuthStateStore : ISocialOAuthStateStore
    {
        private readonly Dictionary<string, SocialOAuthState> _states = [];

        public SocialOAuthState? LastStored { get; private set; }

        public string? LastKey { get; private set; }

        public Task StoreAsync(
            string state,
            SocialOAuthState value,
            CancellationToken cancellationToken)
        {
            _states[state] = value;
            LastStored = value;
            LastKey = state;

            return Task.CompletedTask;
        }

        public Task<SocialOAuthState?> ConsumeAsync(
            string state,
            CancellationToken cancellationToken)
        {
            if (!_states.Remove(state, out var value))
            {
                return Task.FromResult<SocialOAuthState?>(null);
            }

            return Task.FromResult<SocialOAuthState?>(value);
        }

        public void Seed(
            string state,
            Guid workspaceId,
            ContentPlatform platform,
            string? codeVerifier) =>
            StoreAsync(
                state,
                new SocialOAuthState(workspaceId, platform, codeVerifier),
                CancellationToken.None)
                .GetAwaiter()
                .GetResult();
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }
}
