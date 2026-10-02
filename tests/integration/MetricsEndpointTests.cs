using System.Globalization;
using System.Net;
using System.Text.Json;
using Xunit;

namespace Api.IntegrationTests;

/// <summary>
/// Exercises the scrape endpoint the way a scraper uses it: no token, the
/// Prometheus content type, and a body a Prometheus server would accept.
/// </summary>
/// <remarks>
/// The assertions read the response rather than the collector. The instruments are
/// process-wide, and the integration suite runs several hosts in one process, so a
/// count read from the collector would not be this host's count. The response is the
/// only surface a scraper actually sees.
/// </remarks>
public sealed class MetricsEndpointTests : IClassFixture<MayorxApiFactory>, IAsyncLifetime
{
    /// <summary>
    /// The label keys this application is allowed to write. Anything else would be
    /// a dimension nobody reviewed, and dimensions are what make a metric expensive.
    /// </summary>
    private static readonly string[] AllowedLabelKeys =
    [
        "failure_class",
        "le",
        "method",
        "operation",
        "outcome",
        "platform",
        "provider",
        "reason",
        "route",
        "run_type",
        "status_class"
    ];

    private readonly MayorxApiFactory _factory;

    private HttpClient _client = null!;

    public MetricsEndpointTests(MayorxApiFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        _factory.Initialise();
        await _factory.MigrateAsync();

        _client = _factory.CreateClient();

        // One throwaway scrape so the host has a measurement to report. A scrape is
        // rendered before the middleware records it, so the first one after startup
        // is legitimately empty and says nothing about the endpoint.
        using var warmUp = await _client.GetAsync("api/v1/metrics");
    }

    public Task DisposeAsync()
    {
        _client?.Dispose();

        return Task.CompletedTask;
    }

    [Fact]
    public async Task The_scrape_is_served_without_a_token()
    {
        // A scraper is not a workspace caller and has no JWT to present. Requiring
        // one would mean the endpoint was never scraped.
        using var response = await _client.GetAsync("api/v1/metrics");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task The_scrape_uses_the_text_exposition_content_type()
    {
        using var response = await _client.GetAsync("api/v1/metrics");

        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("0.0.4", response.Content.Headers.ContentType?.Parameters
            .Single(parameter => parameter.Name == "version").Value);
        Assert.Equal("utf-8", response.Content.Headers.ContentType?.CharSet);
    }

    [Fact]
    public async Task The_scrape_reports_the_request_that_made_it()
    {
        // One scrape to take the measurement of the first, because the first cannot
        // report itself: it is rendered before the middleware records it.
        var before = await ScrapeAsync();

        using var response = await _client.GetAsync("api/v1/metrics");
        var after = await response.Content.ReadAsStringAsync();

        Assert.Equal(
            Value(before, "mayorx_http_server_requests_total") + 1,
            Value(after, "mayorx_http_server_requests_total"));
        Assert.Contains("route=\"/api/v1/metrics\"", after, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_duration_of_the_scrapes_is_reported_as_a_histogram()
    {
        using var response = await _client.GetAsync("api/v1/metrics");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("# TYPE mayorx_http_server_request_duration_seconds histogram", body, StringComparison.Ordinal);
        Assert.Contains("mayorx_http_server_request_duration_seconds_bucket{", body, StringComparison.Ordinal);
        Assert.Contains("mayorx_http_server_request_duration_seconds_count{", body, StringComparison.Ordinal);
        Assert.Contains("mayorx_http_server_request_duration_seconds_sum{", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Every_line_is_something_a_prometheus_server_can_parse()
    {
        using var response = await _client.GetAsync("api/v1/metrics");
        var body = await response.Content.ReadAsStringAsync();

        var lines = body.Split('\n').Select(line => line.TrimEnd('\r')).Where(line => line.Length > 0);

        Assert.NotEmpty(lines);

        foreach (var line in lines)
        {
            if (line[0] == '#')
            {
                Assert.True(
                    line.StartsWith("# HELP ", StringComparison.Ordinal) ||
                    line.StartsWith("# TYPE ", StringComparison.Ordinal),
                    $"An unrecognised comment line would be dropped or rejected: {line}");

                continue;
            }

            var separator = line.LastIndexOf(' ');

            Assert.True(separator > 0, $"A sample line needs a value: {line}");
            Assert.True(
                double.TryParse(
                    line.AsSpan(separator + 1),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out _),
                $"A sample line needs a numeric value: {line}");
            Assert.StartsWith("mayorx_", line, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Every_label_is_one_this_application_declared()
    {
        using var response = await _client.GetAsync("api/v1/metrics");
        var body = await response.Content.ReadAsStringAsync();

        var keys = body
            .Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .Where(line => line.Length > 0 && line[0] != '#')
            .SelectMany(LabelKeys)
            .Distinct()
            .Order()
            .ToArray();

        Assert.NotEmpty(keys);

        foreach (var key in keys)
        {
            Assert.Contains(key, AllowedLabelKeys);
        }
    }

    [Fact]
    public async Task The_scrape_never_carries_an_identifier_from_the_request()
    {
        // A path can hold a content post id, an account id or anything else a
        // caller put there. The routing table has a fixed number of templates, and
        // only those are reported.
        var contentPostId = Guid.NewGuid();

        using var notFound = await _client.GetAsync($"api/v1/content/posts/{contentPostId}");
        Assert.Equal(HttpStatusCode.NotFound, notFound.StatusCode);

        using var response = await _client.GetAsync("api/v1/metrics");
        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain(contentPostId.ToString(), body, StringComparison.Ordinal);
        Assert.DoesNotContain("/api/v1/content/posts/", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task No_label_value_is_longer_than_a_route_template_could_be()
    {
        // A bound, not a formatting rule: a label longer than this would mean some
        // caller-supplied string had reached the scrape.
        using var response = await _client.GetAsync("api/v1/metrics");
        var body = await response.Content.ReadAsStringAsync();

        foreach (var value in LabelValues(body))
        {
            Assert.True(value.Length <= 128, $"A label value is {value.Length} characters long: {value}");
        }
    }

    [Fact]
    public async Task Every_route_label_is_a_route_this_api_serves_or_the_word_unmatched()
    {
        // Every route in the routing table starts with the API prefix. A value that
        // did not would have been built from the request rather than the table.
        using var response = await _client.GetAsync("api/v1/metrics");
        var body = await response.Content.ReadAsStringAsync();

        var routes = LabelsWithKey(body, "route").Select(label => label.Value).Distinct().ToArray();

        Assert.NotEmpty(routes);

        foreach (var route in routes)
        {
            Assert.True(
                route == "unmatched" || route.StartsWith("/api/v1/", StringComparison.Ordinal),
                $"A route label that is neither a served route nor unmatched would come from a request: {route}");
        }
    }

    [Fact]
    public async Task Disabling_the_endpoint_removes_it_and_leaves_the_api_serving()
    {
        using var factory = MayorxApiFactory.WithSettings(
            new Dictionary<string, string?>
            {
                ["Metrics:Enabled"] = "false"
            });

        factory.Initialise();
        await factory.MigrateAsync();

        using var client = factory.CreateClient();

        using var metrics = await client.GetAsync("api/v1/metrics");
        Assert.Equal(HttpStatusCode.NotFound, metrics.StatusCode);

        // Off means the endpoint is absent, not that the API is broken.
        using var health = await client.GetAsync("api/v1/health/live");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }

    [Fact]
    public async Task The_scrape_is_not_described_in_the_openapi_document()
    {
        // An advertised metrics route invites a scraper with credentials, and an
        // operator finding it in the documentation expects a secured resource.
        using var factory = MayorxApiFactory.WithSettings(null);

        factory.UseEnvironmentFor("Development");
        factory.Initialise();

        await factory.MigrateAsync();

        using var response = await factory.CreateClient()
            .GetAsync("swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.False(
            document.RootElement.GetProperty("paths").TryGetProperty("/api/v1/metrics", out _),
            "The scrape endpoint is anonymous and is not part of the documented API.");
    }

    private async Task<string> ScrapeAsync()
    {
        using var response = await _client.GetAsync("api/v1/metrics");

        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// The sum of one counter's series, so a delta can be taken when another test in
    /// this process has recorded the same counter.
    /// </summary>
    private static double Value(string body, string name) =>
        body.Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .Where(line => line.StartsWith(name, StringComparison.Ordinal) &&
                !line.StartsWith(name + "_", StringComparison.Ordinal))
            .Select(line => line[(line.LastIndexOf(' ') + 1)..])
            .Where(value => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            .Sum(value => double.Parse(value, CultureInfo.InvariantCulture));

    private static IEnumerable<string> LabelKeys(string sampleLine) =>
        Labels(sampleLine).Select(label => label.Key);

    private static IReadOnlyList<KeyValuePair<string, string>> Labels(string line)
    {
        var labels = new List<KeyValuePair<string, string>>();

        var open = line.IndexOf('{');

        // The closing brace is the last one before the value, because a route
        // template ends in one itself: /api/v1/content/posts/{contentPostId}.
        var close = line.LastIndexOf('}', line.LastIndexOf(' '));

        if (open < 0 || close < open)
        {
            return labels;
        }

        foreach (var pair in line[(open + 1)..close].Split(','))
        {
            var equals = pair.IndexOf('=');

            if (equals < 0)
            {
                continue;
            }

            labels.Add(new KeyValuePair<string, string>(
                pair[..equals],
                pair[(equals + 2)..^1].Replace("\\\"", "\"", StringComparison.Ordinal)));
        }

        return labels;
    }

    private static IEnumerable<KeyValuePair<string, string>> LabelsWithKey(string body, string key) =>
        body.Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .Where(line => line.Length > 0 && line[0] != '#')
            .SelectMany(line => Labels(line))
            .Where(label => label.Key == key);

    private static IEnumerable<string> LabelValues(string body) =>
        body.Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .Where(line => line.Length > 0 && line[0] != '#')
            .SelectMany(line => Labels(line))
            .Select(label => label.Value);
}