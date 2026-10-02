using Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Xunit;

namespace Api.Tests;

/// <summary>
/// The HTTP instrument has to survive whatever the pipeline does to it, and must
/// never carry anything a caller chose.
/// </summary>
/// <remarks>
/// Each test counts against its own route template, so the assertion is an exact
/// value rather than a delta. The instruments are process-wide, so a shared series
/// would make these assertions depend on which test happened to run first.
/// </remarks>
[Collection(ApplicationMetricsCollection.Name)]
public sealed class HttpMetricsMiddlewareTests
{
    [Fact]
    public async Task A_handled_request_is_counted_against_the_route_it_matched()
    {
        await InvokeAsync(
            _ => { },
            path: "/api/v1/tests/handled",
            route: HandledRoute,
            statusCode: StatusCodes.Status201Created);

        Assert.Equal(1, Count(HandledRoute, "2xx"));
    }

    [Fact]
    public async Task A_failed_request_is_counted_as_a_failure()
    {
        await InvokeAsync(
            _ => { },
            path: "/api/v1/tests/failed",
            route: FailedRoute,
            statusCode: StatusCodes.Status500InternalServerError);

        Assert.Equal(1, Count(FailedRoute, "5xx"));
    }

    [Fact]
    public async Task A_request_that_threw_is_counted_with_the_status_it_returned()
    {
        // The middleware records from a finally block. A request that ended in an
        // exception has to appear in the totals with the status the exception
        // handler wrote, or a spike of 500s would look like a quiet API.
        var middleware = new HttpMetricsMiddleware(_ =>
        {
            throw new InvalidOperationException("endpoint blew up");
        });

        var context = Context("/api/v1/tests/threw", ThrewRoute);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            middleware.InvokeAsync(context));

        Assert.Equal(1, Count(ThrewRoute, "2xx"));
    }

    [Fact]
    public async Task A_request_that_matches_no_endpoint_is_counted_as_unmatched()
    {
        var before = Count("unmatched", "4xx");

        await InvokeAsync(
            _ => { },
            path: "/api/v1/does-not-exist",
            route: null,
            statusCode: StatusCodes.Status404NotFound);

        Assert.Equal(before + 1, Count("unmatched", "4xx"));
    }

    [Fact]
    public async Task The_requested_path_is_never_used_as_a_label()
    {
        // A path carries content post identifiers, and anything else a caller put
        // in it. The routing table has a fixed number of templates; the number of
        // paths a caller can ask for does not.
        var contentPostId = Guid.NewGuid();

        await InvokeAsync(
            _ => { },
            path: $"/api/v1/content/posts/{contentPostId}",
            route: ByIdRoute,
            statusCode: StatusCodes.Status404NotFound);

        Assert.Equal(1, Count(ByIdRoute, "4xx"));

        var text = MetricsScrape.Text;

        // The template is reported, and the concrete path it stood for is not.
        Assert.Contains(ByIdRoute, text, StringComparison.Ordinal);
        Assert.DoesNotContain(contentPostId.ToString(), text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unresolved_request_is_reported_as_unmatched_without_its_query_string()
    {
        // A 404 from an unmatched path must still be attributed to something
        // bounded. The raw path is not available as a label, so the series is
        // reported as unmatched rather than guessed at.
        var before = Count("unmatched", "4xx");
        var contentPostId = Guid.NewGuid();

        await InvokeAsync(
            _ => { },
            path: $"/api/v1/content/posts/{contentPostId}?include=everything",
            route: null,
            statusCode: StatusCodes.Status404NotFound);

        var text = MetricsScrape.Text;

        Assert.Equal(before + 1, Count("unmatched", "4xx"));
        Assert.DoesNotContain("include=everything", text, StringComparison.Ordinal);
        Assert.DoesNotContain(contentPostId.ToString(), text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_duration_of_a_request_is_observed()
    {
        await InvokeAsync(
            _ => { },
            path: "/api/v1/tests/slow",
            route: SlowRoute,
            statusCode: StatusCodes.Status200OK,
            delay: TimeSpan.FromMilliseconds(20));

        Assert.Equal(1, MetricsScrape.HistogramCount(
            "mayorx_http_server_request_duration_seconds",
            ("method", "GET"),
            ("status_class", "2xx"),
            ("route", SlowRoute)));
        Assert.True(
            MetricsScrape.HistogramSum(
                "mayorx_http_server_request_duration_seconds",
                ("method", "GET"),
                ("status_class", "2xx"),
                ("route", SlowRoute)) >= 0.01,
            "A request that took twenty milliseconds should not be recorded as instantaneous.");
    }

    private const string HandledRoute = "/api/v1/tests/handled";
    private const string FailedRoute = "/api/v1/tests/failed";
    private const string ThrewRoute = "/api/v1/tests/threw";
    private const string SlowRoute = "/api/v1/tests/slow";
    private const string ByIdRoute = "/api/v1/content/posts/{contentPostId}";

    private static long Count(string route, string statusClass) =>
        MetricsScrape.CounterOrZero(
            "mayorx_http_server_requests_total",
            ("method", "GET"),
            ("status_class", statusClass),
            ("route", route));

    private static async Task InvokeAsync(
        Action<HttpContext> next,
        string path,
        string? route,
        int statusCode,
        TimeSpan? delay = null)
    {
        var context = Context(path, route);

        var middleware = new HttpMetricsMiddleware(async inner =>
        {
            if (delay is not null)
            {
                await Task.Delay(delay.Value);
            }

            next(inner);
            inner.Response.StatusCode = statusCode;
        });

        await middleware.InvokeAsync(context);
    }

    private static DefaultHttpContext Context(string path, string? route)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = path;
        context.Request.Scheme = "https";
        context.Response.StatusCode = StatusCodes.Status200OK;

        if (route is not null)
        {
            context.SetEndpoint(new RouteEndpoint(
                _ => Task.CompletedTask,
                RoutePatternFactory.Parse(route),
                order: 0,
                EndpointMetadataCollection.Empty,
                displayName: null));
        }

        return context;
    }
}