namespace Api.Configuration;

/// <summary>
/// Controls the metrics scrape surface.
/// </summary>
/// <remarks>
/// The scrape endpoint is the one route in the API whose audience is an operator
/// rather than a caller, so it is configurable: a deployment that does not run a
/// scraper can remove the route and the listener behind it rather than serving an
/// endpoint nobody reads.
/// </remarks>
public sealed class MetricsOptions
{
    public const string SectionName = "Metrics";

    /// <summary>
    /// Whether <c>GET /api/v1/metrics</c> is served.
    /// </summary>
    public bool Enabled { get; set; } = true;
}