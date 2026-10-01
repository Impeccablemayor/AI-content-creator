using Api.Domain.Entities;
using Api.Domain.Enums;
using Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Api.Application.Analytics;

/// <summary>
/// A published content post paired with the snapshot that currently describes
/// it, or null when the post has never been measured.
/// </summary>
/// <remarks>
/// The snapshot is null for a post that has been published but not yet read
/// from a platform. That is a different state from a snapshot whose metrics are
/// all null, which means the platform was asked and reported nothing.
/// </remarks>
public sealed record MeasuredPost(ContentPost Post, ContentPostMetric? Latest);

/// <summary>
/// Reads published posts and their current performance snapshots for one
/// workspace.
/// </summary>
/// <remarks>
/// Both the analytics reporting endpoints and the learning loop need the same
/// two things: which published posts fall in a period, and what the most recent
/// measurement of each one says. Keeping that in one place is what stops the
/// two from disagreeing about what "the current metrics of a post" means.
/// </remarks>
public interface IPublishedPostMetricsReader
{
    /// <summary>
    /// Published posts matching the query, newest publication first, each with
    /// its current snapshot.
    /// </summary>
    Task<IReadOnlyList<MeasuredPost>> ReadAsync(
        Guid workspaceId,
        AnalyticsQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// One published post with its current snapshot, or null when the post does
    /// not exist in this workspace or is not published.
    /// </summary>
    Task<MeasuredPost?> ReadOneAsync(
        Guid workspaceId,
        Guid contentPostId,
        CancellationToken cancellationToken = default);
}

public sealed class PublishedPostMetricsReader(AppDbContext dbContext) : IPublishedPostMetricsReader
{
    public async Task<IReadOnlyList<MeasuredPost>> ReadAsync(
        Guid workspaceId,
        AnalyticsQuery query,
        CancellationToken cancellationToken)
    {
        var posts = QueryPublishedPosts(workspaceId, query);

        // PublishedAt can tie, so Id breaks the tie to keep paging stable
        // across identical calls.
        var ordered = await posts
            .OrderByDescending(post => post.PublishedAt)
            .ThenBy(post => post.Id)
            .ToListAsync(cancellationToken);

        var latest = await QueryLatestMetricsAsync(
            workspaceId,
            ordered.Select(post => post.Id).ToList(),
            cancellationToken);

        var byPostId = latest.ToDictionary(metric => metric.ContentPostId);

        return ordered
            .Select(post => new MeasuredPost(post, byPostId.GetValueOrDefault(post.Id)))
            .ToList();
    }

    public async Task<MeasuredPost?> ReadOneAsync(
        Guid workspaceId,
        Guid contentPostId,
        CancellationToken cancellationToken)
    {
        var post = await dbContext.ContentPosts.AsNoTracking()
            .Include(item => item.ContentIdea)
                .ThenInclude(idea => idea!.ContentPillar)
            .SingleOrDefaultAsync(
                item => item.Id == contentPostId && item.WorkspaceId == workspaceId,
                cancellationToken);

        // A post that exists but was never published is not a performance
        // result. Reporting it would imply metrics for something that has none.
        if (post is null || post.Status != ContentPostStatus.Published)
        {
            return null;
        }

        var latest = await QueryLatestMetricsAsync(workspaceId, [post.Id], cancellationToken);

        return new MeasuredPost(post, latest.SingleOrDefault());
    }

    private IQueryable<ContentPost> QueryPublishedPosts(Guid workspaceId, AnalyticsQuery query)
    {
        var posts = dbContext.ContentPosts.AsNoTracking()
            // Carried along so responses can name the pillar without a second
            // round of lookups. Always reference navigation, never a local.
            .Include(post => post.ContentIdea)
                .ThenInclude(idea => idea!.ContentPillar)
            .Where(post => post.WorkspaceId == workspaceId)
            .Where(post => post.Status == ContentPostStatus.Published);

        if (query.Platform is not null)
        {
            posts = posts.Where(post => post.Platform == query.Platform);
        }

        // Filtering on PublishedAt rather than CreatedAt: reach accumulates after
        // a post goes out, so a post is part of a period by when it was
        // published, not by when it was written.
        if (query.From is not null)
        {
            var from = query.From.Value;
            posts = posts.Where(post => post.PublishedAt >= from);
        }

        if (query.To is not null)
        {
            var to = query.To.Value;
            posts = posts.Where(post => post.PublishedAt <= to);
        }

        // A pillar is only known through the idea the post came from, so the
        // filter has to reach across that relationship rather than the post.
        if (query.PillarId is not null)
        {
            var pillarId = query.PillarId.Value;
            posts = posts.Where(post => post.ContentIdea.ContentPillarId == pillarId);
        }

        return posts;
    }

    /// <summary>
    /// Loads the current snapshot for each of the given posts.
    /// </summary>
    /// <remarks>
    /// Only the workspace filter and the post set are pushed to the database.
    /// Picking the newest row per post is done in memory because "order a group
    /// and take the first" is not translatable by EF Core, and a
    /// (postId, recordedAt) pair match would need a generated OR chain. The
    /// set reaching this point is already bounded to one workspace's published
    /// posts, so the reduction is small. See the Analytics scope note in the
    /// API specification for the test-coverage consequence.
    /// </remarks>
    private async Task<List<ContentPostMetric>> QueryLatestMetricsAsync(
        Guid workspaceId,
        IReadOnlyCollection<Guid> contentPostIds,
        CancellationToken cancellationToken)
    {
        if (contentPostIds.Count == 0)
        {
            return [];
        }

        var metrics = await dbContext.ContentPostMetrics.AsNoTracking()
            .Where(metric => metric.WorkspaceId == workspaceId)
            .Where(metric => contentPostIds.Contains(metric.ContentPostId))
            .ToListAsync(cancellationToken);

        return metrics
            .GroupBy(metric => metric.ContentPostId)
            .Select(group => group
                .OrderByDescending(metric => metric.RecordedAt)
                .ThenByDescending(metric => metric.Id)
                .First())
            .ToList();
    }
}
