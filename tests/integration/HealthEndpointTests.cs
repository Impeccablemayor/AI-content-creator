using System.Net;
using System.Text.Json;
using Xunit;

namespace Api.IntegrationTests;

/// <summary>
/// Exercises the endpoints a load balancer and an orchestrator actually poll, against
/// a real PostgreSQL and a real Redis.
/// </summary>
public sealed class HealthEndpointTests : IClassFixture<MayorxApiFactory>, IAsyncLifetime
{
    private readonly MayorxApiFactory _factory;

    private HttpClient _client = null!;

    public HealthEndpointTests(MayorxApiFactory factory)
    {
        _factory = factory;
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

        return Task.CompletedTask;
    }

    [Fact]
    public async Task Readiness_reports_every_registered_check_as_healthy()
    {
        using var response = await _client.GetAsync("api/v1/health");

        await HealthBody.AssertStatusAsync(response, HttpStatusCode.OK);

        var data = await HealthBody.ReadAsync(response);

        Assert.Equal("healthy", data.GetProperty("status").GetString());
        Assert.Equal(
            "mayorx-content-engine-api",
            data.GetProperty("service").GetString());

        var checks = data.GetProperty("checks");

        Assert.Equal(
            ["ai-provider", "postgres", "redis"],
            checks.EnumerateObject().Select(check => check.Name).Order());

        Assert.All(
            checks.EnumerateObject(),
            check => Assert.Equal(
                "healthy",
                check.Value.GetProperty("status").GetString()));

        Assert.True(data.GetProperty("totalDurationMs").GetDouble() >= 0);
    }

    [Fact]
    public async Task Liveness_does_not_consult_its_dependencies()
    {
        using var response = await _client.GetAsync("api/v1/health/live");

        await HealthBody.AssertStatusAsync(response, HttpStatusCode.OK);

        var data = await HealthBody.ReadAsync(response);

        Assert.Equal("healthy", data.GetProperty("status").GetString());

        // No entries, because nothing was asked to check. That is the difference
        // between liveness and readiness: a restart must not be triggered by a
        // dependency being down.
        Assert.Empty(data.GetProperty("checks").EnumerateObject());
    }

    [Fact]
    public async Task Health_responses_carry_a_correlation_id()
    {
        using var response = await _client.GetAsync("api/v1/health/live");

        Assert.True(response.Headers.Contains("X-Correlation-Id"));
        Assert.False(string.IsNullOrWhiteSpace(
            response.Headers.GetValues("X-Correlation-Id").Single()));
    }

    [Fact]
    public async Task An_inbound_correlation_id_is_echoed_back()
    {
        const string supplied = "integration-trace-123";

        using var request = new HttpRequestMessage(HttpMethod.Get, "api/v1/health/live");
        request.Headers.Add("X-Correlation-Id", supplied);

        using var response = await _client.SendAsync(request);

        Assert.Equal(supplied, response.Headers.GetValues("X-Correlation-Id").Single());
    }
}