using Api.Application.AnalyticsSync;
using Api.Application.Publishing;
using Api.Domain.Enums;
using Api.Infrastructure.Metrics;
using Xunit;

namespace Api.Tests;

/// <summary>
/// A label is a promise that the set of values behind it is small. These tests hold
/// the promise where a value is turned into a label, rather than trusting every
/// caller to have passed something bounded.
/// </summary>
[Collection(ApplicationMetricsCollection.Name)]
public sealed class ApplicationMetricsLabelTests
{
    [Theory]
    [InlineData(0, "unknown")]
    [InlineData(99, "unknown")]
    [InlineData(100, "1xx")]
    [InlineData(199, "1xx")]
    [InlineData(200, "2xx")]
    [InlineData(201, "2xx")]
    [InlineData(301, "3xx")]
    [InlineData(404, "4xx")]
    [InlineData(429, "4xx")]
    [InlineData(500, "5xx")]
    [InlineData(503, "5xx")]
    [InlineData(700, "unknown")]
    public void A_status_is_reported_as_a_class(int statusCode, string expected) =>
        Assert.Equal(expected, ApplicationMetrics.StatusClass(statusCode));

    [Fact]
    public void A_status_class_never_carries_the_status_itself()
    {
        // 404 and 410 are two different answers, but one axis is what an alert
        // groups on, and there is a fixed number of them.
        Assert.Equal(ApplicationMetrics.StatusClass(404), ApplicationMetrics.StatusClass(410));
        Assert.DoesNotContain("404", ApplicationMetrics.StatusClass(404), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_route_that_never_resolved_is_reported_as_unmatched(string? routeTemplate) =>
        Assert.Equal(ApplicationMetrics.UnmatchedRoute, ApplicationMetrics.RouteLabel(routeTemplate));

    [Fact]
    public void A_route_template_is_reported_as_written() =>
        Assert.Equal(
            "/api/v1/content/posts/{contentPostId}",
            ApplicationMetrics.RouteLabel("/api/v1/content/posts/{contentPostId}"));

    [Fact]
    public void A_route_longer_than_the_limit_is_collapsed()
    {
        // The route is the only label derived from a caller-supplied request. A
        // template longer than the limit would otherwise add a series per
        // endpoint per deploy.
        Assert.Equal(
            ApplicationMetrics.OverflowLabel,
            ApplicationMetrics.RouteLabel(new string('a', ApplicationMetrics.MaxRouteLabelLength + 1)));
    }

    [Theory]
    [InlineData("PublishOutcome", "publish_outcome")]
    [InlineData("MetricsSyncFailureReason", "metrics_sync_failure_reason")]
    [InlineData("NotFound", "not_found")]
    [InlineData("IdeaGeneration", "idea_generation")]
    [InlineData("OAuthToken", "oauth_token")]
    [InlineData("AI", "ai")]
    [InlineData("ID", "id")]
    [InlineData("Already_Lower", "already_lower")]
    [InlineData("ContentPlatform", "content_platform")]
    public void An_enum_member_is_written_in_snake_case(string value, string expected) =>
        Assert.Equal(expected, ApplicationMetrics.SnakeCase(value));

    [Fact]
    public void The_run_type_label_is_the_stored_value_lowercased()
    {
        // The run type is stored in SCREAMING_SNAKE_CASE, so the label and the
        // column can be read against each other without a translation table.
        Assert.Equal(
            AIRunTypeStorage.ToStorage(AIRunType.IdeaGeneration).ToLowerInvariant(),
            ApplicationMetrics.AiRunTypeLabel(AIRunType.IdeaGeneration));
        Assert.Equal("idea_generation", ApplicationMetrics.AiRunTypeLabel(AIRunType.IdeaGeneration));
        Assert.Equal("quality_evaluation", ApplicationMetrics.AiRunTypeLabel(AIRunType.QualityEvaluation));
    }

    [Fact]
    public void A_run_type_the_stored_mapping_does_not_know_is_reported_as_other() =>
        Assert.Equal(ApplicationMetrics.OverflowLabel, ApplicationMetrics.AiRunTypeLabel((AIRunType)999));

    [Fact]
    public void Every_run_type_gets_a_lowercase_snake_case_label()
    {
        foreach (var runType in Enum.GetValues<AIRunType>())
        {
            var label = ApplicationMetrics.AiRunTypeLabel(runType);

            Assert.Equal(AIRunTypeStorage.ToStorage(runType).ToLowerInvariant(), label);
            Assert.DoesNotContain(' ', label);
            Assert.DoesNotContain('-', label);
        }
    }

    [Fact]
    public void The_platform_label_is_spelled_the_way_the_platform_spells_it()
    {
        // LinkedIn is one word to the company that owns it. linked_in would be a
        // typo in every dashboard built on this.
        Assert.Equal("linkedin", ApplicationMetrics.PlatformLabel(ContentPlatform.LinkedIn));
        Assert.Equal("x", ApplicationMetrics.PlatformLabel(ContentPlatform.X));
    }

    [Fact]
    public void A_platform_the_mapping_does_not_know_is_reported_as_other() =>
        Assert.Equal(ApplicationMetrics.OverflowLabel, ApplicationMetrics.PlatformLabel((ContentPlatform)999));

    [Fact]
    public void A_value_that_is_not_an_enum_member_is_reported_as_other()
    {
        // Writing "999" would make the label vary with the enum's numbering.
        ApplicationMetrics.RecordAnalyticsSyncAttempt(
            (MetricsSyncOutcome)999,
            MetricsSyncFailureReason.CredentialsExpired);

        Assert.Equal(
            ApplicationMetrics.OverflowLabel,
            MetricsScrape.CounterLabelValue(
                "mayorx_analytics_sync_attempts_total",
                "outcome",
                ("reason", "credentials_expired")));
    }

    [Fact]
    public void A_provider_name_that_fits_reaches_the_scrape_as_written()
    {
        // The provider name comes from configuration, so it is a fixed set of
        // values rather than caller text.
        ApplicationMetrics.RecordAiOperation(
            LabelTestProvider,
            AIRunType.ContentGeneration,
            succeeded: true,
            TimeSpan.Zero);

        Assert.Equal(
            1,
            MetricsScrape.CounterValue(
                "mayorx_ai_operations_total",
                ("provider", LabelTestProvider),
                ("run_type", "content_generation"),
                ("outcome", "succeeded")));
    }

    [Fact]
    public void A_provider_name_that_does_not_fit_is_reported_as_other()
    {
        // A configuration value long enough to be a mistake should not become a
        // series of its own.
        ApplicationMetrics.RecordAiOperation(
            new string('p', ApplicationMetrics.MaxProviderLabelLength + 1),
            AIRunType.Embedding,
            succeeded: false,
            TimeSpan.Zero);

        Assert.Equal(
            ApplicationMetrics.OverflowLabel,
            MetricsScrape.CounterLabelValue(
                "mayorx_ai_operations_total",
                "provider",
                ("run_type", "embedding"),
                ("outcome", "failed")));
    }

    [Fact]
    public void An_empty_provider_name_is_reported_as_other()
    {
        ApplicationMetrics.RecordAiOperation(
            "   ",
            AIRunType.Embedding,
            succeeded: false,
            TimeSpan.Zero);

        Assert.Equal(
            ApplicationMetrics.OverflowLabel,
            MetricsScrape.CounterLabelValue(
                "mayorx_ai_operations_total",
                "provider",
                ("run_type", "embedding"),
                ("outcome", "failed")));
    }

    [Fact]
    public void An_operation_that_did_not_fail_has_no_reason_label_value_for_a_reason()
    {
        // A missing label cannot be selected on; "none" can. The counter below is
        // read by label rather than by value so it does not matter how many other
        // tests published successfully.
        ApplicationMetrics.RecordPublishAttempt(ContentPlatform.LinkedIn, PublishOutcome.Published, reason: null);

        Assert.Equal(
            "none",
            MetricsScrape.CounterLabelValue(
                "mayorx_publish_attempts_total",
                "reason",
                ("platform", "linkedin"),
                ("outcome", "published")));
    }

    [Fact]
    public void A_publish_failure_reports_its_reason_in_snake_case()
    {
        ApplicationMetrics.RecordPublishAttempt(
            ContentPlatform.X,
            PublishOutcome.Failed,
            PublishFailureReason.ProviderUnavailable);

        Assert.Equal(
            "provider_unavailable",
            MetricsScrape.CounterLabelValue(
                "mayorx_publish_attempts_total",
                "reason",
                ("platform", "x"),
                ("outcome", "failed")));
    }

    [Fact]
    public void A_sync_failure_reports_its_reason_in_snake_case()
    {
        ApplicationMetrics.RecordAnalyticsSyncAttempt(
            MetricsSyncOutcome.Failed,
            MetricsSyncFailureReason.ProviderUnavailable);

        Assert.Equal(
            "provider_unavailable",
            MetricsScrape.CounterLabelValue(
                "mayorx_analytics_sync_attempts_total",
                "reason",
                ("outcome", "failed")));
    }

    [Fact]
    public void An_attempt_that_succeeded_reports_no_reason()
    {
        ApplicationMetrics.RecordAnalyticsSyncAttempt(MetricsSyncOutcome.Synced, reason: null);

        Assert.Equal(
            "none",
            MetricsScrape.CounterLabelValue(
                "mayorx_analytics_sync_attempts_total",
                "reason",
                ("outcome", "synced")));
    }

    private const string LabelTestProvider = "label-test-provider";
}