namespace Api.IntegrationTests;

/// <summary>
/// Connection details for the integration run, taken from the environment so the
/// same tests work locally and in CI.
/// </summary>
/// <remarks>
/// These point at the containers in the repository's own <c>docker-compose.yml</c>,
/// which is the point: an integration test that provisions its own database engine
/// tests a different system from the one that ships. The only thing created here is
/// a database inside that already running Postgres, so a run cannot collide with a
/// developer's working data.
/// </remarks>
public static class IntegrationTestSettings
{
    public const string PostgresConnectionStringVariable =
        "MAYORX_TEST_POSTGRES";

    public const string RedisConnectionStringVariable =
        "MAYORX_TEST_REDIS";

    /// <summary>
    /// The schema the run owns. Each run gets its own database and drops it again,
    /// so concurrent runs against one Postgres do not see each other's rows.
    /// </summary>
    /// <remarks>
    /// Held in a field so every read returns the same name. A property that built a
    /// new GUID per read would create one database, then query and migrate a second,
    /// which fails in a way that looks like a schema problem.
    /// </remarks>
    public static string DatabaseName { get; } =
        $"mayorx_integration_{Guid.NewGuid():N}";

    public static string AdminConnectionString =>
        BuildConnectionString(PostgresConnectionString);

    public static string TestConnectionString =>
        BuildConnectionString(PostgresConnectionString, DatabaseName);

    public static string RedisConnectionString =>
        Environment.GetEnvironmentVariable(RedisConnectionStringVariable)
        ?? "localhost:6379";

    /// <summary>
    /// Long enough for the API's own startup validation, which requires at least 32
    /// characters. It is a test value for a database that is dropped after the run.
    /// </summary>
    public const string JwtSecretKey = "integration-test-signing-key-not-a-real-secret";

    public const string JwtIssuer = "mayorx-content-engine-tests";

    public const string JwtAudience = "mayorx-content-engine-tests";

    private static string PostgresConnectionString =>
        Environment.GetEnvironmentVariable(PostgresConnectionStringVariable)
        ?? "Host=localhost;Port=5432;Database=postgres;Username=mayorx;Password=mayorx_dev_password";

    private static string BuildConnectionString(
        string connectionString,
        string? database = null)
    {
        var builder = new Npgsql.NpgsqlConnectionStringBuilder(connectionString);

        if (database is not null)
        {
            builder.Database = database;
        }

        return builder.ConnectionString;
    }
}
