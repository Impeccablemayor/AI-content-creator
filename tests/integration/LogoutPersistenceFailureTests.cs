using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.IntegrationTests;

/// <summary>
/// What logout does when the revocation cannot be written.
/// </summary>
/// <remarks>
/// The failure is produced with a real PostgreSQL privilege revocation rather
/// than a mocked context, because the property under test is precisely that the
/// code does not swallow a database error. A mock would let the assertion pass
/// while the real command still succeeded.
/// </remarks>
public sealed class LogoutPersistenceFailureTests : IAsyncLifetime
{
    private readonly MayorxApiFactory _factory;

    private HttpClient _client = null!;

    public LogoutPersistenceFailureTests()
    {
        // Throttling is off so the request under test is not refused for an
        // unrelated reason.
        _factory = MayorxApiFactory.WithSettings(
            new Dictionary<string, string?>
            {
                ["RateLimiting:AuthenticationPermitLimit"] = "0"
            });
    }

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

    [Fact]
    public async Task Logout_reports_a_failure_rather_than_claiming_success()
    {
        var session = await TestSession.RegisterAsync(_client, "logout-fail");

        Assert.True(await IsActiveAsync(session));

        await BreakRefreshTokenUpdatesAsync();

        try
        {
            using var response = await _client.PostAsJsonAsync(
                "api/v1/auth/logout",
                new { refreshToken = session.RefreshToken });

            // Reporting 200 here would tell the user their session is closed
            // while the token is still usable, which is the worst possible
            // answer to give.
            Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(
                HttpStatusCode.InternalServerError,
                response.StatusCode);

            var body = await response.Content.ReadAsStringAsync();

            using var document = JsonDocument.Parse(body);

            Assert.Equal(
                "INTERNAL_SERVER_ERROR",
                document.RootElement.GetProperty("error").GetProperty("code").GetString());

            // And nothing leaked about the database in the process.
            Assert.DoesNotContain("refresh_tokens", body, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await RestoreRefreshTokenUpdatesAsync();
        }
    }

    [Fact]
    public async Task A_failed_logout_leaves_the_token_exactly_as_it_was()
    {
        var session = await TestSession.RegisterAsync(_client, "logout-fail-state");

        await BreakRefreshTokenUpdatesAsync();

        try
        {
            using var response = await _client.PostAsJsonAsync(
                "api/v1/auth/logout",
                new { refreshToken = session.RefreshToken });

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

            // No half-written state: the token was not partially revoked, and no
            // replacement was invented.
            var tokens = await LoadTokensAsync(session.UserId);

            var stored = Assert.Single(tokens);

            Assert.Equal(Api.Domain.Enums.RefreshTokenStatus.Active, stored.Status);
            Assert.Null(stored.RevokedAt);
            Assert.Null(stored.RevocationReason);
            Assert.Null(stored.ReplacedById);
        }
        finally
        {
            await RestoreRefreshTokenUpdatesAsync();
        }
    }

    [Fact]
    public async Task The_token_still_works_once_logout_can_reach_the_database_again()
    {
        var session = await TestSession.RegisterAsync(_client, "logout-recover");

        await BreakRefreshTokenUpdatesAsync();

        try
        {
            using var refused = await _client.PostAsJsonAsync(
                "api/v1/auth/logout",
                new { refreshToken = session.RefreshToken });

            Assert.Equal(HttpStatusCode.InternalServerError, refused.StatusCode);
        }
        finally
        {
            await RestoreRefreshTokenUpdatesAsync();
        }

        // The token was never revoked, so rotating it proves the failed logout
        // did not damage the session.
        using var refresh = await _client.PostAsJsonAsync(
            "api/v1/auth/refresh",
            new { refreshToken = session.RefreshToken });

        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
    }

    // ---------------------------------------------------------------- helpers

    private async Task<bool> IsActiveAsync(TestSession session)
    {
        var tokens = await LoadTokensAsync(session.UserId);

        return Assert.Single(tokens).Status
            == Api.Domain.Enums.RefreshTokenStatus.Active;
    }

    private Task<List<Api.Domain.Entities.RefreshToken>> LoadTokensAsync(
        Guid userId)
    {
        return RunAsync(async context =>
        {
            await using var transaction = await context.Database
                .BeginTransactionAsync();

            var tokens = await context.RefreshTokens
                .AsNoTracking()
                .Where(token => token.UserId == userId)
                .ToListAsync();

            await transaction.RollbackAsync();

            return tokens;
        });
    }

    /// <summary>
    /// Makes every update to the refresh token table fail at the database, the way
    /// a storage outage or a full disk would.
    /// </summary>
    /// <remarks>
    /// A privilege revocation would not do: the role that owns the table keeps
    /// owner rights regardless, so the update would still succeed and the test
    /// would prove nothing. A trigger raising an exception fails the statement
    /// inside PostgreSQL itself, which is the failure mode being tested.
    /// </remarks>
    private Task BreakRefreshTokenUpdatesAsync() =>
        RunAgainstTestDatabaseAsync(
            """
            CREATE OR REPLACE FUNCTION fail_refresh_token_update()
            RETURNS trigger AS $$
            BEGIN
                RAISE EXCEPTION 'storage is unavailable';
            END;
            $$ LANGUAGE plpgsql;

            CREATE TRIGGER refresh_token_update_failure
            BEFORE UPDATE ON "RefreshTokens"
            FOR EACH ROW EXECUTE FUNCTION fail_refresh_token_update();
            """);

    private Task RestoreRefreshTokenUpdatesAsync() =>
        RunAgainstTestDatabaseAsync(
            """
            DROP TRIGGER IF EXISTS refresh_token_update_failure ON "RefreshTokens";

            DROP FUNCTION IF EXISTS fail_refresh_token_update();
            """);

    private async Task<T> RunAsync<T>(
        Func<Api.Infrastructure.Persistence.AppDbContext, Task<T>> work)
    {
        await using var scope = _factory.Services.CreateAsyncScope();

        return await work(
            scope.ServiceProvider
                .GetRequiredService<Api.Infrastructure.Persistence.AppDbContext>());
    }

    /// <summary>
    /// Runs against this factory's own database, not the shared admin database,
    /// because the table only exists in the schema the migrations created.
    /// </summary>
    private async Task RunAgainstTestDatabaseAsync(string commandText)
    {
        await using var connection =
            new Npgsql.NpgsqlConnection(_factory.TestConnectionString);

        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = commandText;

        await command.ExecuteNonQueryAsync();
    }
}
