using System.Globalization;
using Api.Application.Analytics;
using Api.Configuration;

namespace Api.Application.Learning;

/// <summary>
/// Compares a workspace's measured posts across one dimension at a time and
/// reports a difference only where the evidence supports one.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately free of EF, HTTP and AI. Everything here is arithmetic over
/// numbers that were already measured, so a finding can be reproduced from the
/// same inputs and cannot drift from the documented metric definitions — the
/// engagement rate is the one
/// <see cref="AnalyticsCalculator"/> defines, not a second formula.
/// </para>
/// <para>
/// The output is also the boundary between observation and inference. The
/// <see cref="PerformanceFinding.Observation"/> text is generated from the
/// totals below, so it can only state what was measured. Interpretation is the
/// model's job and happens later, on the evidence this produces.
/// </para>
/// <para>
/// Nothing here claims that a difference was caused by the thing being compared.
/// Posts differ in many ways at once, and a comparison across a handful of them
/// is a correlation at best. The wording is chosen to survive being read alone.
/// </para>
/// </remarks>
public static class PerformanceAnalyzer
{
    /// <summary>
    /// Analyses measured posts and returns at most one finding per dimension.
    /// </summary>
    /// <param name="measuredPosts">
    /// Published posts that have a metrics snapshot. Posts without one are
    /// counted in <paramref name="postsConsidered"/> and contribute no evidence.
    /// </param>
    /// <param name="postsConsidered">Published posts in the window, measured or not.</param>
    public static PerformanceAnalysis Analyze(
        IReadOnlyList<AnalyzedPost> measuredPosts,
        int postsConsidered,
        LearningLoopOptions options)
    {
        ArgumentNullException.ThrowIfNull(measuredPosts);
        ArgumentNullException.ThrowIfNull(options);

        if (measuredPosts.Count < options.MinimumMeasuredPosts)
        {
            // Not a signal about performance, just not enough to compare. Reported
            // honestly as a short count rather than dressed up as a finding.
            return new PerformanceAnalysis(postsConsidered, measuredPosts.Count, []);
        }

        var findings = new List<PerformanceFinding>();

        // Dimension order is fixed rather than data-driven so the same workspace
        // always produces its findings in the same sequence.
        foreach (var dimension in Dimensions)
        {
            var finding = AnalyzeDimension(measuredPosts, dimension, options);
            if (finding is not null)
            {
                findings.Add(finding);
            }
        }

        return new PerformanceAnalysis(postsConsidered, measuredPosts.Count, findings);
    }

    /// <summary>
    /// Fixed dimension sequence. Adding a dimension here is the only change
    /// needed to have it analysed.
    /// </summary>
    private static readonly LearningDimension[] Dimensions =
    [
        LearningDimension.Platform,
        LearningDimension.ContentType,
        LearningDimension.ContentPillar,
        LearningDimension.PostingDay
    ];

    private static PerformanceFinding? AnalyzeDimension(
        IReadOnlyList<AnalyzedPost> posts,
        LearningDimension dimension,
        LearningLoopOptions options)
    {
        var groups = BuildGroups(posts, dimension, options);
        if (groups.Count < 2)
        {
            return null;
        }

        // Best first, weakest last. Key breaks ties so two equal rates produce a
        // stable choice instead of depending on the order the posts arrived in.
        var ranked = groups
            .OrderByDescending(group => group.EngagementRate!.Value)
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .ToList();

        var best = ranked[0];
        var worst = ranked[^1];

        if (!ClearsGap(best.EngagementRate!.Value, worst.EngagementRate!.Value, options))
        {
            return null;
        }

        var evidenceCount = groups.Sum(group => group.Posts);

        return new PerformanceFinding(
            dimension,
            SignalTypeFor(dimension),
            Describe(dimension, best, worst, evidenceCount),
            best,
            worst,
            ranked,
            evidenceCount,
            Confidence(best, worst, options));
    }

    /// <summary>
    /// Signal kinds are part of the published contract, so they are mapped
    /// explicitly rather than derived from the enum name: upper-casing
    /// <c>ContentType</c> would emit <c>CONTENTTYPE_ENGAGEMENT</c>, which no
    /// documented consumer expects.
    /// </summary>
    private static string SignalTypeFor(LearningDimension dimension) => dimension switch
    {
        LearningDimension.Platform => "PLATFORM_ENGAGEMENT",
        LearningDimension.ContentType => "CONTENT_TYPE_ENGAGEMENT",
        LearningDimension.ContentPillar => "CONTENT_PILLAR_ENGAGEMENT",
        LearningDimension.PostingDay => "POSTING_DAY_ENGAGEMENT",
        _ => throw new ArgumentOutOfRangeException(
            nameof(dimension),
            dimension,
            "Unknown learning dimension.")
    };

    private static bool ClearsGap(decimal best, decimal worst, LearningLoopOptions options)
    {
        if (best <= worst)
        {
            return false;
        }

        return best >= worst * (1m + options.MinimumEngagementRateGap);
    }

    /// <summary>
    /// Groups the posts by one dimension, keeping only groups that could support
    /// a comparison.
    /// </summary>
    private static List<LearningGroup> BuildGroups(
        IReadOnlyList<AnalyzedPost> posts,
        LearningDimension dimension,
        LearningLoopOptions options)
    {
        // Keyed by the stable value so ordering is reproducible; the label is
        // carried alongside because a pillar is identified by its id but has to
        // be named in a sentence.
        var grouped = new Dictionary<string, (string Label, List<AnalyzedPost> Posts)>(
            StringComparer.Ordinal);

        foreach (var post in posts)
        {
            var resolved = Resolve(post, dimension);
            if (resolved is null)
            {
                // The post cannot be placed on this dimension at all, which
                // happens for an idea that was never attributed to a pillar.
                // Excluded rather than collected into a synthetic group, because
                // "unattributed" is not a pillar anyone can act on.
                continue;
            }

            if (!grouped.TryGetValue(resolved.Value.Key, out var bucket))
            {
                bucket = (resolved.Value.Label, []);
                grouped[resolved.Value.Key] = bucket;
            }

            bucket.Posts.Add(post);
        }

        var groups = new List<LearningGroup>();

        foreach (var (key, bucket) in grouped)
        {
            if (bucket.Posts.Count < options.MinimumPostsPerGroup)
            {
                continue;
            }

            var impressions = AnalyticsCalculator.Sum(bucket.Posts.Select(post => post.Impressions));
            var engagementRate = AnalyticsCalculator.EngagementRate(
                AnalyticsCalculator.Sum(bucket.Posts.Select(post => post.Likes)),
                AnalyticsCalculator.Sum(bucket.Posts.Select(post => post.Comments)),
                AnalyticsCalculator.Sum(bucket.Posts.Select(post => post.Shares)),
                impressions);

            if (engagementRate is null)
            {
                // No impressions means no rate, so there is nothing to rank the
                // group by. A group is never scored as zero reach.
                continue;
            }

            groups.Add(new LearningGroup(
                key,
                bucket.Label,
                bucket.Posts.Count,
                impressions,
                engagementRate));
        }

        return groups;
    }

    private static (string Key, string Label)? Resolve(AnalyzedPost post, LearningDimension dimension) =>
        dimension switch
        {
            LearningDimension.Platform => (post.Platform.ToString(), post.Platform.ToString()),
            LearningDimension.ContentType => (post.ContentType.ToString(), post.ContentType.ToString()),
            LearningDimension.ContentPillar when post.ContentPillarId is not null =>
                (post.ContentPillarId.Value.ToString(), post.ContentPillarName ?? "Unnamed pillar"),
            LearningDimension.ContentPillar => null,
            LearningDimension.PostingDay when post.PublishedAt is not null => Day(post.PublishedAt.Value),
            // A published post with no publication time cannot be placed on a
            // calendar, so it is left out of this dimension only. It still counts
            // towards the others and towards the workspace's measured total.
            LearningDimension.PostingDay => null,
            _ => throw new ArgumentOutOfRangeException(nameof(dimension), dimension, "Unknown learning dimension.")
        };

    /// <summary>
    /// Day of week in UTC.
    /// </summary>
    /// <remarks>
    /// UTC, because the workspace's audience timezone is not stored anywhere the
    /// publisher can read it, and a "best time to post" figure silently shifted
    /// into the wrong zone would be worse than one that says UTC. Hour of day is
    /// not analysed at all: 24 buckets would leave almost every bucket with a
    /// single post, which cannot support a comparison no matter what the
    /// threshold is set to.
    /// </remarks>
    private static (string Key, string Label) Day(DateTime publishedAt) =>
        (publishedAt.DayOfWeek.ToString(), $"{publishedAt.DayOfWeek} (UTC)");

    private static string Describe(
        LearningDimension dimension,
        LearningGroup best,
        LearningGroup worst,
        int evidenceCount)
    {
        var posts = evidenceCount == 1 ? "post" : "posts";
        var subject = dimension switch
        {
            LearningDimension.PostingDay => "Publishing day",
            _ => dimension.ToString()
        };

        return string.Create(
            CultureInfo.InvariantCulture,
            $"Across {evidenceCount} measured {posts}, {subject} {best.Label} averaged " +
            $"{Format(best.EngagementRate)}% engagement over {best.Posts} {Plural(best.Posts)}, " +
            $"compared with {Format(worst.EngagementRate)}% for {worst.Label} over " +
            $"{worst.Posts} {Plural(worst.Posts)}.");
    }

    /// <summary>
    /// Evidence strength behind a finding, from 0 to 100.
    /// </summary>
    /// <remarks>
    /// A deterministic function of the smaller of the two compared group sizes,
    /// relative to the minimum a group needs to be considered at all. It is a
    /// strength-of-evidence indicator for ordering findings against each other,
    /// and nothing more: it is not a probability, and no significance test or
    /// confidence interval is computed anywhere in this feature.
    /// </remarks>
    private static decimal Confidence(LearningGroup best, LearningGroup worst, LearningLoopOptions options)
    {
        var weakerSide = Math.Min(best.Posts, worst.Posts);
        var fullConfidencePosts = Math.Max(1, options.MinimumPostsPerGroup * 2);

        var ratio = 100m * weakerSide / fullConfidencePosts;

        return Math.Round(Math.Min(ratio, 100m), 2, MidpointRounding.AwayFromZero);
    }

    private static string Format(decimal? rate) =>
        rate!.Value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string Plural(int count) => count == 1 ? "post" : "posts";
}
