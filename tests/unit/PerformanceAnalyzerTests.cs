using Api.Application.Learning;
using Api.Configuration;
using Api.Domain.Enums;
using Xunit;

namespace Api.Tests;

/// <summary>
/// Covers the part of the loop that decides whether there is anything to say.
/// </summary>
public sealed class PerformanceAnalyzerTests
{
    private static readonly DateTime Monday = new(2026, 9, 7, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Tuesday = Monday.AddDays(1);

    private static LearningLoopOptions Options(
        int minimumMeasuredPosts = 5,
        int minimumPostsPerGroup = 2,
        decimal minimumGap = 0.25m) => new()
        {
            MinimumMeasuredPosts = minimumMeasuredPosts,
            MinimumPostsPerGroup = minimumPostsPerGroup,
            MinimumEngagementRateGap = minimumGap
        };

    /// <summary>
    /// A post the platform was asked about and reported nothing for. Measured,
    /// but with no rate derivable from it.
    /// </summary>
    private static AnalyzedPost PostWithoutReach(
        DateTime? publishedAt = null,
        ContentPlatform platform = ContentPlatform.LinkedIn,
        ContentType contentType = ContentType.Insight,
        Guid? pillarId = null,
        string? pillarName = null) =>
        new(
            platform,
            contentType,
            pillarId,
            pillarName,
            publishedAt ?? Monday,
            Impressions: null,
            Likes: null,
            Comments: null,
            Shares: null);

    /// <summary>
    /// Builds a post whose engagement rate is <paramref name="engagementRate"/>
    /// percent, so a test states the comparison it wants instead of arithmetic.
    /// </summary>
    private static AnalyzedPost Post(
        DateTime? publishedAt = null,
        ContentPlatform platform = ContentPlatform.LinkedIn,
        ContentType contentType = ContentType.Insight,
        Guid? pillarId = null,
        string? pillarName = null,
        decimal? engagementRate = 5m,
        long? impressions = null)
    {
        var reach = impressions ?? 1_000;

        return new AnalyzedPost(
            platform,
            contentType,
            pillarId,
            pillarName,
            publishedAt ?? Monday,
            reach,
            (long)Math.Round(reach * (engagementRate ?? 0m) / 100m, MidpointRounding.AwayFromZero),
            0,
            0);
    }

    // ---------------------------------------------------------------------
    // Insufficient data
    // ---------------------------------------------------------------------

    [Fact]
    public void No_findings_when_fewer_posts_are_measured_than_the_minimum()
    {
        var posts = Enumerable.Range(0, 4)
            .Select(index => Post(engagementRate: index % 2 == 0 ? 2m : 20m))
            .ToList();

        var analysis = PerformanceAnalyzer.Analyze(posts, 40, Options());

        Assert.False(analysis.HasSufficientData);
        Assert.Empty(analysis.Findings);
    }

    [Fact]
    public void Insufficient_run_still_reports_the_counts_it_measured()
    {
        var analysis = PerformanceAnalyzer.Analyze(
            [Post(), Post(engagementRate: 20m)],
            17,
            Options());

        Assert.Equal(17, analysis.PostsConsidered);
        Assert.Equal(2, analysis.PostsMeasured);
    }

    [Fact]
    public void No_findings_when_every_measured_post_is_on_one_platform()
    {
        var posts = Enumerable.Range(0, 6)
            .Select(index => Post(engagementRate: index == 0 ? 1m : 30m))
            .ToList();

        var analysis = PerformanceAnalyzer.Analyze(posts, posts.Count, Options());

        // A single group has nothing to be compared against. The spread across
        // those posts is real, but the platform did not vary.
        Assert.Empty(analysis.Findings);
    }

    // ---------------------------------------------------------------------
    // What clears the threshold
    // ---------------------------------------------------------------------

    [Fact]
    public void Reports_a_platform_difference_that_clears_the_gap()
    {
        var posts = new List<AnalyzedPost>
        {
            Post(platform: ContentPlatform.LinkedIn, engagementRate: 6m),
            Post(platform: ContentPlatform.LinkedIn, engagementRate: 6m),
            Post(platform: ContentPlatform.X, engagementRate: 2m),
            Post(platform: ContentPlatform.X, engagementRate: 2m),
            Post(platform: ContentPlatform.X, engagementRate: 2m)
        };

        var analysis = PerformanceAnalyzer.Analyze(posts, posts.Count, Options());

        var finding = Assert.Single(analysis.Findings);
        Assert.Equal(LearningDimension.Platform, finding.Dimension);
        Assert.Equal("PLATFORM_ENGAGEMENT", finding.SignalType);
        Assert.Equal("LinkedIn", finding.Best.Label);
        Assert.Equal("X", finding.Worst.Label);
        Assert.Equal(5, finding.EvidenceCount);
    }

    [Fact]
    public void Stays_silent_when_the_difference_is_inside_the_gap()
    {
        var posts = new List<AnalyzedPost>
        {
            Post(platform: ContentPlatform.LinkedIn, engagementRate: 5.1m),
            Post(platform: ContentPlatform.LinkedIn, engagementRate: 5.1m),
            Post(platform: ContentPlatform.X, engagementRate: 5m),
            Post(platform: ContentPlatform.X, engagementRate: 5m),
            Post(platform: ContentPlatform.X, engagementRate: 5m)
        };

        var analysis = PerformanceAnalyzer.Analyze(posts, posts.Count, Options());

        // 5.1 against 5.0 is a two percent difference. Reporting it would spend
        // an AI call on noise.
        Assert.Empty(analysis.Findings);
    }

    [Fact]
    public void Stays_silent_when_a_group_has_fewer_posts_than_the_minimum()
    {
        var posts = new List<AnalyzedPost>
        {
            Post(platform: ContentPlatform.LinkedIn, engagementRate: 30m),
            Post(platform: ContentPlatform.LinkedIn, engagementRate: 30m),
            Post(platform: ContentPlatform.LinkedIn, engagementRate: 30m),
            Post(platform: ContentPlatform.LinkedIn, engagementRate: 30m),
            // Far enough below the rest to look dramatic, and still not a pattern.
            Post(platform: ContentPlatform.X, engagementRate: 0.1m)
        };

        var analysis = PerformanceAnalyzer.Analyze(posts, posts.Count, Options());

        Assert.Empty(analysis.Findings);
    }

    [Fact]
    public void A_group_whose_impressions_were_never_reported_cannot_be_ranked()
    {
        var posts = new List<AnalyzedPost>
        {
            Post(platform: ContentPlatform.LinkedIn, engagementRate: 30m),
            Post(platform: ContentPlatform.LinkedIn, engagementRate: 30m),
            PostWithoutReach(platform: ContentPlatform.X),
            PostWithoutReach(platform: ContentPlatform.X),
            PostWithoutReach(platform: ContentPlatform.X)
        };

        var analysis = PerformanceAnalyzer.Analyze(posts, posts.Count, Options());

        // The X posts are measured, and they are counted in the workspace total,
        // but no rate can be formed from them, so they cannot be the weakest.
        // Ranking them as zero would read as a result the platform never gave.
        Assert.Equal(5, analysis.PostsMeasured);
        Assert.Empty(analysis.Findings);
    }

    [Fact]
    public void A_post_with_no_reported_reach_does_not_drag_its_group_down()
    {
        var posts = new List<AnalyzedPost>
        {
            Post(platform: ContentPlatform.LinkedIn, engagementRate: 6m),
            Post(platform: ContentPlatform.LinkedIn, engagementRate: 6m),
            Post(platform: ContentPlatform.LinkedIn, engagementRate: 6m),
            Post(platform: ContentPlatform.X, engagementRate: 2m, impressions: 500),
            Post(platform: ContentPlatform.X, engagementRate: 2m, impressions: 500),
            PostWithoutReach(platform: ContentPlatform.X)
        };

        var finding = Assert.Single(PerformanceAnalyzer.Analyze(posts, posts.Count, Options()).Findings);

        // X is still three posts, and its rate comes from the two that reported
        // reach. The unmeasured one is neither counted as zero engagement nor
        // dropped from the group.
        Assert.Equal(3, finding.Worst.Posts);
        Assert.Equal(1_000, finding.Worst.Impressions);
        Assert.Equal(2m, finding.Worst.EngagementRate);
        Assert.Equal(6, finding.EvidenceCount);
    }

    [Fact]
    public void A_group_the_platform_measured_as_null_is_not_ranked_as_zero()
    {
        var posts = new List<AnalyzedPost>
        {
            Post(platform: ContentPlatform.LinkedIn, engagementRate: 30m),
            Post(platform: ContentPlatform.LinkedIn, engagementRate: 30m),
            Post(platform: ContentPlatform.X, engagementRate: 1m, impressions: 0),
            Post(platform: ContentPlatform.X, engagementRate: 1m, impressions: 0)
        };

        var analysis = PerformanceAnalyzer.Analyze(posts, posts.Count, Options());

        // Zero impressions is a real zero, not a missing measurement, but a rate
        // cannot be formed from it. Excluded rather than ranked as the worst.
        Assert.Empty(analysis.Findings);
    }

    // ---------------------------------------------------------------------
    // Dimensions
    // ---------------------------------------------------------------------

    [Fact]
    public void An_unattributed_post_is_excluded_from_the_pillar_dimension_only()
    {
        var automation = Guid.NewGuid();
        var career = Guid.NewGuid();

        var posts = new List<AnalyzedPost>
        {
            Post(pillarId: automation, pillarName: "Automation", engagementRate: 8m),
            Post(pillarId: automation, pillarName: "Automation", engagementRate: 8m),
            Post(pillarId: automation, pillarName: "Automation", engagementRate: 8m),
            Post(pillarId: career, pillarName: "Career", engagementRate: 2m),
            Post(pillarId: career, pillarName: "Career", engagementRate: 2m),
            Post(pillarId: career, pillarName: "Career", engagementRate: 2m)
        };

        var unattributed = new List<AnalyzedPost>
        {
            Post(platform: ContentPlatform.X, engagementRate: 1m),
            Post(platform: ContentPlatform.X, engagementRate: 1m)
        };

        var analysis = PerformanceAnalyzer.Analyze([.. posts, .. unattributed], 8, Options());

        var pillarFinding = Assert.Single(
            analysis.Findings,
            finding => finding.Dimension == LearningDimension.ContentPillar);

        // Six posts were attributed to a pillar; the two that were not are not
        // a pillar anyone can act on, so they are left out of this comparison
        // rather than collected into a group called "none".
        Assert.Equal("Automation", pillarFinding.Best.Label);
        Assert.Equal("Career", pillarFinding.Worst.Label);
        Assert.Equal(6, pillarFinding.EvidenceCount);
    }

    [Fact]
    public void Publishing_day_is_reported_in_utc()
    {
        var posts = new List<AnalyzedPost>
        {
            Post(publishedAt: Monday, engagementRate: 9m),
            Post(publishedAt: Monday.AddDays(7), engagementRate: 9m),
            Post(publishedAt: Monday.AddDays(14), engagementRate: 9m),
            Post(publishedAt: Tuesday, engagementRate: 2m),
            Post(publishedAt: Tuesday.AddDays(7), engagementRate: 2m)
        };

        var analysis = PerformanceAnalyzer.Analyze(posts, posts.Count, Options());

        var finding = Assert.Single(
            analysis.Findings,
            candidate => candidate.Dimension == LearningDimension.PostingDay);

        Assert.Equal("Monday (UTC)", finding.Best.Label);
        Assert.Equal("Tuesday (UTC)", finding.Worst.Label);
    }

    [Fact]
    public void A_post_with_no_publication_time_is_left_out_of_the_day_dimension()
    {
        var posts = new List<AnalyzedPost>
        {
            Post(publishedAt: Monday, engagementRate: 9m),
            Post(publishedAt: Monday, engagementRate: 9m),
            Post(publishedAt: null, platform: ContentPlatform.X, engagementRate: 2m),
            Post(publishedAt: null, platform: ContentPlatform.X, engagementRate: 2m)
        };

        var analysis = PerformanceAnalyzer.Analyze(posts, posts.Count, Options());

        Assert.DoesNotContain(analysis.Findings, finding => finding.Dimension == LearningDimension.PostingDay);
    }

    // ---------------------------------------------------------------------
    // Determinism
    // ---------------------------------------------------------------------

    [Fact]
    public void Findings_do_not_depend_on_the_order_posts_arrive_in()
    {
        var posts = new List<AnalyzedPost>
        {
            Post(platform: ContentPlatform.LinkedIn, engagementRate: 8m),
            Post(platform: ContentPlatform.LinkedIn, engagementRate: 8m),
            Post(platform: ContentPlatform.LinkedIn, engagementRate: 8m),
            Post(platform: ContentPlatform.X, engagementRate: 2m),
            Post(platform: ContentPlatform.X, engagementRate: 2m),
            Post(platform: ContentPlatform.X, engagementRate: 2m)
        };

        var forwards = PerformanceAnalyzer.Analyze(posts, posts.Count, Options()).Findings;
        var backwards = PerformanceAnalyzer
            .Analyze(posts.AsEnumerable().Reverse().ToList(), posts.Count, Options())
            .Findings;

        Assert.Equal(forwards.Count, backwards.Count);
        for (var index = 0; index < forwards.Count; index++)
        {
            Assert.Equal(forwards[index].Dimension, backwards[index].Dimension);
            Assert.Equal(forwards[index].SignalType, backwards[index].SignalType);
            Assert.Equal(forwards[index].Observation, backwards[index].Observation);
            Assert.Equal(forwards[index].Confidence, backwards[index].Confidence);
        }
    }

    [Fact]
    public void Groups_at_the_same_rate_are_ranked_in_a_stable_order()
    {
        var best = Guid.NewGuid();
        var tied = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var alsoTied = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var posts = new List<AnalyzedPost>
        {
            Post(pillarId: best, pillarName: "Best", engagementRate: 9m),
            Post(pillarId: best, pillarName: "Best", engagementRate: 9m),
            Post(pillarId: tied, pillarName: "Tied", engagementRate: 5m),
            Post(pillarId: tied, pillarName: "Tied", engagementRate: 5m),
            Post(pillarId: alsoTied, pillarName: "Also tied", engagementRate: 5m),
            Post(pillarId: alsoTied, pillarName: "Also tied", engagementRate: 5m)
        };

        var forwards = PillarFinding(PerformanceAnalyzer.Analyze(posts, posts.Count, Options()));
        var backwards = PillarFinding(
            PerformanceAnalyzer.Analyze(posts.AsEnumerable().Reverse().ToList(), posts.Count, Options()));

        // The two tied pillars sit in the same order whichever order the posts
        // came back in, because the tie is broken by the group key rather than by
        // which post the database returned first.
        Assert.Equal(
            forwards.Groups.Select(group => group.Key),
            backwards.Groups.Select(group => group.Key));
        Assert.Equal(
            [tied.ToString(), alsoTied.ToString()],
            forwards.Groups.Skip(1).Select(group => group.Key).ToArray());
    }

    private static PerformanceFinding PillarFinding(PerformanceAnalysis analysis) =>
        Assert.Single(analysis.Findings, finding => finding.Dimension == LearningDimension.ContentPillar);

    // ---------------------------------------------------------------------
    // Observation wording and confidence
    // ---------------------------------------------------------------------

    [Fact]
    public void The_observation_states_the_measured_totals_and_names_both_groups()
    {
        var posts = new List<AnalyzedPost>
        {
            Post(platform: ContentPlatform.LinkedIn, engagementRate: 6m),
            Post(platform: ContentPlatform.LinkedIn, engagementRate: 6m),
            Post(platform: ContentPlatform.LinkedIn, engagementRate: 6m),
            Post(platform: ContentPlatform.X, engagementRate: 2m, impressions: 500),
            Post(platform: ContentPlatform.X, engagementRate: 2m, impressions: 500)
        };

        var finding = Assert.Single(PerformanceAnalyzer.Analyze(posts, posts.Count, Options()).Findings);

        Assert.Equal(
            "Across 5 measured posts, Platform LinkedIn averaged 6% engagement over 3 posts, " +
            "compared with 2% for X over 2 posts.",
            finding.Observation);
    }

    [Fact]
    public void The_observation_does_not_claim_the_difference_was_caused()
    {
        var posts = new List<AnalyzedPost>
        {
            Post(platform: ContentPlatform.LinkedIn, engagementRate: 6m),
            Post(platform: ContentPlatform.LinkedIn, engagementRate: 6m),
            Post(platform: ContentPlatform.LinkedIn, engagementRate: 6m),
            Post(platform: ContentPlatform.X, engagementRate: 2m),
            Post(platform: ContentPlatform.X, engagementRate: 2m)
        };

        var finding = Assert.Single(PerformanceAnalyzer.Analyze(posts, posts.Count, Options()).Findings);

        Assert.DoesNotContain("because", finding.Observation, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cause", finding.Observation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Confidence_rises_with_the_smaller_groups_post_count()
    {
        var thin = PerformanceAnalyzer.Analyze(
            [
                Post(platform: ContentPlatform.LinkedIn, engagementRate: 6m),
                Post(platform: ContentPlatform.LinkedIn, engagementRate: 6m),
                Post(platform: ContentPlatform.X, engagementRate: 2m),
                Post(platform: ContentPlatform.X, engagementRate: 2m),
                Post(platform: ContentPlatform.X, engagementRate: 2m)
            ],
            5,
            Options()).Findings.Single().Confidence;

        var thick = PerformanceAnalyzer.Analyze(
            [
                Post(platform: ContentPlatform.LinkedIn, engagementRate: 6m),
                Post(platform: ContentPlatform.LinkedIn, engagementRate: 6m),
                Post(platform: ContentPlatform.LinkedIn, engagementRate: 6m),
                Post(platform: ContentPlatform.LinkedIn, engagementRate: 6m),
                Post(platform: ContentPlatform.X, engagementRate: 2m),
                Post(platform: ContentPlatform.X, engagementRate: 2m),
                Post(platform: ContentPlatform.X, engagementRate: 2m),
                Post(platform: ContentPlatform.X, engagementRate: 2m)
            ],
            8,
            Options()).Findings.Single().Confidence;

        Assert.True(thick > thin, $"expected {thick} to exceed {thin}");
        Assert.Equal(50m, thin);
        Assert.Equal(100m, thick);
    }

    [Fact]
    public void Confidence_never_exceeds_one_hundred()
    {
        var posts = Enumerable.Range(0, 10)
            .Select(index => Post(
                platform: index % 2 == 0 ? ContentPlatform.LinkedIn : ContentPlatform.X,
                engagementRate: index % 2 == 0 ? 9m : 1m))
            .ToList();

        var finding = PerformanceAnalyzer.Analyze(posts, posts.Count, Options()).Findings.Single();

        Assert.InRange(finding.Confidence, 0m, 100m);
        Assert.Equal(100m, finding.Confidence);
    }

    [Fact]
    public void Every_dimension_is_analysed_in_a_fixed_order()
    {
        var automation = Guid.NewGuid();
        var career = Guid.NewGuid();

        AnalyzedPost Strong() => Post(
            publishedAt: Monday,
            platform: ContentPlatform.LinkedIn,
            contentType: ContentType.Insight,
            pillarId: automation,
            pillarName: "Automation",
            engagementRate: 9m);

        AnalyzedPost Weak(DayOfWeek day) => Post(
            publishedAt: DayOf(day),
            platform: ContentPlatform.X,
            contentType: ContentType.HowTo,
            pillarId: career,
            pillarName: "Career",
            engagementRate: 1m);

        // Two strong posts and three weak ones, arranged so that every dimension
        // has two groups of at least the minimum.
        var posts = new List<AnalyzedPost>
        {
            Strong(),
            Strong(),
            Weak(DayOfWeek.Tuesday),
            Weak(DayOfWeek.Tuesday),
            Weak(DayOfWeek.Tuesday)
        };

        var analysis = PerformanceAnalyzer.Analyze(posts, posts.Count, Options());

        Assert.Equal(
            [
                LearningDimension.Platform,
                LearningDimension.ContentType,
                LearningDimension.ContentPillar,
                LearningDimension.PostingDay
            ],
            analysis.Findings.Select(finding => finding.Dimension).ToArray());

        // These strings are persisted in SignalType and returned as "type", so
        // they are part of the published vocabulary. Deriving them from the enum
        // name would emit CONTENTTYPE_ENGAGEMENT instead of CONTENT_TYPE_ENGAGEMENT.
        Assert.Equal(
            [
                "PLATFORM_ENGAGEMENT",
                "CONTENT_TYPE_ENGAGEMENT",
                "CONTENT_PILLAR_ENGAGEMENT",
                "POSTING_DAY_ENGAGEMENT"
            ],
            analysis.Findings.Select(finding => finding.SignalType).ToArray());
    }

    private static DateTime DayOf(DayOfWeek day) =>
        Monday.AddDays(((int)day - (int)Monday.DayOfWeek + 7) % 7);
}
