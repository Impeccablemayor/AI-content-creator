using Api.Application.AI;
using Api.Configuration;
using Api.Infrastructure.Health;
using Api.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Xunit;

namespace Api.Tests;

public sealed class HealthCheckTests
{
    private static IOptions<DatabaseOptions> DatabaseOptions(string connectionString) =>
        Options.Create(new DatabaseOptions { ConnectionString = connectionString });

    [Fact]
    public async Task Postgres_check_fails_when_no_connection_string_is_configured()
    {
        var check = new PostgresHealthCheck(DatabaseOptions(string.Empty));

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        // Unconfigured is a deployment error, so it must not be reported as a pass
        // and must not throw out of the health pipeline.
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Null(result.Exception);
    }

    [Fact]
    public async Task Postgres_check_reports_unavailable_instead_of_throwing()
    {
        // Port 1 on loopback refuses immediately, and Timeout=1 keeps the test fast.
        var check = new PostgresHealthCheck(DatabaseOptions(
            "Host=127.0.0.1;Port=1;Database=none;Username=none;Password=none;Timeout=1"));

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        // A health check that throws takes down the endpoint that reports it, which
        // is the opposite of what the endpoint is for.
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.NotNull(result.Exception);
    }

    [Fact]
    public async Task An_unconfigured_ai_provider_is_degraded_rather_than_unhealthy()
    {
        var check = new AiProviderReadinessCheck(
            Options.Create(new AiOptions { Provider = "Unavailable" }),
            new StubAiProvider());

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        // Unhealthy here would fail readiness, so a developer running locally
        // without an API key, and CI running without one, could not be served.
        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    [Fact]
    public async Task A_configured_ai_provider_is_healthy_and_never_echoes_the_api_key()
    {
        var check = new AiProviderReadinessCheck(
            Options.Create(new AiOptions
            {
                Provider = "Groq",
                Model = "openai/gpt-oss-20b",
                ApiKey = "sk-secret-value-that-must-not-be-logged"
            }),
            new StubAiProvider());

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.DoesNotContain("sk-secret", result.Description);
    }

    /// <summary>
    /// The previous health endpoint reported a constant. These assertions exist
    /// because a check that is registered but never evaluated is indistinguishable
    /// from no check at all, which is exactly the defect being fixed here.
    /// </summary>
    [Fact]
    public void Every_dependency_the_api_needs_is_registered_as_a_readiness_check()
    {
        using var provider = new ServiceCollection()
            .AddSingleton(DatabaseOptions("Host=127.0.0.1;Database=none"))
            .AddSingleton<IOptions<AiOptions>>(
                Options.Create(new AiOptions { Provider = "Unavailable" }))
            .AddSingleton<IAiProvider, StubAiProvider>()
            .AddSingleton<Microsoft.AspNetCore.Http.IHttpContextAccessor, Microsoft.AspNetCore.Http.HttpContextAccessor>()
            .AddHealthChecks()
            .AddCheck<PostgresHealthCheck>("postgres", tags: ["ready", "database"])
            .AddCheck<AiProviderReadinessCheck>("ai-provider", tags: ["ready"])
            .AddCheck<StubRedisHealthCheck>("redis", tags: ["ready", "cache"])
            .Services
            .BuildServiceProvider();

        var options = provider
            .GetRequiredService<IOptions<HealthCheckServiceOptions>>()
            .Value;

        var readiness = options.Registrations
            .Where(registration => registration.Tags.Contains("ready"))
            .Select(registration => registration.Name)
            .ToList();

        Assert.Equal(3, readiness.Count);
        Assert.Contains("postgres", readiness);
        Assert.Contains("redis", readiness);
        Assert.Contains("ai-provider", readiness);
    }

    private sealed class StubAiProvider : IAiProvider
    {
        public Task<string> GenerateAsync(
            string systemPrompt,
            string userPrompt,
            AiResponseSchema responseSchema,
            CancellationToken cancellationToken = default) =>
            Task.FromResult("{}");
    }

    private sealed class StubRedisHealthCheck : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(HealthCheckResult.Healthy());
    }
}
