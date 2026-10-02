using System.Diagnostics.Metrics;
using System.Text;
using Api.Application.AnalyticsSync;
using Api.Application.Publishing;
using Api.Application.SocialAccounts;
using Api.Domain.Enums;

namespace Api.Infrastructure.Metrics;

/// <summary>
    /// The application's instruments, and the only supported way to record them.
    /// </summary>
    /// <remarks>
    /// Every label written here is bounded by construction: an enum, a deployment
    /// setting, or an endpoint route template taken from the routing table. There is
    /// no parameter that would accept a workspace id, a content post id, a path or an
    /// exception message, so a tenant identifier cannot reach the scrape output by
    /// accident.
    /// <para>
    /// Counters are <see cref="Counter{T}"/> of <see cref="long"/> and histograms are
    /// <see cref="Histogram{T}"/> of <see cref="double"/>. A listener is handed a
    /// measurement and the instrument, and the instrument does not say which kind it
    /// is, so the value type is what distinguishes them.
    /// </para>
    /// </remarks>
public static class ApplicationMetrics
{
    public const string MeterName = "Mayorx.ContentEngine";

    /// <summary>
    /// Route label used when a request matched no endpoint, or no endpoint was
    /// resolved because the request never reached one.
    /// </summary>
    public const string UnmatchedRoute = "unmatched";

    /// <summary>
    /// Label used instead of a value that is longer than the label allows.
    /// </summary>
    /// <remarks>
    /// Nothing recorded today should reach this. It exists because a route
    /// template is the one label derived from a caller-supplied request, and an
    /// unbounded label is a cardinality problem rather than a tidy one.
    /// </remarks>
    internal const string OverflowLabel = "other";

    internal const int MaxRouteLabelLength = 128;
    internal const int MaxProviderLabelLength = 32;

    // Bucketed for a service whose response time is a few milliseconds to a few
    // seconds. Both sets are declared here and read by the renderer, so the
    // buckets an operator sees and the buckets the instrument was created with
    // cannot drift apart.
    internal static readonly double[] HttpDurationBounds =
        [0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10, 30];

    internal static readonly double[] AiDurationBounds =
        [0.5, 1, 2.5, 5, 10, 30, 60, 120];

    internal static readonly double[] DuePostBounds =
        [1, 2, 5, 10, 25, 50, 100];

    private static readonly Meter Meter = new(MeterName, "1.0.0");

    private static readonly KeyValuePair<string, object?>[] HttpLabels =
        [Label("method"), Label("status_class"), Label("route")];

    private static readonly KeyValuePair<string, object?>[] AiLabels =
        [Label("provider"), Label("run_type"), Label("outcome")];

    private static readonly KeyValuePair<string, object?>[] AiDurationLabels =
        [Label("provider"), Label("run_type")];

    private static readonly KeyValuePair<string, object?>[] PublishLabels =
        [Label("platform"), Label("outcome"), Label("reason")];

    private static readonly KeyValuePair<string, object?>[] AnalyticsSyncLabels =
        [Label("outcome"), Label("reason")];

    private static readonly KeyValuePair<string, object?>[] ProviderFailureLabels =
        [Label("platform"), Label("operation"), Label("failure_class")];

    private static readonly KeyValuePair<string, object?>[] WorkerPollLabels =
        [Label("operation"), Label("outcome")];

    /// <summary>HTTP requests handled, by method, status class and route.</summary>
    public static readonly Counter<long> HttpServerRequests = Meter.CreateCounter<long>(
        "mayorx_http_server_requests_total",
        "{request}",
        "HTTP requests handled by the API.",
        HttpLabels);

    /// <summary>Time spent handling an HTTP request.</summary>
    public static readonly Histogram<double> HttpServerRequestDuration = Meter.CreateHistogram<double>(
        "mayorx_http_server_request_duration_seconds",
        "s",
        "Time spent handling an HTTP request, in seconds.",
        HttpLabels,
        new InstrumentAdvice<double> { HistogramBucketBoundaries = HttpDurationBounds });

    /// <summary>AI operations attempted, by provider, run type and outcome.</summary>
    /// <remarks>
    /// Token counts and cost are deliberately absent: the provider interface
    /// returns generated text only, so there is no measured usage to report.
    /// </remarks>
    public static readonly Counter<long> AiOperations = Meter.CreateCounter<long>(
        "mayorx_ai_operations_total",
        "{operation}",
        "AI operations attempted, whether they succeeded or failed.",
        AiLabels);

    /// <summary>Time an AI operation took.</summary>
    public static readonly Histogram<double> AiOperationDuration = Meter.CreateHistogram<double>(
        "mayorx_ai_operation_duration_seconds",
        "s",
        "Time an AI operation took, in seconds.",
        AiDurationLabels,
        new InstrumentAdvice<double> { HistogramBucketBoundaries = AiDurationBounds });

    /// <summary>Publish attempts, by platform and how they ended.</summary>
    public static readonly Counter<long> PublishAttempts = Meter.CreateCounter<long>(
        "mayorx_publish_attempts_total",
        "{attempt}",
        "Scheduled publish attempts, by platform and outcome.",
        PublishLabels);

    /// <summary>Analytics sync attempts, by how they ended.</summary>
    public static readonly Counter<long> AnalyticsSyncAttempts = Meter.CreateCounter<long>(
        "mayorx_analytics_sync_attempts_total",
        "{attempt}",
        "Post metrics read attempts, by outcome.",
        AnalyticsSyncLabels);

    /// <summary>Social platform HTTP failures, classified.</summary>
    public static readonly Counter<long> ExternalProviderFailures = Meter.CreateCounter<long>(
        "mayorx_external_provider_failures_total",
        "{failure}",
        "Social platform requests that failed, by platform, operation and class.",
        ProviderFailureLabels);

    /// <summary>Background poll runs, by whether the poll completed.</summary>
    public static readonly Counter<long> WorkerPollRuns = Meter.CreateCounter<long>(
        "mayorx_worker_poll_runs_total",
        "{run}",
        "Background scan runs, by operation and whether the run completed.",
        WorkerPollLabels);

    /// <summary>Due posts found per scheduled post scan.</summary>
    /// <remarks>
    /// Recorded on a <see cref="Histogram{T}"/> of doubles even though the value is
    /// a count, because the collector dispatches a long measurement to a counter
    /// and a double measurement to a histogram. The BCL's Instrument does not say
    /// which kind it is, so the two kinds are kept distinguishable by their value
    /// type.
    /// </remarks>
    public static readonly Histogram<double> ScheduledPostScanDuePosts = Meter.CreateHistogram<double>(
        "mayorx_scheduled_post_scan_due_posts",
        "{post}",
        "Due content posts found by one scheduled post scan.",
        [],
        new InstrumentAdvice<double> { HistogramBucketBoundaries = DuePostBounds });

    /// <summary>
    /// Records one handled HTTP request.
    /// </summary>
    /// <param name="method">HTTP method. A fixed verb set, never caller text.</param>
    /// <param name="statusCode">The response status, grouped into a class.</param>
    /// <param name="routeTemplate">
    /// The route pattern the request matched, never the requested path.
    /// </param>
    /// <param name="duration">Time from entering the pipeline to the response.</param>
    public static void RecordHttpRequest(
        string method,
        int statusCode,
        string? routeTemplate,
        double durationSeconds)
    {
        var labels = new KeyValuePair<string, object?>[]
        {
            new("method", Bounded(method) ?? "other"),
            new("status_class", StatusClass(statusCode)),
            new("route", RouteLabel(routeTemplate))
        };

        HttpServerRequests.Add(1, labels);
        HttpServerRequestDuration.Record(durationSeconds, labels);
    }

    /// <summary>
    /// Records one AI operation and how long it took.
    /// </summary>
    public static void RecordAiOperation(
        string provider,
        AIRunType runType,
        bool succeeded,
        TimeSpan duration)
    {
        var providerLabel = Bounded(provider, MaxProviderLabelLength) ?? OverflowLabel;
        var runTypeLabel = AiRunTypeLabel(runType);

        AiOperations.Add(
            1,
            new KeyValuePair<string, object?>[]
            {
                new("provider", providerLabel),
                new("run_type", runTypeLabel),
                new("outcome", succeeded ? "succeeded" : "failed")
            });

        AiOperationDuration.Record(
            duration.TotalSeconds,
            new KeyValuePair<string, object?>[]
            {
                new("provider", providerLabel),
                new("run_type", runTypeLabel)
            });
    }

    /// <summary>
    /// Records one scheduled publish attempt.
    /// </summary>
    public static void RecordPublishAttempt(
        ContentPlatform platform,
        PublishOutcome outcome,
        PublishFailureReason? reason)
    {
        PublishAttempts.Add(
            1,
            new KeyValuePair<string, object?>[]
            {
                new("platform", PlatformLabel(platform)),
                new("outcome", Label(outcome)),
                new("reason", reason is null ? "none" : Label(reason.Value))
            });
    }

    /// <summary>
    /// Records one post metrics read attempt.
    /// </summary>
    public static void RecordAnalyticsSyncAttempt(
        MetricsSyncOutcome outcome,
        MetricsSyncFailureReason? reason)
    {
        AnalyticsSyncAttempts.Add(
            1,
            new KeyValuePair<string, object?>[]
            {
                new("outcome", Label(outcome)),
                new("reason", reason is null ? "none" : Label(reason.Value))
            });
    }

    /// <summary>
    /// Records one failed social platform request.
    /// </summary>
    /// <remarks>
    /// Called from the place that classifies a provider failure, which is what
    /// makes the count exact: a failure cannot be reported by one path and missed
    /// by another, and no message or response body is available to leak.
    /// </remarks>
    public static void RecordProviderFailure(
        ContentPlatform platform,
        SocialProviderOperation operation,
        SocialProviderFailureClass failureClass)
    {
        ExternalProviderFailures.Add(
            1,
            new KeyValuePair<string, object?>[]
            {
                new("platform", PlatformLabel(platform)),
                new("operation", Label(operation)),
                new("failure_class", Label(failureClass))
            });
    }

    /// <summary>
    /// Records one background scan run.
    /// </summary>
    public static void RecordWorkerPoll(WorkerOperation operation, bool succeeded)
    {
        WorkerPollRuns.Add(
            1,
            new KeyValuePair<string, object?>[]
            {
                new("operation", Label(operation)),
                new("outcome", succeeded ? "succeeded" : "failed")
            });
    }

    /// <summary>
    /// Records how many due posts one scheduled post scan found.
    /// </summary>
    public static void RecordScheduledPostScan(int duePosts) =>
        ScheduledPostScanDuePosts.Record(duePosts);

    internal static string StatusClass(int statusCode) =>
        statusCode switch
        {
            < 100 => "unknown",
            < 200 => "1xx",
            < 300 => "2xx",
            < 400 => "3xx",
            < 500 => "4xx",
            < 600 => "5xx",
            _ => "unknown"
        };

    internal static string RouteLabel(string? routeTemplate) =>
        string.IsNullOrWhiteSpace(routeTemplate)
            ? UnmatchedRoute
            : Bounded(routeTemplate, MaxRouteLabelLength) ?? OverflowLabel;

    internal static string AiRunTypeLabel(AIRunType runType) =>
        Enum.IsDefined(runType)
            ? AIRunTypeStorage.ToStorage(runType).ToLowerInvariant()
            : OverflowLabel;

    /// <summary>
    /// Writes an enum member as a snake_case label value.
    /// </summary>
    /// <remarks>
    /// Prometheus queries read better with underscores, and a dash-separated
    /// value would have to be quoted. Groups of capitals stay together so
    /// <c>OAuthToken</c> becomes <c>oauth_token</c> rather than
    /// <c>o_auth_token</c>.
    /// <para>
    /// A value that is not a member of the enum has no name to write. The number
    /// is reported instead of the name only because that is what
    /// <see cref="Enum.ToString()"/> gives, and a label that varies with the
    /// enum's numbering is worse than one that says nothing useful.
    /// </para>
    /// </remarks>
    private static string Label<T>(T value) where T : struct, Enum =>
        Enum.IsDefined(value) ? SnakeCase(value.ToString()) : OverflowLabel;

    internal static string SnakeCase(string value)
    {
        var snake = new StringBuilder(value.Length + 4);

        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];

            if (char.IsUpper(character) && index > 0 && NeedsSeparator(value, index))
            {
                snake.Append('_');
            }

            snake.Append(char.ToLowerInvariant(character));
        }

        return snake.ToString();
    }

    private static bool NeedsSeparator(string value, int index)
    {
        var previous = value[index - 1];

        // Something already separates the two words.
        if (!char.IsLetterOrDigit(previous))
        {
            return false;
        }

        // A lower case letter or a digit ends the previous word.
        if (!char.IsUpper(previous))
        {
            return true;
        }

        // Both letters are capitals, so this is either the end of a run of capitals
        // or the start of a word that follows one. A run belongs together, so the
        // boundary goes after it rather than inside it: HTTPServer is
        // http_server, and OAuthToken is oauth_token rather than o_auth_token
        // because the run was the first thing in the value.
        var runStartsTheValue = index == 1 && char.IsUpper(value[0]);
        var runEndsHere = index + 1 >= value.Length || !char.IsLower(value[index + 1]);

        return !runEndsHere && !runStartsTheValue;
    }

    /// <summary>
    /// The platform label, which is spelled the way the platforms spell their own
    /// names rather than by the rule above.
    /// </summary>
    /// <remarks>
    /// <c>LinkedIn</c> is one word to the company that owns it, and turning it
    /// into <c>linked_in</c> would be a typo in every dashboard built on it.
    /// </remarks>
    internal static string PlatformLabel(ContentPlatform platform) =>
        platform switch
        {
            ContentPlatform.LinkedIn => "linkedin",
            ContentPlatform.X => "x",
            _ => OverflowLabel
        };

    private static string? Bounded(string? value, int maxLength = 64) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Length <= maxLength ? value : null;

    private static KeyValuePair<string, object?> Label(string name) =>
        new(name, null);
}