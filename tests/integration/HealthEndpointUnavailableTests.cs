using System.Net;
using System.Text.Json;
using Xunit;

namespace Api.IntegrationTests;

/// <summary>
/// Proves the two endpoints answer differently when a dependency is gone, which is
/// the behaviour an orchestrator and a load balancer depend on.
/// </summary>
/// <remarks>
/// This class does not use the shared fixture. It needs a container whose
/// PostgreSQL and Redis are both unreachable, and pointing the shared one at dead
/// addresses would break every other test in the run.
/// </remarks>
public sealed class HealthEndpointUnavailableTests
{
    private static MayorxApiFactory UnreachableFactory() =>
        MayorxApiFactory.Unreachable(
            "Host=localhost;Port=1;Database=absent;Username=absent;" +
            "Password=absent;Timeout=2;Command Timeout=2");

    [Fact]
    public async Task Readiness_fails_when_the_database_and_cache_are_unreachable()
    {
        using var factory = UnreachableFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("api/v1/health");

        await HealthBody.AssertStatusAsync(
            response,
            HttpStatusCode.ServiceUnavailable);

        var data = await HealthBody.ReadAsync(response);

        Assert.Equal("unhealthy", data.GetProperty("status").GetString());

        var checks = data.GetProperty("checks")
            .EnumerateObject()
            .ToDictionary(entry => entry.Name, entry => entry.Value);

        Assert.Equal("unhealthy", checks["postgres"].GetProperty("status").GetString());
        Assert.Equal("unhealthy", checks["redis"].GetProperty("status").GetString());

        // The AI provider is configured, so it stays ready. That distinction is what
        // lets a report say which dependency actually failed.
        Assert.Equal("healthy", checks["ai-provider"].GetProperty("status").GetString());
    }

    [Fact]
    public async Task Liveness_stays_healthy_when_the_dependencies_are_unreachable()
    {
        using var factory = UnreachableFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("api/v1/health/live");

        // A restart is not the answer to a database outage. Returning 200 here is
        // what keeps a dependency incident from turning into a crash loop.
        await HealthBody.AssertStatusAsync(response, HttpStatusCode.OK);

        var data = await HealthBody.ReadAsync(response);

        Assert.Equal("healthy", data.GetProperty("status").GetString());
    }
}