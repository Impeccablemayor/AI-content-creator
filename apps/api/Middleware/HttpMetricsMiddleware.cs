using System.Diagnostics;
using Api.Infrastructure.Metrics;
using Microsoft.AspNetCore.Routing;

namespace Api.Middleware;

/// <summary>
/// Counts and times every HTTP request the API handles.
/// </summary>
/// <remarks>
/// Sits directly after the correlation middleware so nothing can short-circuit
/// past it, and it records from a finally block so a request that ended in an
/// exception is counted with the status the exception handler wrote rather than
/// disappearing from the totals.
/// </remarks>
public sealed class HttpMetricsMiddleware(RequestDelegate next)
{
    private static readonly double SecondsPerTick = 1d / Stopwatch.Frequency;

    public async Task InvokeAsync(HttpContext context)
    {
        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            await next(context);
        }
        finally
        {
            try
            {
                ApplicationMetrics.RecordHttpRequest(
                    context.Request.Method,
                    context.Response.StatusCode,
                    RouteTemplate(context),
                    (Stopwatch.GetTimestamp() - startedAt) * SecondsPerTick);
            }
            catch (Exception)
            {
                // Measuring a request must never be able to fail it. A fault here
                // would replace the real outcome of the request, including the
                // exception the handler was about to report.
            }
        }
    }

    /// <summary>
    /// The route pattern the request matched, never the path it asked for.
    /// </summary>
    /// <remarks>
    /// The raw path carries identifiers and whatever else a caller chose to put in
    /// it, and it is unbounded. The route template is a fixed entry in the routing
    /// table, so a metric built from it has a known number of possible values.
    /// </remarks>
    private static string? RouteTemplate(HttpContext context) =>
        (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText;
}