using System.Globalization;
using System.Text;
using Api.Infrastructure.Metrics;
using Xunit;

namespace Api.Tests;

/// <summary>
/// One scraped series.
/// </summary>
internal sealed record MetricSample(
    string Name,
    IReadOnlyList<KeyValuePair<string, string>> Labels,
    double Value)
{
    public bool Has(string key, string value) =>
        Labels.Any(label =>
            label.Key == key && string.Equals(label.Value, value, StringComparison.Ordinal));

    public string? Label(string key) =>
        Labels.FirstOrDefault(label => label.Key == key).Value;
}

/// <summary>
/// Reads the application's instruments the same way a scraper does: through the
/// exposition text, never through the instrument objects.
/// </summary>
/// <remarks>
/// The instruments are process-wide static state, so a test that read them
/// directly would depend on which other tests ran first. Every assertion here goes
/// through a listener and a parser, which means a test also proves the value a
/// Prometheus server would actually receive.
/// <para>
/// The collector is created when the test assembly loads, before any test records
/// anything. That is not tidiness: a listener only sees measurements made after it
/// starts, so a collector created on the first read would have missed every
/// recording the test had just made. It is also exactly what the API has to do,
/// which is why Program.cs resolves the collector at startup.
/// </para>
/// </remarks>
internal static class MetricsScrape
{
    private static readonly PrometheusMetricsCollector Collector = new();

    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void CreateCollectorBeforeAnyTestRuns() => _ = Collector;

    public static string Text => Collector.Render();

    public static long CounterValue(string name, params (string Key, string Value)[] labels) =>
        (long)Single(name, labels).Value;

    /// <summary>
    /// The value of a counter series, or zero when nothing has recorded it yet.
    /// </summary>
    /// <remarks>
    /// For a before-and-after assertion. A series that does not exist and a series
    /// at zero are the same thing to a reader, so a delta assertion starts from
    /// zero rather than failing on a series that has not been created.
    /// </remarks>
    public static long CounterOrZero(string name, params (string Key, string Value)[] labels)
    {
        var matches = Parse(Text)
            .Where(sample => Matches(sample, name, labels))
            .ToArray();

        return matches.Length == 0 ? 0 : (long)matches.Sum(sample => sample.Value);
    }

    public static long HistogramBucketOrZero(
        string name,
        string lessThanOrEqual,
        params (string Key, string Value)[] labels) =>
        Parse(Text)
            .Where(sample => Matches(sample, name + "_bucket", [.. labels, ("le", lessThanOrEqual)]))
            .Sum(sample => (long)sample.Value);

    public static long HistogramCount(string name, params (string Key, string Value)[] labels) =>
        (long)Single(name + "_count", labels).Value;

    /// <summary>
    /// The number of observations of a histogram series, or zero when nothing has
    /// recorded it yet.
    /// </summary>
    public static long HistogramCountOrZero(string name, params (string Key, string Value)[] labels) =>
        Parse(Text)
            .Where(sample => Matches(sample, name + "_count", labels))
            .Sum(sample => (long)sample.Value);

    public static double HistogramSum(string name, params (string Key, string Value)[] labels) =>
        Single(name + "_sum", labels).Value;

    public static long HistogramBucket(
        string name,
        string lessThanOrEqual,
        params (string Key, string Value)[] labels) =>
        (long)Single(
            name + "_bucket",
            [.. labels, ("le", lessThanOrEqual)]).Value;

    /// <summary>
    /// The value of one label of the single series matching the others.
    /// </summary>
    /// <remarks>
    /// For asserting how a label was spelled. The remaining labels have to identify
    /// one series exactly, which is why a caller reaching for this passes a label
    /// value chosen to be unique rather than one another test also records.
    /// </remarks>
    public static string CounterLabelValue(
        string name,
        string label,
        params (string Key, string Value)[] labels)
    {
        var value = Single(name, labels).Label(label);

        Assert.False(
            string.IsNullOrEmpty(value),
            $"The series matched but carried no {label} label.");

        return value!;
    }

    public static bool HasSeries(string name, params (string Key, string Value)[] labels) =>
        Parse(Text).Any(sample => Matches(sample, name, labels));

    /// <summary>
    /// Every series of one name, in the order the scrape reported them.
    /// </summary>
    public static IReadOnlyList<MetricSample> Series(string name) =>
        [.. Parse(Text).Where(sample => sample.Name == name)];

    /// <summary>
    /// The TYPE line the scrape declared for an instrument.
    /// </summary>
    public static string TypeOf(string name)
    {
        var line = Line($"# TYPE {name} ")
            ?? throw new Xunit.Sdk.XunitException(
                $"No TYPE line for {name} in:{Environment.NewLine}{Text}");

        return line[($"# TYPE {name} ").Length..];
    }

    /// <summary>
    /// The text of an instrument's HELP line.
    /// </summary>
    public static string HelpOf(string name)
    {
        var line = Line($"# HELP {name} ")
            ?? throw new Xunit.Sdk.XunitException(
                $"No HELP line for {name} in:{Environment.NewLine}{Text}");

        return line["# HELP ".Length..];
    }

    private static string? Line(string prefix) =>
        Text.Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .FirstOrDefault(line => line.StartsWith(prefix, StringComparison.Ordinal));

    public static IReadOnlyList<MetricSample> Parse(string text)
    {
        var samples = new List<MetricSample>();

        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.TrimEnd('\r');

            if (trimmed.Length == 0 || trimmed[0] == '#')
            {
                continue;
            }

            var separator = trimmed.LastIndexOf(' ');
            if (separator < 0)
            {
                continue;
            }

            if (!double.TryParse(
                trimmed.AsSpan(separator + 1),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var value))
            {
                continue;
            }

            var series = trimmed[..separator];

            if (!series.EndsWith('}'))
            {
                samples.Add(new MetricSample(series, [], value));
                continue;
            }

            var name = series[..series.IndexOf('{')];

            samples.Add(new MetricSample(
                name,
                ParseLabels(series[(series.IndexOf('{') + 1)..^1]),
                value));
        }

        return samples;
    }

    public static MetricSample Single(
        string name,
        params (string Key, string Value)[] labels)
    {
        var matches = Parse(Text)
            .Where(sample => Matches(sample, name, labels))
            .ToArray();

        Assert.True(
            matches.Length > 0,
            $"No series {name} with labels [{string.Join(", ", labels.Select(label => label.Key + "=" + label.Value))}] in:{Environment.NewLine}{Text}");

        Assert.True(
            matches.Length == 1,
            $"Expected one series {name} with the labels given but found {matches.Length}:" +
            $"{Environment.NewLine}{string.Join(Environment.NewLine, matches.Select(sample => $"{sample.Name}{{{string.Join(",", sample.Labels.Select(label => label.Key + "=" + label.Value))}}} {sample.Value}"))}");

        return matches[0];
    }

    private static bool Matches(
        MetricSample sample,
        string name,
        (string Key, string Value)[] labels) =>
        string.Equals(sample.Name, name, StringComparison.Ordinal) &&
        labels.All(label => sample.Has(label.Key, label.Value));

    private static IReadOnlyList<KeyValuePair<string, string>> ParseLabels(string body)
    {
        var labels = new List<KeyValuePair<string, string>>();

        if (body.Length == 0)
        {
            return labels;
        }

        var index = 0;

        while (index < body.Length)
        {
            var equals = body.IndexOf('=', index);
            var key = body[index..equals];

            index = equals + 2;

            var value = new StringBuilder();

            while (index < body.Length && body[index] != '"')
            {
                if (body[index] == '\\' && index + 1 < body.Length)
                {
                    value.Append(body[index + 1] switch
                    {
                        'n' => '\n',
                        var escaped => escaped
                    });

                    index += 2;
                    continue;
                }

                value.Append(body[index]);
                index++;
            }

            labels.Add(new KeyValuePair<string, string>(key, value.ToString()));

            // Skip the closing quote and the separating comma.
            index += 2;
        }

        return labels;
    }
}