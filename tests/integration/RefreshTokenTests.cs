using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Contracts.Auth;
using Api.Contracts.Responses;
using Api.Domain.Entities;
using Api.Domain.Enums;
using Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.IntegrationTests;

/// <summary>
/// Refresh token lifecycle against a real PostgreSQL: rotation, reuse
/// detection, descendant revocation, expiry, logout, and ownership.
/// </summary>
/// <remarks>
/// Every case here goes through the real HTTP surface and the real database,
/// because the properties under test are database properties. An in-memory
/// provider has no transactions, no row locks, and no unique indexes, so it
/// would pass a rotation that cannot actually be made atomic, and would pass a
/// concurrent double-submit that a real database rejects.
/// </remarks>
public sealed class RefreshTokenTests : IAsyncLifetime
{
    private readonly MayorxApiFactory _factory = MayorxApiFactory.WithSettings(
        new Dictionary<string, string?>
        {
            // These tests are about tokens, not throttling, and they make far
            // more than twenty authentication calls. Throttling gets its own
            // class with its own limit and its own Redis keys.
            ["RateLimiting:AuthenticationPermitLimit"] = "0"
        });

    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _factory.Initialise();
        await _factory.MigrateAsync();

        _client = _factory.CreateClient();
    }

public Task DisposeAsync()
    {
        _client?.Dispose();
        _factory.Dispose();

        return Task.CompletedTask;
    }

    // ---------------------------------------------------------------- issuing

    [Fact]
    public async Task Register_issues_an_access_token_and_a_refresh_token()
    {
        var session = await TestSession.RegisterAsync(_client, "issue");

        Assert.False(string.IsNullOrWhiteSpace(session.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(session.RefreshToken));
        Assert.NotEqual(Guid.Empty, session.WorkspaceId);
        Assert.NotEqual(Guid.Empty, session.UserId);

        // The access token is a JWT that actually authenticates.
        using var authenticated = _factory.CreateAuthenticatedClient(session.AccessToken);
        using var response = await authenticated.GetAsync("api/v1/users/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_issues_an_access_token_and_a_refresh_token()
    {
        var session = await TestSession.RegisterAsync(_client, "login-issue");

        var response = await _client.PostAsJsonAsync(
            "api/v1/auth/login",
            new { email = session.Email, password = "Integration-Test-Password-42" });

        var body = await TestHttp.ReadAsync<AuthResponse>(response);

        TestHttp.EnsureSucceeded(response, body);

        Assert.False(string.IsNullOrWhiteSpace(body.RefreshToken));
        Assert.Equal(session.WorkspaceId, body.WorkspaceId);
        Assert.Equal(session.UserId, body.User.Id);
    }

    [Fact]
    public async Task Refresh_token_is_stored_as_a_hash_and_never_as_the_issued_value()
    {
        var session = await TestSession.RegisterAsync(_client, "storage");

        var stored = await LoadTokensAsync(session.UserId);

        var row = Assert.Single(stored);

        Assert.Equal(
            Api.Application.Authentication.RefreshTokenService.HashToken(session.RefreshToken),
            row.TokenHash);

        // The issued value must appear nowhere in the row.
        Assert.DoesNotContain(session.RefreshToken, row.TokenHash, StringComparison.Ordinal);
        Assert.DoesNotContain(session.RefreshToken, row.Status.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(session.RefreshToken, row.RevocationReason ?? string.Empty, StringComparison.Ordinal);

        // And nothing in the whole table reproduces it.
        Assert.All(
            stored,
            token => Assert.DoesNotContain(
                session.RefreshToken,
                token.TokenHash,
                StringComparison.Ordinal));
    }

    // -------------------------------------------------------------- rotation

    [Fact]
    public async Task Refreshing_with_a_valid_token_succeeds_and_returns_a_new_access_token()
    {
        var session = await TestSession.RegisterAsync(_client, "refresh-ok");

        using var response = await RefreshAsync(session.RefreshToken);
        var body = await TestHttp.ReadAsync<AuthResponse>(response);

        TestHttp.EnsureSucceeded(response, body);

        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));

        // The new access token is usable, so the refresh genuinely re-authenticated.
        using var authenticated = _factory.CreateAuthenticatedClient(body.AccessToken);
        using var me = await authenticated.GetAsync("api/v1/users/me");

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task Refreshing_rotates_the_refresh_token()
    {
        var session = await TestSession.RegisterAsync(_client, "rotate");

        using var response = await RefreshAsync(session.RefreshToken);
        var body = await TestHttp.ReadAsync<AuthResponse>(response);

        TestHttp.EnsureSucceeded(response, body);

        // A new value, not the one presented.
        Assert.NotEqual(session.RefreshToken, body.RefreshToken);
        Assert.False(string.IsNullOrWhiteSpace(body.RefreshToken));

        // The original row is marked consumed and linked to its successor.
        var consumed = await LoadTokenByHashAsync(
            Api.Application.Authentication.RefreshTokenService.HashToken(session.RefreshToken));

        Assert.Equal(RefreshTokenStatus.Revoked, consumed.Status);
        Assert.NotNull(consumed.ReplacedById);
        Assert.NotNull(consumed.RevokedAt);
        Assert.Equal("Rotated", consumed.RevocationReason);

        // The successor exists, is active, and belongs to the same owner.
        var successor = await LoadTokenAsync(consumed.ReplacedById!.Value);

        Assert.Equal(RefreshTokenStatus.Active, successor.Status);
        Assert.Equal(session.UserId, successor.UserId);
        Assert.Equal(session.WorkspaceId, successor.WorkspaceId);

        // Exactly one successor row was created, not two.
        Assert.Equal(2, (await LoadTokensAsync(session.UserId)).Count);
    }

    [Fact]
    public async Task The_previous_refresh_token_cannot_be_used_again()
    {
        var session = await TestSession.RegisterAsync(_client, "old-unusable");

        using var first = await RefreshAsync(session.RefreshToken);
        TestHttp.EnsureSucceeded(first);

        using var replay = await RefreshAsync(session.RefreshToken);

        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
    }

    [Fact]
    public async Task Several_rotation_generations_stay_consistent()
    {
        var session = await TestSession.RegisterAsync(_client, "generations");

        var current = session.RefreshToken;

        for (var generation = 1; generation <= 4; generation++)
        {
            using var response = await RefreshAsync(current);
            var body = await TestHttp.ReadAsync<AuthResponse>(response);

            TestHttp.EnsureSucceeded(response, body);

            Assert.NotEqual(current, body.RefreshToken);
            current = body.RefreshToken;

            // Each generation leaves exactly one active token for this user.
            var active = (await LoadTokensAsync(session.UserId))
                .Where(token => token.Status == RefreshTokenStatus.Active)
                .ToList();

            Assert.Single(active);
        }

        // Five rows: the original plus four replacements.
        var all = await LoadTokensAsync(session.UserId);

        Assert.Equal(5, all.Count);

        // The chain is fully linked from the original to the newest.
        var oldest = all.OrderBy(token => token.CreatedAt).First();

        Assert.NotNull(oldest.ReplacedById);

        var generations = await CountGenerationsAsync(oldest.ReplacedById!.Value);

        // Five rows means the original plus four replacements, so the walk from
        // the first replacement must reach four links to the newest.
        Assert.Equal(all.Count - 1, generations);
    }

    // -------------------------------------------------------- reuse detection

    [Fact]
    public async Task Replaying_a_consumed_token_is_reported_as_reuse()
    {
        var session = await TestSession.RegisterAsync(_client, "reuse-code");

        using var first = await RefreshAsync(session.RefreshToken);
        TestHttp.EnsureSucceeded(first);

        using var replay = await RefreshAsync(session.RefreshToken);

        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);

        // The distinction matters: a replay is not the same event as an
        // ordinary invalid token, because it means the token may be stolen.
        Assert.Equal(
            "REFRESH_TOKEN_REUSED",
            await ReadErrorCodeAsync(replay));

        var consumed = await LoadTokenByHashAsync(
            Api.Application.Authentication.RefreshTokenService.HashToken(session.RefreshToken));

        Assert.Equal(RefreshTokenStatus.Reused, consumed.Status);
    }

    [Fact]
    public async Task Replaying_a_consumed_token_revokes_the_session_it_spawned()
    {
        var session = await TestSession.RegisterAsync(_client, "reuse-revokes");

        using var first = await RefreshAsync(session.RefreshToken);
        var rotated = await TestHttp.ReadAsync<AuthResponse>(first);
        TestHttp.EnsureSucceeded(first, rotated);

        // The successor is a live session before the replay.
        Assert.True(await IsActiveAsync(rotated.RefreshToken));

        using var replay = await RefreshAsync(session.RefreshToken);

        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);

        // The point of reuse detection: the newer session does not survive.
        Assert.False(await IsActiveAsync(rotated.RefreshToken));

        var successor = await LoadTokenByHashAsync(
            Api.Application.Authentication.RefreshTokenService.HashToken(rotated.RefreshToken));

        Assert.Equal(RefreshTokenStatus.Revoked, successor.Status);
        Assert.Equal("RevokedByReuseDetection", successor.RevocationReason);

        // And the live token really is dead at the API, not just marked.
        using var afterRevocation = await RefreshAsync(rotated.RefreshToken);

        Assert.Equal(HttpStatusCode.Unauthorized, afterRevocation.StatusCode);
        Assert.Equal("REFRESH_TOKEN_REVOKED", await ReadErrorCodeAsync(afterRevocation));
    }

    [Fact]
    public async Task Reuse_detection_revokes_every_later_generation()
    {
        var session = await TestSession.RegisterAsync(_client, "reuse-deep");

        // Three rotations, so the stolen token sits at the back of a chain.
        var generation1 = await RotateAsync(session.RefreshToken);
        var generation2 = await RotateAsync(generation1);
        var generation3 = await RotateAsync(generation2);

        // Replay the original, three generations back.
        using var replay = await RefreshAsync(session.RefreshToken);

        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        Assert.Equal("REFRESH_TOKEN_REUSED", await ReadErrorCodeAsync(replay));

        // Every descendant is dead, not only the immediate child. Otherwise a
        // stolen token could still reach the newest session one hop further on.
        Assert.False(await IsActiveAsync(generation1));
        Assert.False(await IsActiveAsync(generation2));
        Assert.False(await IsActiveAsync(generation3));

        Assert.DoesNotContain(
            (await LoadTokensAsync(session.UserId)),
            token => token.Status == RefreshTokenStatus.Active);
    }

    [Fact]
    public async Task Reuse_detection_does_not_reach_another_users_tokens()
    {
        var first = await TestSession.RegisterAsync(_client, "reuse-a");
        var second = await TestSession.RegisterAsync(_client, "reuse-b");

        var firstRotated = await RotateAsync(first.RefreshToken);
        var secondRotated = await RotateAsync(second.RefreshToken);

        // Replaying the first user's consumed token.
        using var replay = await RefreshAsync(first.RefreshToken);

        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);

        // The other user is in a different workspace with a different chain.
        // Containment must stop at the boundary of the session that was
        // replayed rather than revoking unrelated sessions.
        Assert.True(await IsActiveAsync(secondRotated));
        Assert.False(await IsActiveAsync(firstRotated));
    }

    [Fact]
    public async Task Reusing_a_token_a_second_time_stays_a_reuse()
    {
        var session = await TestSession.RegisterAsync(_client, "reuse-twice");

        await RotateAsync(session.RefreshToken);

        using var first = await RefreshAsync(session.RefreshToken);
        using var second = await RefreshAsync(session.RefreshToken);

        Assert.Equal("REFRESH_TOKEN_REUSED", await ReadErrorCodeAsync(first));
        Assert.Equal("REFRESH_TOKEN_REUSED", await ReadErrorCodeAsync(second));
    }

    // ------------------------------------------------------------ concurrency

    [Fact]
    public async Task Concurrent_refresh_of_one_token_rotates_it_only_once()
    {
        var session = await TestSession.RegisterAsync(_client, "concurrent");

        // Two requests presenting the same token at the same moment. Exactly one
        // may win the compare-and-swap; the other must not also receive a
        // session, and must not be able to corrupt the chain.
        var attempts = await Task.WhenAll(
            RefreshAsync(session.RefreshToken),
            RefreshAsync(session.RefreshToken));

        var succeeded = attempts
            .Where(response => response.StatusCode == HttpStatusCode.OK)
            .ToList();

        Assert.Single(succeeded);

        var refused = attempts
            .Where(response => response.StatusCode == HttpStatusCode.Unauthorized)
            .ToList();

Assert.Single(refused);

        // Which refusal the loser receives depends on when it read the row, and
        // both answers are correct:
        //
        //   - It read before the winner committed, so its compare-and-swap
        //     matched nothing and it lost a race.
        //   - It read after the winner committed, so it was handed a token that
        //     rotation had already consumed, which is exactly what a stolen token
        //     looks like.
        //
        // Asserting a single code here would be asserting on thread scheduling.
        var refusalCode = await ReadErrorCodeAsync(refused[0]);

        Assert.Contains(
            refusalCode,
            new[] { "REFRESH_TOKEN_ROTATION_CONFLICT", "REFRESH_TOKEN_REUSED" });

        // One replacement exists. A second winner would have left two.
        var tokens = await LoadTokensAsync(session.UserId);

        Assert.Equal(2, tokens.Count);

        // Exactly one row is consumed and linked onward: the original.
        Assert.Single(tokens, token => token.ReplacedById is not null);

        // Never two live sessions from one token. One active row means the
        // winner's session survived; none means the loser recognised the replay
        // and revoked the descendant. Both are safe, two would not be.
        Assert.True(
            tokens.Count(token => token.Status == RefreshTokenStatus.Active) <= 1,
            "One token must never yield more than one live session.");
    }

    [Fact]
    public async Task Concurrent_logout_and_refresh_never_leave_an_active_session()
    {
        var session = await TestSession.RegisterAsync(_client, "logout-race");

        await Task.WhenAll(
            RefreshAsync(session.RefreshToken),
            LogoutAsync(session.RefreshToken));

        // Whichever order the database chose, the token must not still be usable.
        var tokens = await LoadTokensAsync(session.UserId);

        Assert.DoesNotContain(
            tokens,
            token => token.Status == RefreshTokenStatus.Active &&
                     token.TokenHash ==
                         Api.Application.Authentication.RefreshTokenService
                             .HashToken(session.RefreshToken));
    }

    // ----------------------------------------------------------- expiry

    [Fact]
    public async Task An_expired_refresh_token_is_rejected()
    {
        var session = await TestSession.RegisterAsync(_client, "expired");

        await ExpireAsync(
            Api.Application.Authentication.RefreshTokenService.HashToken(session.RefreshToken));

        using var response = await RefreshAsync(session.RefreshToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("REFRESH_TOKEN_EXPIRED", await ReadErrorCodeAsync(response));

        var stored = await LoadTokenByHashAsync(
            Api.Application.Authentication.RefreshTokenService.HashToken(session.RefreshToken));

        Assert.Equal(RefreshTokenStatus.Expired, stored.Status);
    }

    [Fact]
    public async Task A_token_expiring_in_the_past_is_rejected_without_being_written_back()
    {
        var session = await TestSession.RegisterAsync(_client, "expired-past");

        // Move the expiry into the past without changing the status, so the
        // check has to consult ExpiresAt rather than trusting the stored state.
        await SetExpiryAsync(
            Api.Application.Authentication.RefreshTokenService.HashToken(session.RefreshToken),
            DateTime.UtcNow.AddMinutes(-1));

        using var response = await RefreshAsync(session.RefreshToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("REFRESH_TOKEN_EXPIRED", await ReadErrorCodeAsync(response));
    }

    [Fact]
    public async Task Refresh_lifetime_comes_from_configuration_not_a_constant()
    {
        var session = await TestSession.RegisterAsync(_client, "expiry-config");

        var stored = await LoadTokenByHashAsync(
            Api.Application.Authentication.RefreshTokenService.HashToken(session.RefreshToken));

        var configuredDays = await ReadConfiguredRefreshDaysAsync();
        var expected = stored.CreatedAt.AddDays(configuredDays);

        // Within a second of the configured lifetime. A hardcoded seven days
        // would miss this the moment configuration said anything else.
        Assert.True(
            Math.Abs((stored.ExpiresAt - expected).TotalSeconds) < 5,
            $"Expected roughly {expected:o} from a {configuredDays} day setting, " +
            $"but the token expires at {stored.ExpiresAt:o}.");
    }

    // -------------------------------------------------------------- revocation

    [Fact]
    public async Task A_revoked_refresh_token_is_rejected()
    {
        var session = await TestSession.RegisterAsync(_client, "revoked");

        using var logout = await LogoutAsync(session.RefreshToken);
        TestHttp.EnsureSucceeded(logout);

        using var response = await RefreshAsync(session.RefreshToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("REFRESH_TOKEN_REVOKED", await ReadErrorCodeAsync(response));
    }

    // ----------------------------------------------------------------- logout

    [Fact]
    public async Task Logout_revokes_the_token_so_it_cannot_refresh()
    {
        var session = await TestSession.RegisterAsync(_client, "logout");

        Assert.True(await IsActiveAsync(session.RefreshToken));

        using var response = await LogoutAsync(session.RefreshToken);

        TestHttp.EnsureSucceeded(response);

        var stored = await LoadTokenByHashAsync(
            Api.Application.Authentication.RefreshTokenService.HashToken(session.RefreshToken));

        Assert.Equal(RefreshTokenStatus.Revoked, stored.Status);
        Assert.Equal("Logout", stored.RevocationReason);
        Assert.NotNull(stored.RevokedAt);

        // Logout is not reuse: no replacement link, so no descendant is touched.
        Assert.Null(stored.ReplacedById);

        using var afterLogout = await RefreshAsync(session.RefreshToken);

        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
    }

    [Fact]
    public async Task Logging_out_twice_is_idempotent()
    {
        var session = await TestSession.RegisterAsync(_client, "logout-twice");

        using var first = await LogoutAsync(session.RefreshToken);
        using var second = await LogoutAsync(session.RefreshToken);

        // A client that retried a logout should not be shown an error, and the
        // second call must not overwrite why the first revoked the token.
        TestHttp.EnsureSucceeded(first);
        TestHttp.EnsureSucceeded(second);

        var stored = await LoadTokenByHashAsync(
            Api.Application.Authentication.RefreshTokenService.HashToken(session.RefreshToken));

        Assert.Equal("Logout", stored.RevocationReason);
    }

    [Fact]
    public async Task Logging_out_an_unknown_token_succeeds_without_touching_anything()
    {
        var session = await TestSession.RegisterAsync(_client, "logout-unknown");

        using var response = await LogoutAsync("not-a-real-refresh-token");

        // Nothing to revoke is not a failure.
        TestHttp.EnsureSucceeded(response);

        var stored = await LoadTokenByHashAsync(
            Api.Application.Authentication.RefreshTokenService.HashToken(session.RefreshToken));

        Assert.Equal(RefreshTokenStatus.Active, stored.Status);
    }

    [Fact]
    public async Task Logging_out_with_no_token_at_all_succeeds()
    {
        using var response = await LogoutAsync(string.Empty);

        TestHttp.EnsureSucceeded(response);
    }

    [Fact]
    public async Task One_user_cannot_log_out_another_users_session()
    {
        var first = await TestSession.RegisterAsync(_client, "logout-a");
        var second = await TestSession.RegisterAsync(_client, "logout-b");

        // The second user presents the first user's token.
        using var response = await LogoutAsync(first.RefreshToken);

        TestHttp.EnsureSucceeded(response);

        // The request is indistinguishable from the owner logging out, because
        // the token is the credential. What matters is that revoking it does not
        // affect the other user's own session.
        Assert.True(await IsActiveAsync(second.RefreshToken));
    }

    // -------------------------------------------------------------- isolation

    [Fact]
    public async Task Refreshing_yields_the_workspace_the_token_was_issued_for()
    {
        var first = await TestSession.RegisterAsync(_client, "iso-a");
        var second = await TestSession.RegisterAsync(_client, "iso-b");

using var response = await RefreshAsync(first.RefreshToken);
        var body = await TestHttp.ReadAsync<AuthResponse>(response);

        TestHttp.EnsureSucceeded(response, body);

        // The workspace comes from the stored row, so a token cannot be
        // redeemed against a workspace it was not issued for.
        Assert.Equal(first.WorkspaceId, body.WorkspaceId);
        Assert.NotEqual(second.WorkspaceId, body.WorkspaceId);
        Assert.Equal(first.UserId, body.User.Id);
    }

    [Fact]
    public async Task A_token_whose_membership_was_removed_can_no_longer_refresh()
    {
        var session = await TestSession.RegisterAsync(_client, "iso-membership");

        // Server-side ownership: revoking the membership must invalidate a
        // token that is otherwise perfectly valid, without waiting for expiry.
        await RemoveMembershipAsync(session.UserId, session.WorkspaceId);

        using var response = await RefreshAsync(session.RefreshToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(
            "REFRESH_TOKEN_WORKSPACE_ACCESS_REVOKED",
            await ReadErrorCodeAsync(response));
    }

    [Fact]
    public async Task A_token_for_a_deactivated_user_can_no_longer_refresh()
    {
        var session = await TestSession.RegisterAsync(_client, "iso-inactive");

        await DeactivateUserAsync(session.UserId);

        using var response = await RefreshAsync(session.RefreshToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("ACCOUNT_INACTIVE", await ReadErrorCodeAsync(response));
    }

    [Fact]
    public async Task An_unknown_refresh_token_is_rejected_without_revealing_anything()
    {
        using var response = await RefreshAsync(
            "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("INVALID_REFRESH_TOKEN", await ReadErrorCodeAsync(response));
    }

    [Fact]
    public async Task Refreshing_with_a_blank_token_is_rejected()
    {
        using var response = await RefreshAsync("   ");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("INVALID_REFRESH_TOKEN", await ReadErrorCodeAsync(response));
    }

    // ----------------------------------------------------------------- helpers

    private Task<HttpResponseMessage> RefreshAsync(string refreshToken) =>
        _client.PostAsJsonAsync(
            "api/v1/auth/refresh",
            new { refreshToken });

    private Task<HttpResponseMessage> LogoutAsync(string refreshToken) =>
        _client.PostAsJsonAsync(
            "api/v1/auth/logout",
            new { refreshToken });

    private async Task<string> RotateAsync(string refreshToken)
    {
        using var response = await RefreshAsync(refreshToken);
        var body = await TestHttp.ReadAsync<AuthResponse>(response);

        TestHttp.EnsureSucceeded(response, body);

        return body.RefreshToken;
    }

private static async Task<string> ReadErrorCodeAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(body);

        if (!document.RootElement.TryGetProperty("error", out var error) ||
            !error.TryGetProperty("code", out var code) ||
            code.GetString() is not string codeValue)
        {
            throw new Xunit.Sdk.XunitException(
                $"Expected an error envelope with error.code, but received " +
                $"{response.StatusCode}: {body}");
        }

        return codeValue;
    }

    private async Task<RefreshToken> LoadTokenByHashAsync(string hash)
    {
        var token = await LoadTokensAsync(userId: null, hash);

        return Assert.Single(token);
    }

    private Task<List<RefreshToken>> LoadTokensAsync(
        Guid? userId = null,
        string? hash = null)
    {
        return RunAsync(context =>
        {
            var query = context.RefreshTokens.AsNoTracking();

            if (userId is not null)
            {
                query = (IQueryable<RefreshToken>)query.Where(
                    token => token.UserId == userId.Value);
            }

            if (hash is not null)
            {
                query = query.Where(token => token.TokenHash == hash);
            }

            return query.ToListAsync();
        });
    }

    private async Task<RefreshToken> LoadTokenAsync(Guid id)
    {
        var tokens = await RunAsync(context => context.RefreshTokens
            .AsNoTracking()
            .Where(token => token.Id == id)
            .ToListAsync());

        return Assert.Single(tokens);
    }

    private async Task<bool> IsActiveAsync(string refreshToken)
    {
        var stored = await LoadTokenByHashAsync(
            Api.Application.Authentication.RefreshTokenService.HashToken(refreshToken));

        return stored.Status == RefreshTokenStatus.Active;
    }

    private async Task<int> CountGenerationsAsync(Guid startId)
    {
        var generations = 0;
        var cursor = (Guid?)startId;

        while (cursor is not null)
        {
            generations++;
            cursor = (await LoadTokenAsync(cursor.Value)).ReplacedById;
        }

        return generations;
    }

    private Task ExpireAsync(string hash) =>
        RunAsync(context => context.RefreshTokens
            .Where(token => token.TokenHash == hash)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(token => token.ExpiresAt, DateTime.UtcNow.AddDays(-1))));

    private Task SetExpiryAsync(string hash, DateTime expiresAt) =>
        RunAsync(context => context.RefreshTokens
            .Where(token => token.TokenHash == hash)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(token => token.ExpiresAt, expiresAt)));

    private Task RemoveMembershipAsync(Guid userId, Guid workspaceId) =>
        RunAsync(context => context.WorkspaceMembers
            .Where(member =>
                member.UserId == userId &&
                member.WorkspaceId == workspaceId)
            .ExecuteDeleteAsync());

    private Task DeactivateUserAsync(Guid userId) =>
        RunAsync(context => context.Users
            .Where(user => user.Id == userId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(user => user.IsActive, false)));

    private async Task<int> ReadConfiguredRefreshDaysAsync()
    {
        var options = _factory.Services
            .GetRequiredService<
                Microsoft.Extensions.Options.IOptions<Api.Configuration.JwtOptions>>()
            .Value;

        return options.RefreshTokenExpirationDays;
    }

    private async Task<T> RunAsync<T>(Func<AppDbContext, Task<T>> work)
    {
        await using var scope = _factory.Services.CreateAsyncScope();

        return await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private Task RunAsync(Func<AppDbContext, Task> work) =>
        RunAsync(async context =>
        {
            await work(context);

            return true;
        });
}

