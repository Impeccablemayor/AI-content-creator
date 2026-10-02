namespace Api.Infrastructure.Metrics;

/// <summary>
/// The <c>GET /api/v1/metrics</c> scrape endpoint.
/// </summary>
/// <remarks>
/// Anonymous by design: a scraper is not a workspace caller and has no JWT. It
/// serves only the instruments this process declares, which are labelled with
/// route templates, enums and configuration, so there is nothing in the output
/// that identifies a workspace, a content post or a social account.
/// </remarks>
public static class MetricsScrapeEndpoint
{
    public const string Route = "/api/v1/metrics";

    /// <summary>
    /// The content type Prometheus expects from a text exposition scrape.
    /// </summary>
    public const string ContentType = "text/plain; version=0.0.4; charset=utf-8";

    public static async Task WriteAsync(
        HttpContext context,
        PrometheusMetricsCollector collector)
    {
        context.Response.ContentType = ContentType;

        await context.Response.WriteAsync(collector.Render(), context.RequestAborted);
    }
}