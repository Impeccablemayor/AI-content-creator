using Api.Application.AI;
using Api.Application.AnalyticsSync;
using Api.Application.Publishing;
using Api.Application.Scheduling;
using Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace Api.IntegrationTests;

/// <summary>
/// Boots the real API against a real PostgreSQL and a real Redis.
/// </summary>
/// <remarks>
/// <para>
/// Configuration is supplied through environment variables rather than
/// <c>ConfigureAppConfiguration</c> because the API reads its own configuration
/// while it is still building its service collection, before any test-supplied
/// configuration source would be appended. Environment variables are part of the
/// configuration from the first line, so the API binds exactly what the test asked
/// for. The environment name is set to something that is not Development, so the
/// API's development settings file, which points at a shared working database, is
/// not loaded.
/// </para>
/// <para>
/// Each instance creates its own database, migrates it with the real migrations, and
/// drops it on dispose. The schema therefore comes from the same migration files a
/// deployment applies, rather than from <c>EnsureCreated</c> or an in-memory provider.
/// </para>
/// </remarks>
public sealed class MayorxApiFactory : WebApplicationFactory<Program>
{
private readonly string _adminConnectionString;
private readonly string _testConnectionString;
private readonly bool _ownsDatabase;

private bool _databaseCreated;

public MayorxApiFactory()
{
    _adminConnectionString = IntegrationTestSettings.AdminConnectionString;
    _testConnectionString = IntegrationTestSettings.TestConnectionString;
    _ownsDatabase = true;
}

private MayorxApiFactory(string testConnectionString)
{
    _adminConnectionString = IntegrationTestSettings.AdminConnectionString;
    _testConnectionString = testConnectionString;
    _ownsDatabase = false;

    // Applied here rather than through Initialise, because a caller that asked for
    // an unreachable host must not be able to forget to configure it.
    ConfigureUnreachable();
}

/// <summary>
/// Builds a host whose dependencies are unreachable. No database is created,
/// because the point is that the container cannot be reached at all.
/// </summary>
public static MayorxApiFactory Unreachable(string databaseConnectionString) =>
    new(databaseConnectionString);

/// <summary>
/// A client that carries a bearer token. Each call returns a separate client over
/// the same in-memory server.
/// </summary>
public HttpClient CreateAuthenticatedClient(string accessToken)
{
    var client = CreateClient();

    client.DefaultRequestHeaders.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

    return client;
}

private void ConfigureUnreachable()
{
    Set("Database__ConnectionString", _testConnectionString);
    Set("Redis__ConnectionString", "localhost:1,connectTimeout=2000,syncTimeout=2000");
    Set("Jwt__SecretKey", IntegrationTestSettings.JwtSecretKey);
    Set("Jwt__Issuer", IntegrationTestSettings.JwtIssuer);
    Set("Jwt__Audience", IntegrationTestSettings.JwtAudience);
    Set("AI__Provider", "Groq");
    Set("AI__Model", "stub-model");
    Set("AI__ApiKey", "stub-key");
}

    /// <summary>
    /// Applies the settings the API needs before its entry point reads them, then
    /// creates the database this run will own. Must run before the first client is
    /// created.
    /// </summary>
    public void Initialise()
    {
        Set("Database__ConnectionString", _testConnectionString);
        Set("Redis__ConnectionString", IntegrationTestSettings.RedisConnectionString);

        // No DataProtection key ring path is set, so keys stay in memory. The test
        // encrypts and decrypts inside one process, and a key ring on disk would be
        // shared state between runs and a file in the working tree.
        Set("Jwt__SecretKey", IntegrationTestSettings.JwtSecretKey);
        Set("Jwt__Issuer", IntegrationTestSettings.JwtIssuer);
        Set("Jwt__Audience", IntegrationTestSettings.JwtAudience);

        // The provider is replaced in the container, so these exist only to satisfy
        // the API's own startup validation, which stays in the path being tested.
        Set("AI__Provider", "Groq");
        Set("AI__Model", "stub-model");
        Set("AI__ApiKey", "stub-key");

        // Publishing validates that a configured platform is fully configured, even
        // though no request ever leaves the process.
        Set("Social__LinkedIn__ClientId", "stub-client");
        Set("Social__LinkedIn__ClientSecret", "stub-secret");
        Set("Social__LinkedIn__RedirectUri", "https://example.test/callback");
        Set("Social__LinkedIn__Scopes__0", "openid");

// Two measured posts are enough to produce a finding when each group holds a
        // single post, which keeps the run fast without weakening the rule under
        // test: the grouping, the gap, and the persistence are all unchanged.
        Set("LearningLoop__LookbackDays", "90");
        Set("LearningLoop__MinimumMeasuredPosts", "2");
        Set("LearningLoop__MinimumPostsPerGroup", "1");
        Set("LearningLoop__MinimumEngagementRateGap", "0.05");

        CreateDatabase();
    }

private void CreateDatabase()
    {
        if (!_ownsDatabase || _databaseCreated)
        {
            return;
        }

        using var connection = new NpgsqlConnection(_adminConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE \"{IntegrationTestSettings.DatabaseName}\"";
        command.ExecuteNonQuery();

        _databaseCreated = true;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // The API deliberately does not register the scheduler or the publisher:
            // the worker host owns both. This test drives the same closed loop the
            // worker drives, so it registers them the way the worker does, by calling
            // the same extension methods on the same configuration the API uses.
            var configuration = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();

services.AddSocialPublishing(configuration);
            services.AddAnalyticsSync(configuration);

            // Registered exactly as the worker registers them, because the worker
            // wires these outside the extension methods above.
            services.AddScoped<IScheduledPostStore, ScheduledPostDbStore>();
            services.AddScoped<IScheduledPostProcessor, ScheduledPostProcessor>();
            services.AddScoped<IAnalyticsSyncStore, AnalyticsSyncDbStore>();

            // Only the two edges that require the internet are replaced. The real
            // scheduler, publish service, credential protector, metrics store,
            // reporting service, analyser, and persistence stay in play.
            services.RemoveAll<ISocialPlatformPublisher>();
            services.AddScoped<ISocialPlatformPublisher, StubLinkedInPublisher>();

            services.RemoveAll<ISocialPlatformMetricsProvider>();
            services.AddScoped<ISocialPlatformMetricsProvider, StubLinkedInMetricsProvider>();

            services.RemoveAll<IAiProvider>();
            services.AddSingleton<IAiProvider, StubAiProvider>();
        });
    }

    /// <summary>
    /// Applies the real migrations, so a run proves the migration files still produce
    /// a schema the current model can query.
    /// </summary>
    public async Task MigrateAsync()
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await dbContext.Database.MigrateAsync();
    }

    /// <summary>
    /// Runs one of the worker's processors in a request-free scope, which is how the
    /// worker host invokes it.
    /// </summary>
    public async Task<T> RunWorkerStepAsync<T>(
        Func<IServiceProvider, Task<T>> step)
    {
        using var scope = Services.CreateScope();

        return await step(scope.ServiceProvider);
    }

    public Task<ScheduledPostRunResult> RunScheduledPostProcessorAsync() =>
        RunWorkerStepAsync(services =>
            services.GetRequiredService<IScheduledPostProcessor>().RunOnceAsync(CancellationToken.None));

    public Task<AnalyticsSyncRunResult> RunAnalyticsSyncAsync() =>
        RunWorkerStepAsync(services =>
            services.GetRequiredService<IAnalyticsSyncProcessor>().RunOnceAsync(CancellationToken.None));

private static void Set(string name, string value) =>
        Environment.SetEnvironmentVariable(name, value);

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && _databaseCreated)
        {
            DropDatabase();
        }
    }

    private void DropDatabase()
    {
        // A test that leaves its database behind would fail the next run on a name
        // that already exists, and would leave rows in a developer's instance.
        NpgsqlConnection.ClearAllPools();

        using var connection = new NpgsqlConnection(_adminConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText =
            $"DROP DATABASE IF EXISTS \"{IntegrationTestSettings.DatabaseName}\" WITH (FORCE)";
        command.ExecuteNonQuery();

        _databaseCreated = false;
    }
}
