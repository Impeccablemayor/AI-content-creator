using Api.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Api.Infrastructure.Persistence;

/// <summary>
/// Reports whether the database this API reads and writes is reachable.
/// </summary>
/// <remarks>
/// <para>
/// This opens a short-lived connection rather than resolving <c>AppDbContext</c>,
/// because a health check is constructed once for the lifetime of the host while
/// the context is scoped. A check that held a context would either capture one
/// scoped service in a singleton or have to run against a database that is up but
/// has not been migrated.
/// </para>
/// <para>
/// The probe is <c>SELECT 1</c>, which proves a connection and a round trip. It
/// deliberately does not query an application table, because a missing table means
/// the deployment is mis-migrated, which is a deployment problem an orchestrator
/// should not see as a reason to restart a process that is otherwise serving.
/// </para>
/// </remarks>
public sealed class PostgresHealthCheck(IOptions<DatabaseOptions> databaseOptions)
    : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var connectionString = databaseOptions.Value.ConnectionString;

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return HealthCheckResult.Unhealthy(
                "No database connection string is configured.");
        }

        try
        {
            await using var connection = new NpgsqlConnection(connectionString);

            await connection.OpenAsync(cancellationToken);

            await using var command = new NpgsqlCommand("SELECT 1", connection);

            await command.ExecuteScalarAsync(cancellationToken);

            return HealthCheckResult.Healthy("PostgreSQL is reachable.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy(
                "PostgreSQL is unavailable.",
                exception);
        }
    }
}
