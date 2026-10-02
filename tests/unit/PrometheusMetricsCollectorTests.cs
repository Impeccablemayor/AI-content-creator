using Api.Application.AnalyticsSync;
using Api.Application.Publishing;
using Api.Application.SocialAccounts;
using Api.Domain.Enums;
using Api.Infrastructure.Metrics;
using Xunit;

namespace Api.Tests;

/// <summary>
/// The scrape endpoint is the only thing an operator has to work with, so these
/// tests pin the text a Prometheus server would receive.
/// </summary>
[Collection(ApplicationMetricsCollection.Name)]
public sealed class PrometheusMetricsCollectorTests
{
    private const string BucketRoute = "/test/bucket-shape";

    [Fact]
    public void A_counter_is_rendered_with_a_help_line_a_type_and_its_value()
    {
        ApplicationMetrics.RecordPublishAttempt(
            ContentPlatform.X,
            PublishOutcome.Skipped,
            PublishFailureReason.CredentialsExpired);

        Assert.Equal(1, MetricsScrape.CounterValue(
            "mayorx_publish_attempts_total",
            ("platform", "x"),
            ("outcome", "skipped"),
            ("reason", "credentials_expired")));

        Assert.Equal("counter", MetricsScrape.TypeOf("mayorx_publish_attempts_total"));
        Assert.Contains(
            "Scheduled publish attempts",
            MetricsScrape.HelpOf("mayorx_publish_attempts_total"),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Labels_are_written_as_the_instrument_declared_them()
    {
ApplicationMetrics.RecordPublishAttempt(
            ContentPlatform.LinkedIn,
            PublishOutcome.Published,
            reason: null);

        var series = MetricsScrape.Single(
            "mayorx_publish_attempts_total",
            ("platform", "linkedin"),
            ("outcome", "published"),
            ("reason", "none"));

        Assert.Equal(
            ["platform", "outcome", "reason"],
            series.Labels.Select(label => label.Key));
        Assert.Equal("published", series.Label("outcome"));
        Assert.Equal("none", series.Label("reason"));
    }

[Fact]
    public void A_counter_accumulates_across_the_recordings_that_made_it()
    {
        // Deltas rather than exact values: another class in this collection has
        // already run the real workers, so the series exist before this test does.
        var succeededBefore = MetricsScrape.CounterOrZero(
            "mayorx_worker_poll_runs_total",
            ("operation", "scheduled_post_scan"),
            ("outcome", "succeeded"));
        var failedBefore = MetricsScrape.CounterOrZero(
            "mayorx_worker_poll_runs_total",
            ("operation", "scheduled_post_scan"),
            ("outcome", "failed"));

        ApplicationMetrics.RecordWorkerPoll(WorkerOperation.ScheduledPostScan, succeeded: true);
        ApplicationMetrics.RecordWorkerPoll(WorkerOperation.ScheduledPostScan, succeeded: true);
        ApplicationMetrics.RecordWorkerPoll(WorkerOperation.ScheduledPostScan, succeeded: false);

        Assert.Equal(
            succeededBefore + 2,
            MetricsScrape.CounterValue(
                "mayorx_worker_poll_runs_total",
                ("operation", "scheduled_post_scan"),
                ("outcome", "succeeded")));
        Assert.Equal(
            failedBefore + 1,
            MetricsScrape.CounterValue(
                "mayorx_worker_poll_runs_total",
                ("operation", "scheduled_post_scan"),
                ("outcome", "failed")));
    }

    [Fact]
    public void A_measurement_made_before_a_listener_existed_is_not_reported()
    {
        // The reason the API resolves its collector while the host is being built
        // rather than on the first scrape. A listener only reports measurements
        // made after it started, so a collector created on the first scrape would
        // report a process that had handled nothing, and this test would go on
        // passing while the first scrape of a running API was empty.
        ApplicationMetrics.RecordPublishAttempt(
            ContentPlatform.LinkedIn,
            PublishOutcome.Published,
            reason: null);

        using var collector = new PrometheusMetricsCollector();

        Assert.DoesNotContain(
            "mayorx_publish_attempts_total",
            collector.Render(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void A_histogram_buckets_are_cumulative_and_end_with_an_infinite_bucket()
    {
        // Below every bound, inside the 0.1 to 0.25 bucket, inside the 0.5 to 1
        // bucket, and above every bound.
        RecordRequestDuration(0.002);
        RecordRequestDuration(0.125);
        RecordRequestDuration(0.75);
        RecordRequestDuration(120);

        Assert.Equal(1, MetricsScrape.HistogramBucket("mayorx_http_server_request_duration_seconds", "0.005", ("route", BucketRoute)));
        Assert.Equal(2, MetricsScrape.HistogramBucket("mayorx_http_server_request_duration_seconds", "0.25", ("route", BucketRoute)));
        Assert.Equal(3, MetricsScrape.HistogramBucket("mayorx_http_server_request_duration_seconds", "1", ("route", BucketRoute)));
        Assert.Equal(4, MetricsScrape.HistogramBucket("mayorx_http_server_request_duration_seconds", "+Inf", ("route", BucketRoute)));

        Assert.Equal(4, MetricsScrape.HistogramCount("mayorx_http_server_request_duration_seconds", ("route", BucketRoute)));
        Assert.Equal(
            120 + 0.75 + 0.125 + 0.002,
            MetricsScrape.HistogramSum("mayorx_http_server_request_duration_seconds", ("route", BucketRoute)),
            precision: 6);
    }

    [Fact]
    public void A_value_below_the_smallest_bound_still_lands_in_the_first_bucket()
    {
        // The lowest bound is one rather than zero, so an idle scan that found
        // nothing still lands in a bucket. With a bound of zero, "no due posts" and
        // "one due post" would be indistinguishable in the histogram.
        var before = MetricsScrape.HistogramBucketOrZero("mayorx_scheduled_post_scan_due_posts", "1");

        ApplicationMetrics.RecordScheduledPostScan(0);

        Assert.Equal(
            before + 1,
            MetricsScrape.HistogramBucket("mayorx_scheduled_post_scan_due_posts", "1"));
    }

    [Fact]
    public void A_histogram_without_labels_still_writes_valid_braces()
    {
        ApplicationMetrics.RecordScheduledPostScan(3);

        Assert.Contains(
            "mayorx_scheduled_post_scan_due_posts_bucket{le=\"1\"}",
            MetricsScrape.Text,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Each_instrument_is_described_once_however_many_series_it_has()
    {
        RecordEveryInstrument();

        var text = MetricsScrape.Text;

        // One HELP and one TYPE per instrument, not per series: a counter with
        // fifty label combinations is still one instrument, and a scrape that
        // repeated its header would be invalid exposition format.
        foreach (var instrument in new[]
        {
            "mayorx_http_server_requests_total",
            "mayorx_http_server_request_duration_seconds",
            "mayorx_ai_operations_total",
            "mayorx_ai_operation_duration_seconds",
            "mayorx_publish_attempts_total",
            "mayorx_analytics_sync_attempts_total",
            "mayorx_external_provider_failures_total",
            "mayorx_worker_poll_runs_total",
            "mayorx_scheduled_post_scan_due_posts"
        })
        {
            Assert.Equal(1, CountOccurrences(text, $"# HELP {instrument} "));
            Assert.Equal(1, CountOccurrences(text, $"# TYPE {instrument} "));
        }
    }

    [Fact]
    public void Every_series_is_written_with_a_name_that_can_be_queried()
    {
        // The instrument name, then the labels in a fixed order: the string a
        // dashboard would put in a PromQL selector.
        ApplicationMetrics.RecordProviderFailure(
            ContentPlatform.LinkedIn,
            SocialProviderOperation.Publish,
            SocialProviderFailureClass.Unavailable);

        Assert.Contains(
            "mayorx_external_provider_failures_total{platform=\"linkedin\",operation=\"publish\",failure_class=\"unavailable\"}",
            MetricsScrape.Text,
            StringComparison.Ordinal);
    }

    [Fact]
    public void A_label_value_that_would_break_the_format_is_escaped()
    {
        // A route template reaches the renderer from the routing table, and a
        // literal quote or newline in a label would otherwise end the line and
        // make the rest of the scrape a parse error rather than a metric.
        ApplicationMetrics.RecordHttpRequest(
            "GET",
            200,
            "/test/quote\"and{brace}",
            0.01);

        Assert.Contains(
            "mayorx_http_server_requests_total{method=\"GET\",status_class=\"2xx\",route=\"/test/quote\\\"and{brace}\"}",
            MetricsScrape.Text,
            StringComparison.Ordinal);
    }

    private static void RecordRequestDuration(double seconds) =>
        ApplicationMetrics.RecordHttpRequest("POST", 200, BucketRoute, seconds);

    /// <summary>
    /// Records one measurement on every instrument the application declares.
    /// </summary>
    private static void RecordEveryInstrument()
    {
        ApplicationMetrics.RecordHttpRequest("PUT", 204, "/test/inventory", 0.2);
        ApplicationMetrics.RecordHttpRequest("DELETE", 503, "/test/inventory", 0.3);
        ApplicationMetrics.RecordAiOperation("openai", AIRunType.ContentGeneration, true, TimeSpan.FromSeconds(3));
        ApplicationMetrics.RecordPublishAttempt(
            ContentPlatform.X,
            PublishOutcome.Published,
            reason: null);
        ApplicationMetrics.RecordAnalyticsSyncAttempt(MetricsSyncOutcome.Synced, reason: null);
        ApplicationMetrics.RecordProviderFailure(
            ContentPlatform.X,
            SocialProviderOperation.Metrics,
            SocialProviderFailureClass.Unavailable);
        ApplicationMetrics.RecordWorkerPoll(WorkerOperation.AnalyticsSyncScan, succeeded: true);
        ApplicationMetrics.RecordScheduledPostScan(7);
    }

    private static string Labels(MetricSample sample) =>
        string.Join(",", sample.Labels.Select(label => $"{label.Key}=\"{label.Value}\""));

    private static int CountOccurrences(string text, string value) =>
        text.Split([value], StringSplitOptions.None).Length - 1;
}