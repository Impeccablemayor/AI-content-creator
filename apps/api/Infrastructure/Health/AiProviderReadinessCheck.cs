using Api.Application.AI;
using Api.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Api.Infrastructure.Health;

/// <summary>
/// Reports how the AI provider is configured.
/// </summary>
/// <remarks>
/// <para>
/// This is a configuration check and makes no network call on purpose. Calling the
/// provider from a health endpoint would mean every readiness probe, every container
/// orchestrator probe, and every CI run depends on a third party being reachable and
/// responsive, and a provider outage would then mark the whole service unhealthy and
/// take down a deployment that is otherwise serving traffic fine. Provider
/// availability is a real dependency, but it is not a reason to restart this process.
/// </para>
/// <para>
/// The state is still reported rather than hidden. A deployment that intends to
/// generate content and has no provider configured is reported as
/// <see cref="HealthStatus.Degraded"/>, which appears in the response body and in the
/// aggregate status without failing the endpoint, so the gap is visible in monitoring
/// without making local development and CI brittle. The provider name and model are
/// included; the API key never is.
/// </para>
/// </remarks>
public sealed class AiProviderReadinessCheck(
    IOptions<AiOptions> aiOptions,
    IAiProvider aiProvider) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var options = aiOptions.Value;
        var isConfigured = !options.Provider.Equals(
            "Unavailable",
            StringComparison.OrdinalIgnoreCase);

        var description = isConfigured
            ? $"AI provider {options.Provider} ({aiProvider.GetType().Name}) " +
              $"using model {options.Model}."
            : "No AI provider is configured. Content generation and learning " +
              "recommendations are unavailable, everything else is not.";

        return Task.FromResult(isConfigured
            ? HealthCheckResult.Healthy(description)
            : HealthCheckResult.Degraded(description));
    }
}
