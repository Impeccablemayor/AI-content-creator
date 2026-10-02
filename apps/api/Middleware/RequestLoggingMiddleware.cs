using System.Diagnostics;
using System.Security.Claims;
using Api.Infrastructure.Metrics;
using Microsoft.AspNetCore.Routing;

namespace Api.Middleware;

/// <summary>
/// Logs one line per API request with the fields an incident needs.
/// </summary>
/// <remarks>
/// Runs after authentication so the caller and workspace are known, and before
/// authorization so a rejected request is still logged: a 401 is one of the
/// things worth seeing, and a pipeline that only logs what got through would hide
/// it.
/// </remarks>
public sealed class RequestLoggingMiddleware(
    RequestDelegate next,
    ILogger<RequestLoggingMiddleware> logger)
{
    /// <summary>
    /// Routes an orchestrator or a scraper calls on a timer.
    /// </summary>
    /// <remarks>
    /// Logged at Debug rather than Information so a probe every few seconds does
    /// not bury the requests a person is actually looking for. They are still
    /// counted in <c>mayorx_http_server_requests_total</c>, so their rate is
    /// visible.
    /// </remarks>
    private static readonly string[] ProbeRoutes =
        [MetricsScrapeEndpoint.Route, "/api/v1/health", "/api/v1/health/live"];

    public async Task InvokeAsync(HttpContext context)
    {
        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            await next(context);
        }
        finally
        {
            // The same reason the HTTP metrics record from a finally: a request
            // that threw has an outcome worth logging too.
            Log(context, (Stopwatch.GetTimestamp() - startedAt) * 1000d / Stopwatch.Frequency);
        }
    }

    private void Log(HttpContext context, double durationMilliseconds)
    {
        var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText
            ?? ApplicationMetrics.UnmatchedRoute;

        var fields = new object?[]
        {
            context.Request.Method,
            route,
            context.Response.StatusCode,
            Math.Round(durationMilliseconds),
            context.User.FindFirstValue(ClaimTypes.NameIdentifier),
            context.User.FindFirstValue(WorkspaceClaim)
        };

        // Only what is already known: the method, the route from the routing
        // table, the status and the caller the token identifies. No path, no query
        // string, no body, no header, no token. A request log is read far more
        // widely than the row it came from, so it holds nothing that would have
        // to be redacted later.
        if (Array.IndexOf(ProbeRoutes, route) >= 0)
        {
            logger.LogDebug(
                "{Method} {Route} {StatusCode} in {DurationMs}ms (user {UserId}, workspace {WorkspaceId})",
                fields);
            return;
        }

        logger.LogInformation(
            "{Method} {Route} {StatusCode} in {DurationMs}ms (user {UserId}, workspace {WorkspaceId})",
            fields);
    }

    /// <summary>
    /// The claim <see cref="Application.Authentication.CurrentUserService"/>
    /// reads the workspace from. Duplicated rather than referenced so this
    /// middleware stays usable on an unauthenticated request, where that service
    /// would throw.
    /// </summary>
    private const string WorkspaceClaim = "workspace_id";
}