using System.Text.Json;
using Api.Contracts.Responses;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Api.Infrastructure.Health;

/// <summary>
/// Writes health check results in the same envelope as every other API response.
/// </summary>
/// <remarks>
/// <para>
/// The default health check writer emits a flat document with its own field names,
/// so an orchestrator probe and a client of this API would have to parse two shapes.
/// This keeps one envelope, and preserves the <c>data.status</c> and
/// <c>data.service</c> fields the previous health controller returned, adding the
/// per-dependency detail that makes the response diagnostic rather than a constant.
/// </para>
/// <para>
/// The status values stay lowercase. The field is no longer a constant, but the
/// value a healthy instance reported before is unchanged, so a client comparing
/// <c>data.status</c> against <c>"healthy"</c> keeps working.
/// </para>
/// </remarks>
public static class HealthResponseWriter
{
    public const string ServiceName = "mayorx-content-engine-api";

    public static async Task WriteAsync(
        HttpContext context,
        HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        // Only a dependency the process cannot work without takes the instance out
        // of rotation. A degraded check, such as an unconfigured AI provider, is
        // still a service that can answer requests.
        context.Response.StatusCode = report.Status == HealthStatus.Unhealthy
            ? StatusCodes.Status503ServiceUnavailable
            : StatusCodes.Status200OK;

        var response = new ApiResponse<object>
        {
            Data = new
            {
                Status = Format(report.Status),
                Service = ServiceName,
                TotalDurationMs = Math.Round(report.TotalDuration.TotalMilliseconds),
                Checks = report.Entries.ToDictionary(
                    entry => entry.Key,
                    entry => new
                    {
                        Status = Format(entry.Value.Status),
                        entry.Value.Description,
                        DurationMs = Math.Round(
                            entry.Value.Duration.TotalMilliseconds)
                    })
            },
            Message = report.Status == HealthStatus.Unhealthy
                ? "One or more dependencies are unavailable"
                : "Service is healthy"
        };

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(
                response,
                JsonSerializerOptions.Web));
    }

    private static string Format(HealthStatus status) =>
        status.ToString().ToLowerInvariant();
}
