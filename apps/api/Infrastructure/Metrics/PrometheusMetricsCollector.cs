using System.Diagnostics.Metrics;
using System.Globalization;
using System.Text;

namespace Api.Infrastructure.Metrics;

/// <summary>
/// A background scan the application runs on a timer.
/// </summary>
public enum WorkerOperation
{
    /// <summary>Claiming and publishing due scheduled posts.</summary>
    ScheduledPostScan = 1,

    /// <summary>Reading metrics for published posts that are due.</summary>
    AnalyticsSyncScan = 2
}

/// <summary>
/// Collects this application's instruments and renders them in the Prometheus
/// text exposition format.
/// </summary>
/// <remarks>
/// A listener rather than a pull from the instruments themselves, because the
/// instruments hold no readable state: a counter only pushes, and a listener is
/// what makes what was pushed available to a scrape. One instance serves every
/// scrape, so its totals accumulate for the lifetime of the process and reset on
/// restart, which is what Prometheus expects of a process counter.
/// </remarks>
public sealed class PrometheusMetricsCollector : IDisposable
{
    private const char SeriesSeparator = '\u001f';

    private readonly object _gate = new();
    private readonly Dictionary<string, CounterState> _counters = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HistogramState> _histograms = new(StringComparer.Ordinal);
    private readonly Dictionary<string, double[]> _boundsByInstrument = BuildBounds();
    private readonly MeterListener _listener;

    public PrometheusMetricsCollector()
    {
        _listener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == ApplicationMetrics.MeterName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            }
        };

        // A counter measurement is the operation's own value, so it is added to
        // the running total. A histogram measurement is one observation, so each
        // one is bucketed. The instrument does not report its kind, so the value
        // type is what separates the two: every counter is a long and every
        // histogram is a double.
        _listener.SetMeasurementEventCallback<long>(
            (instrument, value, tags, _) => AddToCounter(instrument, value, tags));

        _listener.SetMeasurementEventCallback<double>(
            (instrument, value, tags, _) => Observe(instrument, value, tags));

        _listener.Start();
    }

    /// <summary>
    /// Renders everything collected so far in Prometheus text format.
    /// </summary>
    public string Render()
    {
        // The state is copied so the listener keeps recording while the response
        // is being written: a scrape must not hold up the operations it is
        // measuring.
        List<CounterState> counters;
        List<HistogramState> histograms;

        lock (_gate)
        {
            counters = [.. _counters.Values];
            histograms = [.. _histograms.Values];
        }

        var output = new StringBuilder();
        string? describedCounter = null;

        foreach (var counter in counters
            .OrderBy(counter => counter.Name, StringComparer.Ordinal)
            .ThenBy(counter => counter.Labels, StringComparer.Ordinal))
        {
            // Once per instrument rather than once per series. The format requires
            // the header to describe the family that follows it, and an instrument
            // with many label combinations is still one family.
            if (!string.Equals(describedCounter, counter.Name, StringComparison.Ordinal))
            {
                AppendHeader(output, counter.Name, "counter", counter.Description);
                describedCounter = counter.Name;
            }

            output.Append(counter.Name)
                .Append('{')
                .Append(counter.Labels)
                .Append("} ")
                .Append(Format(counter.Value))
                .Append('\n');
        }

        string? describedHistogram = null;

        foreach (var histogram in histograms
            .OrderBy(histogram => histogram.Name, StringComparer.Ordinal)
            .ThenBy(histogram => histogram.Labels, StringComparer.Ordinal))
        {
            if (!string.Equals(describedHistogram, histogram.Name, StringComparison.Ordinal))
            {
                AppendHeader(output, histogram.Name, "histogram", histogram.Description);
                describedHistogram = histogram.Name;
            }

            long cumulative = 0;
            for (var index = 0; index < histogram.Bounds.Length; index++)
            {
                cumulative += histogram.Buckets[index];

                output.Append(histogram.Name)
                    .Append("_bucket{")
                    .Append(WithLabel(histogram.Labels, "le", Format(histogram.Bounds[index])))
                    .Append("} ")
                    .Append(cumulative.ToString(CultureInfo.InvariantCulture))
                    .Append('\n');
            }

            // The overflow bucket. Prometheus requires it and reads the count from
            // it as the series total, so it is emitted even when nothing exceeded
            // the largest bound.
            output.Append(histogram.Name)
                .Append("_bucket{")
                .Append(WithLabel(histogram.Labels, "le", "+Inf"))
                .Append("} ")
                .Append(histogram.Count.ToString(CultureInfo.InvariantCulture))
                .Append('\n');

            output.Append(histogram.Name)
                .Append("_sum{")
                .Append(histogram.Labels)
                .Append("} ")
                .Append(Format(histogram.Sum))
                .Append('\n');

            output.Append(histogram.Name)
                .Append("_count{")
                .Append(histogram.Labels)
                .Append("} ")
                .Append(histogram.Count.ToString(CultureInfo.InvariantCulture))
                .Append('\n');
        }

        return output.ToString();
    }

    public void Dispose() => _listener.Dispose();

    private static Dictionary<string, double[]> BuildBounds() => new(StringComparer.Ordinal)
    {
        [ApplicationMetrics.HttpServerRequestDuration.Name] = ApplicationMetrics.HttpDurationBounds,
        [ApplicationMetrics.AiOperationDuration.Name] = ApplicationMetrics.AiDurationBounds,
        [ApplicationMetrics.ScheduledPostScanDuePosts.Name] = ApplicationMetrics.DuePostBounds
    };

    private static string Format(double value) =>
        value.ToString("0.###############", CultureInfo.InvariantCulture);

    private static void AppendHeader(StringBuilder output, string name, string type, string description)
    {
        output.Append("# HELP ")
            .Append(name)
            .Append(' ')
            .Append(description.Replace("\\", @"\\", StringComparison.Ordinal)
                .Replace("\n", @"\n", StringComparison.Ordinal))
            .Append('\n');

        output.Append("# TYPE ")
            .Append(name)
            .Append(' ')
            .Append(type)
            .Append('\n');
    }

    /// <summary>
    /// Renders a measurement's tags as a label set.
    /// </summary>
    /// <remarks>
    /// Labels are emitted in the order the instrument declared them, so a scrape
    /// reads the same way each time rather than depending on dictionary order.
    /// </remarks>
    private static string LabelsOf(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        if (tags.Length == 0)
        {
            return string.Empty;
        }

        var labels = new StringBuilder();

        foreach (var tag in tags)
        {
            if (labels.Length > 0)
            {
                labels.Append(',');
            }

            labels.Append(tag.Key)
                .Append("=\"")
                .Append(Escape(Convert.ToString(tag.Value, CultureInfo.InvariantCulture)))
                .Append('"');
        }

        return labels.ToString();
    }

    private static string Escape(string? value) =>
        (value ?? string.Empty)
            .Replace("\\", @"\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", @"\n", StringComparison.Ordinal);

    /// <summary>
    /// Adds one label to a rendered label set, including the separating comma.
    /// </summary>
    private static string WithLabel(string labels, string key, string value) =>
        labels.Length == 0
            ? $"{key}=\"{value}\""
            : $"{labels},{key}=\"{value}\"";

    private static string KeyOf(Instrument instrument, string labels) =>
        string.Concat(instrument.Name, SeriesSeparator, labels);

    /// <summary>
    /// The instrument's own description, used as the scrape's HELP line.
    /// </summary>
    /// <remarks>
    /// Taken from the instrument rather than repeated here, so the text an
    /// operator reads cannot drift from the one the code documents.
    /// </remarks>
    private static string Description(Instrument instrument) =>
        instrument.Description is { Length: > 0 } description
            ? description
            : instrument.Name;

    private void AddToCounter(
        Instrument instrument,
        long value,
        ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var labels = LabelsOf(tags);
        var key = KeyOf(instrument, labels);

        lock (_gate)
        {
            if (!_counters.TryGetValue(key, out var state))
            {
                state = new CounterState(
                    instrument.Name,
                    Description(instrument),
                    labels,
                    0);
            }

            state.Value += value;
            _counters[key] = state;
        }
    }

    private void Observe(
        Instrument instrument,
        double value,
        ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var labels = LabelsOf(tags);
        var key = KeyOf(instrument, labels);
        var bounds = _boundsByInstrument.TryGetValue(instrument.Name, out var known)
            ? known
            : ApplicationMetrics.HttpDurationBounds;

        lock (_gate)
        {
            if (!_histograms.TryGetValue(key, out var state))
            {
                state = new HistogramState(
                    instrument.Name,
                    Description(instrument),
                    labels,
                    bounds);
            }

            var index = 0;
            while (index < bounds.Length && value > bounds[index])
            {
                index++;
            }

            state.Buckets[index]++;
            state.Sum += value;
            state.Count++;
            _histograms[key] = state;
        }
    }

    private sealed class CounterState(
        string name,
        string description,
        string labels,
        long value)
    {
        public string Name { get; } = name;
        public string Description { get; } = description;
        public string Labels { get; } = labels;
        public long Value { get; set; } = value;
    }

    private sealed class HistogramState(
        string name,
        string description,
        string labels,
        double[] bounds)
    {
        public string Name { get; } = name;
        public string Description { get; } = description;
        public string Labels { get; } = labels;
        public double[] Bounds { get; } = bounds;

        /// <summary>
        /// One slot per declared bound, plus the overflow slot that stands in for
        /// <c>+Inf</c>.
        /// </summary>
        public long[] Buckets { get; } = new long[bounds.Length + 1];

        public double Sum { get; set; }
        public long Count { get; set; }
    }
}