using System.Diagnostics;
using Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests;

public sealed class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task A_request_without_an_id_gets_one_and_it_reaches_the_response()
    {
        var context = await InvokeAsync(requestId: null);

        var correlationId = context.Response.Headers[CorrelationIdMiddleware.HeaderName]
            .ToString();

        Assert.False(string.IsNullOrWhiteSpace(correlationId));
    }

    [Fact]
    public async Task A_supplied_id_is_reused_so_upstream_logs_stay_joinable()
    {
        var context = await InvokeAsync(requestId: "edge-request-42");

        Assert.Equal(
            "edge-request-42",
            context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString());
    }

    [Theory]
    [InlineData("has spaces")]
    [InlineData("has\nnewline")]
    [InlineData("quote\"and{brace")]
    public async Task An_unusable_id_is_replaced_rather_than_trusted(string requestId)
    {
        // A caller-supplied id lands in a response header and in log output, so an
        // unvalidated one would let a caller forge a log line or inject a header.
        var context = await InvokeAsync(requestId);

        var correlationId = context.Response.Headers[CorrelationIdMiddleware.HeaderName]
            .ToString();

        Assert.NotEqual(requestId, correlationId);
        Assert.False(string.IsNullOrWhiteSpace(correlationId));
    }

    [Fact]
    public async Task The_trace_identifier_agrees_with_the_correlation_id()
    {
        // GlobalExceptionMiddleware reports HttpContext.TraceIdentifier, so the two
        // have to be the same value or a failure cites an id the request was not
        // logged under.
        var context = await InvokeAsync(requestId: null);

        Assert.Equal(
            context.TraceIdentifier,
            context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString());
    }

    [Fact]
    public async Task The_ambient_activity_is_tagged_and_reused_rather_than_forked()
    {
        using var activity = new Activity("test-request").Start();

        var context = await InvokeAsync(requestId: null);

        // Reusing the ambient trace id is what keeps this middleware from splitting
        // the existing trace into a second one.
        Assert.Equal(
            activity.TraceId.ToString(),
            context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString());
        Assert.Equal(
            activity.TraceId.ToString(),
            activity.GetTagItem(CorrelationIdMiddleware.HeaderName));
        Assert.Equal("test-request", activity.OperationName);
    }

    [Fact]
    public async Task The_header_is_present_even_when_the_pipeline_short_circuits()
    {
        var context = new DefaultHttpContext();

        var middleware = new CorrelationIdMiddleware(
            _ => Task.CompletedTask,
            NullLogger<CorrelationIdMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        // The id is set before the next stage runs, so a stage that ends the
        // request without writing a body still produces a correlatable response.
        Assert.NotEmpty(
            context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString());
    }

    private static async Task<DefaultHttpContext> InvokeAsync(string? requestId)
    {
        var context = new DefaultHttpContext();

        if (requestId is not null)
        {
            context.Request.Headers[CorrelationIdMiddleware.HeaderName] = requestId;
        }

        var reachedPipeline = new TaskCompletionSource();

        var middleware = new CorrelationIdMiddleware(
            _ =>
            {
                reachedPipeline.TrySetResult();
                return Task.CompletedTask;
            },
            NullLogger<CorrelationIdMiddleware>.Instance);

        await middleware.InvokeAsync(context);
        await reachedPipeline.Task;

        return context;
    }
}
