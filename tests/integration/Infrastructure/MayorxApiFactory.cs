using Api.Application.AI;
using Api.Application.AnalyticsSync;
using Api.Application.Publishing;
using Api.Application.Scheduling;
using Api.Configuration;
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
    private readonly string _databaseName;
    private readonly bool _ownsDatabase;
private readonly IReadOnlyDictionary<string, string?> _settings;

    /// <summary>
    /// The origin a test host is permitted to call from unless a test asks for
    /// different ones.
    /// </summary>
    /// <remarks>
    /// The API refuses to start outside Development when no allowed origin is
    /// configured, which is the intended production behaviour. Test hosts run
    /// with the environment set to Testing, so every one of them needs an origin,
    /// and this is the one place that can supply it without each test class
    /// inventing its own.
    /// </remarks>
    public const string DefaultAllowedOrigin = "https://app.test";

    /// <summary>
    /// Connection string for the database this factory owns, for tests that need
    /// to reach it outside the API, such as inspecting rows or changing privileges.
    /// </summary>
    public string TestConnectionString => _testConnectionString;

    private bool _databaseCreated;
    private string _environment = "Testing";
    private bool _ownsAuthCookieSettings;

    /// <summary>
    /// Stops <see cref="Initialise"/> from forcing the test-wide cookie settings,
    /// so a host can run on the API's own defaults.
    /// </summary>
    /// <remarks>
    /// The cookie tests need <c>AuthCookies:Secure=false</c> because the test host
    /// serves plain HTTP, but a test that asserts what the API chooses unprompted
    /// needs the opposite. The two are separated by this flag rather than by
    /// ordering, because the settings are process-wide and ordering alone would
    /// depend on which test class happened to build its host first.
    /// </remarks>
    public void OwnsAuthCookieSettings()
    {
        _ownsAuthCookieSettings = true;
    }

    /// <summary>
    /// Restores the process-wide cookie settings every host expects.
    /// </summary>
    public static void ResetAuthCookieEnvironment()
    {
        Environment.SetEnvironmentVariable(
            "AuthCookies__SameSite",
            "Lax");
        Environment.SetEnvironmentVariable(
            "AuthCookies__Secure",
            "false");
    }

    /// <summary>
    /// Selects the hosting environment for this host, which is what decides
    /// whether development-only surfaces such as Swagger are served.
    /// </summary>
    public void UseEnvironmentFor(string environment) => _environment = environment;

    public MayorxApiFactory()
        : this(testConnectionString: null, settings: null)
    {
    }

private MayorxApiFactory(
        string? testConnectionString,
        IReadOnlyDictionary<string, string?>? settings)
    {
        _adminConnectionString = IntegrationTestSettings.AdminConnectionString;

        // One database per factory, not one per process. A test that needs a second
        // host with different settings, which several of these do, would otherwise
        // fight the first host over the same name. Kept short enough that PostgreSQL
        // cannot silently truncate two different names into one.
        _databaseName = $"mayorx_itest_{Guid.NewGuid():N}";

        _testConnectionString = testConnectionString ?? BuildConnectionString(
            IntegrationTestSettings.AdminConnectionString,
            _databaseName);

        _ownsDatabase = testConnectionString is null;
        _settings = WithDefaultOrigin(settings);

        if (testConnectionString is not null)
        {
            // Applied here rather than through Initialise, because a caller that
            // asked for an unreachable host must not be able to forget it.
            ConfigureUnreachable();
        }
    }

    private static string BuildConnectionString(
        string connectionString,
        string database)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);

        builder.Database = database;

        return builder.ConnectionString;
    }

    /// <summary>
    /// Builds a factory that additionally overrides configuration for this run.
    /// </summary>
    /// <remarks>
    /// Overrides are applied with <c>UseSetting</c> rather than environment
    /// variables because environment variables are per process: one test class
    /// setting a rate limit would otherwise be visible to whichever class happened
    /// to build its host next. Host settings are scoped to this factory's host.
    /// </remarks>
    public static MayorxApiFactory WithSettings(
        IReadOnlyDictionary<string, string?>? settings) =>
        new(testConnectionString: null, settings);

    /// <summary>
    /// Builds a factory whose dependencies are unreachable. No database is created,
    /// because the point is that the container cannot be reached at all.
    /// </summary>
    public static MayorxApiFactory Unreachable(string databaseConnectionString) =>
        new(databaseConnectionString, settings: null);

    /// <summary>
    /// Returns the caller's settings, with a permitted origin added only when the
    /// caller has not configured one. A test that supplies its own origins keeps
    /// exactly those, so origin-matching behaviour can be asserted honestly.
    /// </summary>
    private static IReadOnlyDictionary<string, string?> WithDefaultOrigin(
        IReadOnlyDictionary<string, string?>? settings)
    {
        var effective = new Dictionary<string, string?>(
            settings ?? new Dictionary<string, string?>());

        var configured = effective.Keys.Any(key =>
            key.StartsWith("Cors:AllowedOrigins", StringComparison.OrdinalIgnoreCase));

        if (!configured)
        {
            effective["Cors:AllowedOrigins:0"] = DefaultAllowedOrigin;
        }

        return effective;
    }


/// <summary>
    /// A client that carries a bearer token. Each call returns a separate client over
    /// the in-memory server.
    /// </summary>
    public HttpClient CreateAuthenticatedClient(string accessToken)
    {
        var client = CreateClient();

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        return client;
    }

    /// <summary>
    /// A client that does not manage cookies at all, so a test decides exactly
    /// what the <c>Cookie</c> header carries.
    /// </summary>
    /// <remarks>
    /// Several of the refresh-token properties under test are about presenting
    /// one specific token: a token rotation has already consumed, a token that
    /// was never issued, an unknown value. A client that silently manages its own
    /// jar cannot be told to send one of those, so those tests drive the cookie
    /// header themselves. Ordinary browser behaviour is exercised by the default
    /// client instead.
    /// </remarks>
    public HttpClient CreateCookieControlledClient() =>
        CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });

    /// <summary>
    /// Drops the test-wide cookie settings so a host can observe the API's own
    /// defaults.
    /// </summary>
    /// <remarks>
    /// <see cref="Initialise"/> states <c>AuthCookies:Secure=false</c> for every
    /// host, which is the only way the cookie tests can work over plain HTTP. A
    /// test that needs to see what the API would choose unprompted therefore has
    /// to undo it, and has to be able to undo exactly one of the two settings
    /// without guessing which one another class changed.
    /// </remarks>
    /// <param name="sameSite">
    /// The value to configure, or null to fall back to the API's own default.
    /// </param>
    /// <param name="secure">
    /// The value to configure, or null to fall back to the API's own default.
    /// </param>
    public void ConfigureAuthCookies(string? sameSite, string? secure)
    {
        // A null value removes the variable, which is what makes the API fall
        // back to its own default rather than to a leftover one.
        Set("AuthCookies__SameSite", sameSite);
        Set("AuthCookies__Secure", secure);
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

// Stated rather than left to the default. The API marks its cookies
        // Secure everywhere except Development, which is correct for a
        // deployment and wrong here: the test host serves plain HTTP, and a
        // browser-faithful client correctly refuses to return a Secure cookie
        // over it, so every cookie test would fail for a reason that has
        // nothing to do with the code under test.
        //
        // Pinned rather than left unset, because these are process-wide variables
        // and a test that changes SameSite for its own host would otherwise leave
        // it behind for whichever class built its host next.
        if (!_ownsAuthCookieSettings)
        {
            Set("AuthCookies__Secure", "false");
            Set("AuthCookies__SameSite", "Lax");
        }

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
        command.CommandText = $"CREATE DATABASE \"{_databaseName}\"";
        command.ExecuteNonQuery();

        _databaseCreated = true;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_environment);

        // Applied before the app reads its configuration, and scoped to this host.
        foreach (var setting in _settings)
        {
            builder.UseSetting(setting.Key, setting.Value);
        }

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

private static void Set(string name, string? value) =>
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
            $"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)";
        command.ExecuteNonQuery();

        _databaseCreated = false;
    }
}
