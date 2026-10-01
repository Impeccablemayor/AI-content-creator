using System.Diagnostics;
using System.Text.Json;
using Api.Infrastructure.Health;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace Api.Tests;

public sealed class HealthResponseWriterTests
{
    [Fact]
    public async Task A_healthy_report_answers_ok()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await HealthResponseWriter.WriteAsync(context, Report(HealthStatus.Healthy));

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task An_unavailable_dependency_answers_503()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await HealthResponseWriter.WriteAsync(context, Report(HealthStatus.Unhealthy));

        // A load balancer has to be able to tell "serving" from "not serving".
        // Returning 200 while a dependency is down is the failure this replaces.
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
    }

    [Fact]
    public async Task A_degraded_report_still_answers_ok()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await HealthResponseWriter.WriteAsync(context, Report(HealthStatus.Degraded));

        // Degraded means something optional is missing, so the instance can serve.
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task The_body_keeps_the_envelope_the_previous_controller_returned()
    {
        var context = new DefaultHttpContext();
        var body = new MemoryStream();
        context.Response.Body = body;

        await HealthResponseWriter.WriteAsync(
            context,
            Report(
                HealthStatus.Unhealthy,
                new Dictionary<string, HealthReportEntry>
                {
                    ["postgres"] = Entry(HealthStatus.Unhealthy, "PostgreSQL is unavailable."),
                    ["redis"] = Entry(HealthStatus.Healthy, "Redis is healthy.")
                }));

        using var document = JsonDocument.Parse(body.ToArray());
        var data = document.RootElement.GetProperty("data");

        Assert.Equal("unhealthy", data.GetProperty("status").GetString());
        Assert.Equal(
            HealthResponseWriter.ServiceName,
            data.GetProperty("service").GetString());
        Assert.NotEqual(default, data.GetProperty("totalDurationMs").GetDouble());

        // Every dependency must be named, otherwise a 503 says nothing about why.
        var checks = data.GetProperty("checks");
        Assert.Equal("unhealthy", checks.GetProperty("postgres").GetProperty("status").GetString());
        Assert.Equal("healthy", checks.GetProperty("redis").GetProperty("status").GetString());
        Assert.Equal(
            "PostgreSQL is unavailable.",
            checks.GetProperty("postgres").GetProperty("description").GetString());
    }

    private static HealthReport Report(
        HealthStatus status,
        IReadOnlyDictionary<string, HealthReportEntry>? entries = null) =>
        // HealthReport derives its aggregate status from the worst entry, so the
        // default entry has to carry the status the test is about.
        new(
            entries ?? new Dictionary<string, HealthReportEntry>
            {
                ["postgres"] = Entry(
                    status,
                    status == HealthStatus.Healthy
                        ? "PostgreSQL is reachable."
                        : "PostgreSQL is unavailable.")
            },
            TimeSpan.FromMilliseconds(3));

    private static HealthReportEntry Entry(HealthStatus status, string description) =>
        new(
            status,
            description,
            TimeSpan.FromMilliseconds(1),
            exception: null,
            data: null);
}
