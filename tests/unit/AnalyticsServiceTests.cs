using Api.Application.Analytics;
using Api.Application.Authentication;
using Api.Domain.Entities;
using Api.Domain.Enums;
using Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Xunit;

namespace Api.Tests;

public sealed class AnalyticsServiceTests
{
    private static readonly DateTime Anchor = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    // ---------------------------------------------------------------------
    // Workspace isolation
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Overview_never_counts_another_workspaces_posts_or_metrics()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        var otherWorkspaceId = Guid.NewGuid();

        SeedWithMetrics(dbContext, workspaceId, Anchor, 1_000, 100, 10, 5, 20);
        SeedWithMetrics(dbContext, otherWorkspaceId, Anchor, 9_000_000, 900_000, 90_000, 9_000, 800);

        var result = await CreateService(dbContext, workspaceId).GetOverviewAsync(new AnalyticsQuery());

        Assert.Equal(1, result.PostsPublished);
        Assert.Equal(1_000, result.Impressions);
        Assert.Equal(100, result.Likes);
    }

    [Fact]
    public async Task Posts_never_lists_another_workspaces_published_posts()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        var otherWorkspaceId = Guid.NewGuid();

        SeedPublishedPost(dbContext, workspaceId, Anchor);
        SeedPublishedPost(dbContext, otherWorkspaceId, Anchor);

        var result = await CreateService(dbContext, workspaceId).GetPostsAsync(new AnalyticsQuery());

        Assert.Single(result);
    }

    /// <summary>
    /// A metric row must only count if its post survived filtering. A post
    /// outside the requested period contributes nothing, even though its
    /// snapshot is in the table and in the same workspace.
    /// </summary>
    [Fact]
    public async Task Overview_ignores_metrics_whose_post_is_outside_the_filtered_set()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();

        SeedWithMetrics(dbContext, workspaceId, Anchor, 500, 50, 5, 5, 25);
        SeedWithMetrics(dbContext, workspaceId, Anchor.AddDays(-40), 400_000, 40_000, 4_000, 400, 4_000);

        var result = await CreateService(dbContext, workspaceId)
            .GetOverviewAsync(new AnalyticsQuery(From: Anchor.AddDays(-1)));

        Assert.Equal(1, result.PostsPublished);
        Assert.Equal(500, result.Impressions);
    }

    [Fact]
    public async Task Get_post_hides_another_workspaces_post()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        var theirs = SeedPublishedPost(dbContext, Guid.NewGuid(), Anchor);

        var result = await CreateService(dbContext, workspaceId).GetPostAsync(theirs.Id);

        Assert.Null(result);
    }

    [Fact]
    public async Task Reads_ignore_data_belonging_to_a_workspace_the_caller_did_not_claim()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();

        SeedWithMetrics(dbContext, Guid.NewGuid(), Anchor, 5_000, 500, 50, 50, 250);

        var result = await CreateService(dbContext, workspaceId).GetOverviewAsync(new AnalyticsQuery());

        Assert.Equal(0, result.PostsPublished);
        Assert.Null(result.Impressions);
    }

    // ---------------------------------------------------------------------
    // Status filtering
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Overview_counts_published_posts_only()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        var idea = AddIdea(dbContext, workspaceId);

        SeedPublishedPost(dbContext, workspaceId, Anchor);
        SeedPost(dbContext, workspaceId, idea, ContentPostStatus.Draft, null, null);
        SeedPost(dbContext, workspaceId, idea, ContentPostStatus.Approved, null, null);
        SeedPost(dbContext, workspaceId, idea, ContentPostStatus.Scheduled, null, null);
        SeedPost(dbContext, workspaceId, idea, ContentPostStatus.Rejected, null, null);

        var result = await CreateService(dbContext, workspaceId).GetOverviewAsync(new AnalyticsQuery());

        Assert.Equal(1, result.PostsPublished);
    }

    /// <summary>
    /// There is no Failed status: a failed publish leaves the post Scheduled
    /// with a recorded reason. Such a post must not count as published even
    /// though a snapshot happens to exist for it.
    /// </summary>
    [Fact]
    public async Task Overview_does_not_count_a_post_whose_publish_failed()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        var idea = AddIdea(dbContext, workspaceId);
        var failed = SeedPost(dbContext, workspaceId, idea, ContentPostStatus.Scheduled, null, null);
        failed.PublishFailureReason = PublishFailureReason.ProviderResponseInvalid;
        dbContext.SaveChanges();
        SeedMetric(dbContext, workspaceId, failed.Id, Anchor, 1_000, 100, 10, 10, 50);

        var result = await CreateService(dbContext, workspaceId).GetOverviewAsync(new AnalyticsQuery());

        Assert.Equal(0, result.PostsPublished);
        Assert.Null(result.Impressions);
    }

    [Fact]
    public async Task Get_post_returns_null_for_a_post_that_was_never_published()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        var idea = AddIdea(dbContext, workspaceId);
        var draft = SeedPost(dbContext, workspaceId, idea, ContentPostStatus.Draft, null, null);

        var result = await CreateService(dbContext, workspaceId).GetPostAsync(draft.Id);

        Assert.Null(result);
    }

    // ---------------------------------------------------------------------
    // Metric calculations
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Overview_sums_every_published_post_in_the_period()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();

        SeedWithMetrics(dbContext, workspaceId, Anchor, 1_000, 100, 10, 5, 20, clicks: 40, profileVisits: 25);
        SeedWithMetrics(dbContext, workspaceId, Anchor.AddDays(1), 2_000, 300, 20, 15, 30, clicks: 60, profileVisits: 25);

        var result = await CreateService(dbContext, workspaceId).GetOverviewAsync(new AnalyticsQuery());

        Assert.Equal(2, result.PostsPublished);
        Assert.Equal(3_000, result.Impressions);
        Assert.Equal(400, result.Likes);
        Assert.Equal(30, result.Comments);
        Assert.Equal(20, result.Shares);
        Assert.Equal(100, result.Clicks);
        Assert.Equal(50, result.ProfileVisits);
        Assert.Equal(50, result.FollowersGained);
    }

    [Fact]
    public async Task Engagement_rate_is_derived_from_the_totals()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();

        // Clicks are deliberately larger than every engagement metric combined:
        // if they leaked into the formula the answer would be obviously wrong.
        SeedWithMetrics(dbContext, workspaceId, Anchor, 4_000, 200, 40, 20, 10, clicks: 5_000);

        var result = await CreateService(dbContext, workspaceId).GetOverviewAsync(new AnalyticsQuery());

        Assert.Equal(6.5m, result.EngagementRate);
    }

    /// <summary>
    /// Summing first and dividing once is not the same as averaging per-post
    /// rates. A 10-impression post must not carry the same weight as a
    /// 100,000-impression post.
    /// </summary>
    [Fact]
    public async Task Engagement_rate_weights_posts_by_impressions_not_as_an_average_of_rates()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();

        SeedWithMetrics(dbContext, workspaceId, Anchor, 100_000, 10_000, 0, 0, 0);
        SeedWithMetrics(dbContext, workspaceId, Anchor.AddDays(1), 10, 5, 0, 0, 0);

        var result = await CreateService(dbContext, workspaceId).GetOverviewAsync(new AnalyticsQuery());

        // Totals: 10005 / 100010 = 10.0%. The naive average of the two per-post
        // rates (10% and 50%) would report 30%, which is not a property of the
        // underlying numbers.
        Assert.Equal(10.0m, result.EngagementRate);
    }

    [Fact]
    public async Task Post_engagement_rate_uses_that_post_only()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();

        var engaged = SeedWithMetrics(dbContext, workspaceId, Anchor, 1_000, 200, 0, 0, 0);
        var ignored = SeedWithMetrics(dbContext, workspaceId, Anchor.AddDays(1), 1_000, 0, 0, 0, 0);

        var posts = await CreateService(dbContext, workspaceId).GetPostsAsync(new AnalyticsQuery());

        Assert.Equal(20.0m, posts.Single(post => post.ContentPostId == engaged.Id).EngagementRate);
        Assert.Equal(0.0m, posts.Single(post => post.ContentPostId == ignored.Id).EngagementRate);
    }

    // ---------------------------------------------------------------------
    // Null versus zero
    // ---------------------------------------------------------------------

    /// <summary>
    /// The design requires "metric unavailable", "metric = 0" and "metric not yet
    /// synchronized" to stay distinct. A post with no snapshot at all is the
    /// third state and must not be reported as zeros.
    /// </summary>
    [Fact]
    public async Task A_post_with_no_snapshot_reports_unknown_not_zero()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        var post = SeedPublishedPost(dbContext, workspaceId, Anchor);

        var result = await CreateService(dbContext, workspaceId).GetPostAsync(post.Id);

        Assert.NotNull(result);
        Assert.Null(result!.RecordedAt);
        Assert.Null(result.Impressions);
        Assert.Null(result.Likes);
        Assert.Null(result.EngagementRate);
    }

    [Fact]
    public async Task An_empty_workspace_reports_zero_posts_and_unknown_metrics()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();

        var result = await CreateService(dbContext, workspaceId).GetOverviewAsync(new AnalyticsQuery());

        Assert.Equal(0, result.PostsPublished);
        Assert.Null(result.Impressions);
        Assert.Null(result.Likes);
        Assert.Null(result.Comments);
        Assert.Null(result.Shares);
        Assert.Null(result.Clicks);
        Assert.Null(result.ProfileVisits);
        Assert.Null(result.FollowersGained);
        Assert.Null(result.EngagementRate);
    }

    /// <summary>
    /// Zero is a real measurement: the platform reported a post nobody engaged
    /// with. It must survive as 0 rather than collapsing to null.
    /// </summary>
    [Fact]
    public async Task A_platform_reported_zero_survives_as_zero()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        var post = SeedPublishedPost(dbContext, workspaceId, Anchor);
        SeedMetric(dbContext, workspaceId, post.Id, Anchor, 0, 0, 0, 0, 0);

        var result = await CreateService(dbContext, workspaceId).GetPostAsync(post.Id);

        Assert.NotNull(result);
        Assert.Equal(0, result!.Impressions);
        Assert.Equal(0, result.Likes);
        Assert.Equal(Anchor, result.RecordedAt);
        // A rate over zero impressions is undefined, so this stays null even
        // though every input was genuinely measured.
        Assert.Null(result.EngagementRate);
    }

    [Fact]
    public async Task Partially_reported_metrics_are_summed_without_inventing_the_missing_ones()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();

        // Followers were not reported by the one post that has a snapshot, and
        // the second post has no snapshot at all, so the total stays unknown.
        SeedWithMetrics(dbContext, workspaceId, Anchor, 100, 10, 0, 0, null, clicks: 7);
        SeedPublishedPost(dbContext, workspaceId, Anchor.AddDays(1));

        var result = await CreateService(dbContext, workspaceId).GetOverviewAsync(new AnalyticsQuery());

        Assert.Equal(2, result.PostsPublished);
        Assert.Equal(100, result.Impressions);
        Assert.Equal(7, result.Clicks);
        // Followers were never reported by the post that has a snapshot.
        Assert.Null(result.FollowersGained);
    }

    [Fact]
    public async Task A_snapshot_where_one_metric_is_missing_does_not_hide_the_others()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        var post = SeedPublishedPost(dbContext, workspaceId, Anchor);
        SeedMetric(dbContext, workspaceId, post.Id, Anchor, 500, 50, 5, 0, 10, clicks: null);

        var result = await CreateService(dbContext, workspaceId).GetPostAsync(post.Id);

        Assert.NotNull(result);
        Assert.Equal(500, result!.Impressions);
        Assert.Equal(50, result.Likes);
        Assert.Null(result.Clicks);
    }

    // ---------------------------------------------------------------------
    // Snapshot selection
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Reads_use_the_latest_snapshot_not_the_stale_one()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        var post = SeedPublishedPost(dbContext, workspaceId, Anchor);

        SeedMetric(dbContext, workspaceId, post.Id, Anchor.AddHours(1), 100, 10, 1, 1, 5);
        SeedMetric(dbContext, workspaceId, post.Id, Anchor.AddHours(6), 400, 40, 4, 4, 20);
        SeedMetric(dbContext, workspaceId, post.Id, Anchor.AddHours(24), 1_000, 100, 10, 10, 50);

        var service = CreateService(dbContext, workspaceId);
        var overview = await service.GetOverviewAsync(new AnalyticsQuery());
        var single = await service.GetPostAsync(post.Id);

        Assert.Equal(1_000, overview.Impressions);
        Assert.Equal(1_000, single!.Impressions);
        Assert.Equal(Anchor.AddHours(24), single.RecordedAt);
    }

    /// <summary>
    /// Two snapshots recorded at the same instant must resolve the same way on
    /// every call, otherwise the overview and the per-post read could disagree.
    /// </summary>
    [Fact]
    public async Task Snapshots_recorded_at_the_same_instant_resolve_deterministically()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        var post = SeedPublishedPost(dbContext, workspaceId, Anchor);

        // Fixed ids so the documented tie-break (greater id wins) is knowable.
        var lower = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var higher = Guid.Parse("00000000-0000-0000-0000-000000000002");
        SeedMetric(dbContext, workspaceId, post.Id, Anchor, 111, 11, 1, 1, 1, id: lower);
        SeedMetric(dbContext, workspaceId, post.Id, Anchor, 999, 99, 9, 9, 9, id: higher);

        var service = CreateService(dbContext, workspaceId);
        var first = await service.GetPostAsync(post.Id);
        var second = await service.GetPostAsync(post.Id);

        Assert.NotNull(first);
        Assert.Equal(999, first!.Impressions);
        Assert.Equal(first.Impressions, second!.Impressions);
    }

    [Fact]
    public async Task A_post_keeps_its_own_latest_snapshot()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();

        var first = SeedPublishedPost(dbContext, workspaceId, Anchor);
        var second = SeedPublishedPost(dbContext, workspaceId, Anchor.AddDays(1));

        // The second post carries the newer snapshot, but only for itself.
        SeedMetric(dbContext, workspaceId, first.Id, Anchor, 1_000, 100, 10, 10, 50);
        SeedMetric(dbContext, workspaceId, second.Id, Anchor.AddDays(2), 500, 50, 5, 5, 25);

        var posts = await CreateService(dbContext, workspaceId).GetPostsAsync(new AnalyticsQuery());

        Assert.Equal(1_000, posts.Single(post => post.ContentPostId == first.Id).Impressions);
        Assert.Equal(500, posts.Single(post => post.ContentPostId == second.Id).Impressions);
    }

    // ---------------------------------------------------------------------
    // Date filtering
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Date_bounds_are_inclusive_on_both_ends()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();

        var before = SeedPublishedPost(dbContext, workspaceId, Anchor.AddDays(-1));
        var startEdge = SeedPublishedPost(dbContext, workspaceId, Anchor);
        var endEdge = SeedPublishedPost(dbContext, workspaceId, Anchor.AddDays(1));
        var after = SeedPublishedPost(dbContext, workspaceId, Anchor.AddDays(2));

        var result = await CreateService(dbContext, workspaceId)
            .GetPostsAsync(new AnalyticsQuery(From: Anchor, To: Anchor.AddDays(1)));

        var ids = result.Select(post => post.ContentPostId).ToList();
        Assert.Contains(startEdge.Id, ids);
        Assert.Contains(endEdge.Id, ids);
        Assert.DoesNotContain(before.Id, ids);
        Assert.DoesNotContain(after.Id, ids);
    }

    /// <summary>
    /// A post is often written long before it goes out. Filtering on creation
    /// would place a post published today inside a period that ended last
    /// month.
    /// </summary>
    [Fact]
    public async Task Date_filtering_uses_published_at_not_created_at()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        var post = SeedPublishedPost(dbContext, workspaceId, Anchor);
        post.CreatedAt = Anchor.AddDays(-200);
        post.ScheduledAt = Anchor.AddDays(-1);
        dbContext.SaveChanges();

        var service = CreateService(dbContext, workspaceId);
        var period = new AnalyticsQuery(From: Anchor, To: Anchor);

        Assert.Equal(1, (await service.GetOverviewAsync(period)).PostsPublished);
        Assert.Single(await service.GetPostsAsync(period));
    }

    [Fact]
    public async Task An_unbounded_query_covers_all_published_posts()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();

        SeedPublishedPost(dbContext, workspaceId, Anchor.AddYears(-2));
        SeedPublishedPost(dbContext, workspaceId, Anchor);
        SeedPublishedPost(dbContext, workspaceId, Anchor.AddYears(2));

        var result = await CreateService(dbContext, workspaceId).GetOverviewAsync(new AnalyticsQuery());

        Assert.Equal(3, result.PostsPublished);
        Assert.Null(result.Period.From);
        Assert.Null(result.Period.To);
    }

    [Fact]
    public async Task The_response_reports_the_period_that_was_applied()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        var from = Anchor.AddDays(-7);

        var result = await CreateService(dbContext, workspaceId)
            .GetOverviewAsync(new AnalyticsQuery(From: from, To: Anchor));

        Assert.Equal(from, result.Period.From);
        Assert.Equal(Anchor, result.Period.To);
    }

    [Fact]
    public async Task A_period_matching_nothing_returns_an_empty_result_not_an_error()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        SeedWithMetrics(dbContext, workspaceId, Anchor, 100, 10, 1, 1, 5);

        var service = CreateService(dbContext, workspaceId);
        var future = new AnalyticsQuery(From: Anchor.AddDays(30));

        Assert.Empty(await service.GetPostsAsync(future));

        var overview = await service.GetOverviewAsync(future);
        Assert.Equal(0, overview.PostsPublished);
        Assert.Null(overview.Impressions);
    }

    // ---------------------------------------------------------------------
    // Platform filtering
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Platform_filter_restricts_posts_and_totals()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();

        SeedWithMetrics(dbContext, workspaceId, Anchor, 1_000, 100, 10, 10, 50, platform: ContentPlatform.LinkedIn);
        SeedWithMetrics(dbContext, workspaceId, Anchor, 300, 30, 3, 3, 15, platform: ContentPlatform.X);

        var service = CreateService(dbContext, workspaceId);
        var linkedIn = await service.GetOverviewAsync(new AnalyticsQuery(Platform: ContentPlatform.LinkedIn));
        var x = await service.GetOverviewAsync(new AnalyticsQuery(Platform: ContentPlatform.X));
        var linkedInPosts = await service.GetPostsAsync(new AnalyticsQuery(Platform: ContentPlatform.LinkedIn));

        Assert.Equal(1_000, linkedIn.Impressions);
        Assert.Equal(1, linkedIn.PostsPublished);
        Assert.Equal(300, x.Impressions);
        Assert.Equal(1, x.PostsPublished);
        Assert.Equal("LinkedIn", Assert.Single(linkedInPosts).Platform);
    }

    [Fact]
    public async Task Platform_and_period_filters_combine()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();

        var wanted = SeedPublishedPost(dbContext, workspaceId, Anchor, platform: ContentPlatform.X);
        SeedPublishedPost(dbContext, workspaceId, Anchor, platform: ContentPlatform.LinkedIn);
        SeedPublishedPost(dbContext, workspaceId, Anchor.AddDays(30), platform: ContentPlatform.X);

        var result = await CreateService(dbContext, workspaceId).GetPostsAsync(new AnalyticsQuery(
            Platform: ContentPlatform.X,
            From: Anchor.AddDays(-1),
            To: Anchor.AddDays(1)));

        Assert.Equal([wanted.Id], result.Select(post => post.ContentPostId));
    }

    // ---------------------------------------------------------------------
    // Determinism
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Posts_are_ordered_newest_first()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();

        var oldest = SeedPublishedPost(dbContext, workspaceId, Anchor);
        var newest = SeedPublishedPost(dbContext, workspaceId, Anchor.AddDays(2));
        var middle = SeedPublishedPost(dbContext, workspaceId, Anchor.AddDays(1));

        var result = await CreateService(dbContext, workspaceId).GetPostsAsync(new AnalyticsQuery());

        Assert.Equal([newest.Id, middle.Id, oldest.Id], result.Select(post => post.ContentPostId));
    }

    [Fact]
    public async Task Posts_published_at_the_same_instant_keep_a_stable_order_across_calls()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();

        for (var index = 0; index < 8; index++)
        {
            SeedPublishedPost(dbContext, workspaceId, Anchor);
        }

        var service = CreateService(dbContext, workspaceId);
        var first = await service.GetPostsAsync(new AnalyticsQuery());
        var second = await service.GetPostsAsync(new AnalyticsQuery());
        var third = await service.GetPostsAsync(new AnalyticsQuery());

        var expected = first.Select(post => post.ContentPostId).ToList();
        Assert.Equal(8, expected.Count);
        Assert.Equal(expected, second.Select(post => post.ContentPostId));
        Assert.Equal(expected, third.Select(post => post.ContentPostId));
        // PublishedAt ties, so the documented tie-break is id ascending.
        Assert.Equal(expected.OrderBy(id => id), expected);
    }

    [Fact]
    public async Task Repeated_overview_calls_return_identical_values()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();

        for (var index = 0; index < 5; index++)
        {
            SeedWithMetrics(dbContext, workspaceId, Anchor.AddDays(index), 100, 10, 1, 1, 5);
        }

        var service = CreateService(dbContext, workspaceId);
        var query = new AnalyticsQuery(Platform: ContentPlatform.LinkedIn, From: Anchor, To: Anchor.AddDays(10));

        var first = await service.GetOverviewAsync(query);
        var second = await service.GetOverviewAsync(query);

        Assert.Equal(first.PostsPublished, second.PostsPublished);
        Assert.Equal(first.Impressions, second.Impressions);
        Assert.Equal(first.Likes, second.Likes);
        Assert.Equal(first.EngagementRate, second.EngagementRate);
    }

    // ---------------------------------------------------------------------
    // Single post
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Get_post_returns_null_for_an_unknown_id()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();

        var result = await CreateService(dbContext, workspaceId).GetPostAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task Get_post_echoes_the_platform_and_publication_time()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        var post = SeedPublishedPost(dbContext, workspaceId, Anchor, platform: ContentPlatform.X);

        var result = await CreateService(dbContext, workspaceId).GetPostAsync(post.Id);

        Assert.NotNull(result);
        Assert.Equal(post.Id, result!.ContentPostId);
        Assert.Equal("X", result.Platform);
        Assert.Equal(Anchor, result.PublishedAt);
    }

    // ---------------------------------------------------------------------
    // Authorization
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Reads_require_an_authenticated_workspace()
    {
        await using var dbContext = CreateDbContext();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CreateService(dbContext, null).GetOverviewAsync(new AnalyticsQuery()));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CreateService(dbContext, null).GetPostsAsync(new AnalyticsQuery()));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CreateService(dbContext, null).GetPostAsync(Guid.NewGuid()));
    }

    // ---------------------------------------------------------------------
    // Calculator
    // ---------------------------------------------------------------------

    [Fact]
    public void Engagement_rate_is_null_when_impressions_are_unknown_or_zero()
    {
        Assert.Null(AnalyticsCalculator.EngagementRate(10, 5, 5, null));
        Assert.Null(AnalyticsCalculator.EngagementRate(10, 5, 5, 0));
    }

    [Fact]
    public void Engagement_rate_treats_unreported_numerators_as_zero()
    {
        // Impressions are known, so a rate exists even when no engagement
        // metric was reported.
        Assert.Equal(0m, AnalyticsCalculator.EngagementRate(null, null, null, 1_000));
    }

    [Theory]
    [InlineData(1, 300, 0.33)]
    [InlineData(2, 300, 0.67)]
    [InlineData(1, 8, 12.5)]
    [InlineData(3, 3, 100.0)]
    public void Engagement_rate_rounds_to_two_places_away_from_zero(
        long engaged,
        long impressions,
        double expected)
    {
        Assert.Equal(
            (decimal)expected,
            AnalyticsCalculator.EngagementRate(engaged, 0, 0, impressions));
    }

    [Fact]
    public void Sum_is_null_only_when_nothing_reported_the_metric()
    {
        Assert.Null(AnalyticsCalculator.Sum([]));
        Assert.Null(AnalyticsCalculator.Sum([null, null]));
        Assert.Equal(0, AnalyticsCalculator.Sum([0, 0]));
        Assert.Equal(0, AnalyticsCalculator.Sum([null, 0, null]));
        Assert.Equal(30, AnalyticsCalculator.Sum([10, null, 20]));
    }

    [Fact]
    public void Sum_does_not_overflow_on_large_metrics()
    {
        Assert.Equal(4_000_000_000_000L, AnalyticsCalculator.Sum([2_000_000_000_000L, 2_000_000_000_000L]));
    }

    // ---------------------------------------------------------------------
    // Pillar filtering
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Pillar_filter_keeps_only_posts_whose_idea_belongs_to_that_pillar()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        var education = SeedPillar(dbContext, workspaceId, "Education").Id;
        var stories = SeedPillar(dbContext, workspaceId, "Stories").Id;

        SeedPublishedPost(dbContext, workspaceId, Anchor, pillarId: education);
        SeedPublishedPost(dbContext, workspaceId, Anchor, pillarId: stories);
        SeedPublishedPost(dbContext, workspaceId, Anchor);

        var result = await CreateService(dbContext, workspaceId)
            .GetPostsAsync(new AnalyticsQuery(PillarId: education));

        var post = Assert.Single(result);
        Assert.Equal(education, post.ContentPillarId);
    }

    [Fact]
    public async Task Pillar_filter_also_narrows_the_overview_totals()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        var education = SeedPillar(dbContext, workspaceId, "Education").Id;

        SeedWithMetrics(dbContext, workspaceId, Anchor, 1_000, 100, 10, 5, 20, pillarId: education);
        SeedWithMetrics(dbContext, workspaceId, Anchor, 9_000, 900, 90, 90, 900);

        var result = await CreateService(dbContext, workspaceId)
            .GetOverviewAsync(new AnalyticsQuery(PillarId: education));

        Assert.Equal(1, result.PostsPublished);
        Assert.Equal(1_000, result.Impressions);
    }

    [Fact]
    public async Task Pillar_filter_does_not_leak_another_workspaces_pillar()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        var foreignPillar = SeedPillar(dbContext, Guid.NewGuid(), "Secret").Id;

        SeedPublishedPost(dbContext, workspaceId, Anchor);

        var result = await CreateService(dbContext, workspaceId)
            .GetPostsAsync(new AnalyticsQuery(PillarId: foreignPillar));

        Assert.Empty(result);
    }

    [Fact]
    public async Task Post_response_names_the_pillar_the_idea_was_attributed_to()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        var pillar = SeedPillar(dbContext, workspaceId, "Education");

        var post = SeedPublishedPost(dbContext, workspaceId, Anchor, pillarId: pillar.Id);

        var result = await CreateService(dbContext, workspaceId).GetPostAsync(post.Id);

        Assert.NotNull(result);
        Assert.Equal(pillar.Id, result!.ContentPillarId);
        Assert.Equal("Education", result.ContentPillarName);
    }

    [Fact]
    public async Task Post_response_reports_no_pillar_when_the_idea_was_never_attributed()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();

        var post = SeedPublishedPost(dbContext, workspaceId, Anchor);

        var result = await CreateService(dbContext, workspaceId).GetPostAsync(post.Id);

        Assert.NotNull(result);
        Assert.Null(result!.ContentPillarId);
        Assert.Null(result.ContentPillarName);
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private static AppDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static AnalyticsService CreateService(AppDbContext dbContext, Guid? workspaceId) =>
        new(CreateCurrentUser(workspaceId), new PublishedPostMetricsReader(dbContext));

    private static CurrentUserService CreateCurrentUser(Guid? workspaceId)
    {
        var claims = workspaceId is null
            ? new List<Claim>()
            :
            [
                new Claim("workspace_id", workspaceId.Value.ToString())
            ];

        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
            }
        };

        return new CurrentUserService(accessor);
    }

    private static ContentIdea AddIdea(AppDbContext dbContext, Guid workspaceId, Guid? pillarId = null)
      {
          var idea = new ContentIdea
          {
              Id = Guid.NewGuid(),
              WorkspaceId = workspaceId,
              ContentPillarId = pillarId,
            Status = ContentIdeaStatus.Approved,
            Platform = ContentPlatform.LinkedIn,
            Title = "Practical AI debugging",
            Topic = "AI for software developers",
            Angle = "A practical debugging framework",
            Hook = "What if AI helped you find the next useful debugging question?",
            Reason = "Developers need practical, grounded AI workflows.",
            SuggestedHashtags = "#AI",
            RelevanceScore = 90,
            EngagementPotentialScore = 80,
            BrandFitScore = 85,
            OverallScore = 85,
            CreatedAt = Anchor,
            UpdatedAt = Anchor
        };

        dbContext.ContentIdeas.Add(idea);
        dbContext.SaveChanges();
        return idea;
    }

    private static ContentPost SeedPublishedPost(
        AppDbContext dbContext,
        Guid workspaceId,
        DateTime publishedAt,
        ContentPlatform platform = ContentPlatform.LinkedIn,
        Guid? pillarId = null) =>
        SeedPost(
            dbContext,
            workspaceId,
            AddIdea(dbContext, workspaceId, pillarId),
            ContentPostStatus.Published,
            publishedAt,
            publishedAt,
            platform);

    private static ContentPillar SeedPillar(AppDbContext dbContext, Guid workspaceId, string name)
    {
        var pillar = new ContentPillar
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Name = name,
            Description = string.Empty,
            Weight = 25m,
            IsActive = true,
            CreatedAt = Anchor,
            UpdatedAt = Anchor
        };

        dbContext.ContentPillars.Add(pillar);
        dbContext.SaveChanges();
        return pillar;
    }

    private static ContentPost SeedPost(
        AppDbContext dbContext,
        Guid workspaceId,
        ContentIdea idea,
        ContentPostStatus status,
        DateTime? publishedAt,
        DateTime? createdAt,
        ContentPlatform platform = ContentPlatform.LinkedIn)
    {
        var post = new ContentPost
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            ContentIdeaId = idea.Id,
            Platform = platform,
            Status = status,
            Content = "A published post",
            Hook = "Existing hook",
            CallToAction = string.Empty,
            Hashtags = ["#AI"],
            ContentType = ContentType.Insight,
            StorytellingStructure = "Hook → Insight",
            PublishedAt = publishedAt,
            CreatedAt = createdAt ?? Anchor,
            UpdatedAt = createdAt ?? Anchor
        };

        dbContext.ContentPosts.Add(post);
        dbContext.SaveChanges();
        return post;
    }

    private static ContentPost SeedWithMetrics(
        AppDbContext dbContext,
        Guid workspaceId,
        DateTime publishedAt,
        long? impressions,
        long? likes,
        long? comments,
        long? shares,
        long? followersGained,
        long? clicks = null,
        long? profileVisits = null,
        ContentPlatform platform = ContentPlatform.LinkedIn,
        Guid? pillarId = null)
    {
        var post = SeedPublishedPost(dbContext, workspaceId, publishedAt, platform, pillarId);
        SeedMetric(dbContext, workspaceId, post.Id, publishedAt, impressions, likes, comments, shares, followersGained, clicks, profileVisits: profileVisits);
        return post;
    }

    private static ContentPostMetric SeedMetric(
        AppDbContext dbContext,
        Guid workspaceId,
        Guid contentPostId,
        DateTime recordedAt,
        long? impressions,
        long? likes,
        long? comments,
        long? shares,
        long? followersGained,
        long? clicks = null,
        Guid? id = null,
        long? profileVisits = null)
    {
        var metric = new ContentPostMetric
        {
            Id = id ?? Guid.NewGuid(),
            WorkspaceId = workspaceId,
            ContentPostId = contentPostId,
            Impressions = impressions,
            Likes = likes,
            Comments = comments,
            Shares = shares,
            Clicks = clicks,
            ProfileVisits = profileVisits,
            FollowersGained = followersGained,
            RecordedAt = recordedAt,
            CreatedAt = recordedAt
        };

        dbContext.ContentPostMetrics.Add(metric);
        dbContext.SaveChanges();
        return metric;
    }
}
