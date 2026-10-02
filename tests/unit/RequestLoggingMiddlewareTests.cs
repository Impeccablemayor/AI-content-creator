using System.Security.Claims;
using Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Api.Tests;

/// <summary>
/// A request log is read far more widely than the row it came from, so it holds
/// only what an incident needs and nothing that would have to be redacted later.
/// </summary>
[Collection(ApplicationMetricsCollection.Name)]
public sealed class RequestLoggingMiddlewareTests
{
    [Fact]
    public async Task A_handled_request_is_logged_once_at_information()
    {
        var logger = new CapturingLogger();

        await InvokeAsync(
            logger,
            _ => { },
            path: "/api/v1/content/posts/8f2c",
            route: ByIdRoute,
            statusCode: StatusCodes.Status200OK);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains(ByIdRoute, entry.Message, StringComparison.Ordinal);
        Assert.Contains("200", entry.Message, StringComparison.Ordinal);
        Assert.Contains("GET", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_request_that_threw_is_still_logged()
    {
        // Same reason the metric is recorded from a finally: a request that ended
        // in an exception is the one worth seeing.
        var logger = new CapturingLogger();
        var middleware = new RequestLoggingMiddleware(
            _ => throw new InvalidOperationException("handler blew up"),
            logger);

        var context = Context("/api/v1/tests/threw", ThrewRoute);

        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains(ThrewRoute, entry.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/api/v1/health")]
    [InlineData("/api/v1/health/live")]
    [InlineData("/api/v1/metrics")]
    public async Task A_probe_is_logged_below_information(string path)
    {
        // An orchestrator scraping every few seconds would otherwise bury the
        // requests a person is looking for. The count stays in the metric.
        var logger = new CapturingLogger();

        await InvokeAsync(logger, _ => { }, path: path, route: path, statusCode: StatusCodes.Status200OK);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Debug, entry.Level);
        Assert.Contains(path, entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_requested_path_is_never_logged()
    {
        var logger = new CapturingLogger();
        var contentPostId = Guid.NewGuid();

        await InvokeAsync(
            logger,
            _ => { },
            path: $"/api/v1/content/posts/{contentPostId}",
            route: ByIdRoute,
            statusCode: StatusCodes.Status200OK);

        var entry = Assert.Single(logger.Entries);
        Assert.Contains(ByIdRoute, entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(contentPostId.ToString(), entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unauthenticated_request_is_logged_without_a_caller()
    {
        // The middleware runs before authorization, so a 401 has to be loggable
        // even though there is no principal to read.
        var logger = new CapturingLogger();

        await InvokeAsync(
            logger,
            _ => { },
            path: "/api/v1/content/posts",
            route: CollectionRoute,
            statusCode: StatusCodes.Status401Unauthorized);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains("401", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_caller_and_the_workspace_are_logged_from_the_token()
    {
        var logger = new CapturingLogger();
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();

        var context = Context("/api/v1/content/posts", CollectionRoute);
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId.ToString()), new Claim("workspace_id", workspaceId.ToString())],
            authenticationType: "test"));
        context.Response.StatusCode = StatusCodes.Status200OK;

        await new RequestLoggingMiddleware(_ => Task.CompletedTask, logger).InvokeAsync(context);

        var entry = Assert.Single(logger.Entries);
        Assert.Contains(userId.ToString(), entry.Message, StringComparison.Ordinal);
        Assert.Contains(workspaceId.ToString(), entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_request_that_matches_no_endpoint_is_logged_as_unmatched()
    {
        var logger = new CapturingLogger();

        await InvokeAsync(
            logger,
            _ => { },
            path: "/api/v1/does-not-exist",
            route: null,
            statusCode: StatusCodes.Status404NotFound);

        var entry = Assert.Single(logger.Entries);
        Assert.Contains("unmatched", entry.Message, StringComparison.Ordinal);
        Assert.Contains("404", entry.Message, StringComparison.Ordinal);
    }

    private const string ByIdRoute = "/api/v1/content/posts/{contentPostId}";
    private const string CollectionRoute = "/api/v1/content/posts";
    private const string ThrewRoute = "/api/v1/tests/threw";

    private static async Task InvokeAsync(
        CapturingLogger logger,
        Action<HttpContext> next,
        string path,
        string? route,
        int statusCode)
    {
        var context = Context(path, route);
        var middleware = new RequestLoggingMiddleware(
            inner =>
            {
                next(inner);
                inner.Response.StatusCode = statusCode;

                return Task.CompletedTask;
            },
            logger);

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

    private sealed class CapturingLogger : ILogger<RequestLoggingMiddleware>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }
}