using Api.Domain.Entities;
using Api.Domain.Enums;
using Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Api.Application.AnalyticsSync;

public sealed class AnalyticsSyncDbStore(AppDbContext dbContext) : IAnalyticsSyncStore
{
    public async Task<IReadOnlyList<MetricsSyncCandidate>> FindDueAsync(
        DateTime dueBefore,
        int limit,
        CancellationToken cancellationToken) =>
        await dbContext.ContentPosts
            .AsNoTracking()
            .Where(post =>
                post.Status == ContentPostStatus.Published &&
                post.PublishedExternalPostId != null)
            // The attempt timestamp is the only backoff, so a post the platform
            // has no data for is retried on the interval rather than every poll.
            .Where(post => post.MetricsLastAttemptedAt == null || post.MetricsLastAttemptedAt <= dueBefore)
            .OrderBy(post => post.PublishedAt)
            .ThenBy(post => post.Id)
            .Select(post => new MetricsSyncCandidate(
                post.Id,
                post.WorkspaceId,
                post.Platform,
                post.PublishedExternalPostId!,
                post.PublishedAt))
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<MetricsSyncAccountCandidate>> FindAccountsAsync(
        Guid workspaceId,
        ContentPlatform platform,
        CancellationToken cancellationToken) =>
        await dbContext.SocialAccounts
            .AsNoTracking()
            .Where(account =>
                account.WorkspaceId == workspaceId &&
                account.Platform == platform)
            .OrderByDescending(account => account.ConnectedAt)
            .Select(account => new MetricsSyncAccountCandidate(
                account.Id,
                account.WorkspaceId,
                account.Platform,
                account.Status,
                account.AccessTokenEncrypted,
                account.TokenExpiresAt))
            .ToListAsync(cancellationToken);

    public async Task<bool> TryRecordAttemptAsync(
        Guid contentPostId,
        DateTime attemptedAt,
        CancellationToken cancellationToken)
    {
        // Guarded on the same conditions as the scan, so a post that stopped
        // being published, or lost its platform id, is never written to.
        var affected = await dbContext.ContentPosts
            .Where(post =>
                post.Id == contentPostId &&
                post.Status == ContentPostStatus.Published &&
                post.PublishedExternalPostId != null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(post => post.MetricsLastAttemptedAt, attemptedAt),
                cancellationToken);

        return affected == 1;
    }

    public async Task AddSnapshotAsync(
        ContentPostMetricSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        dbContext.ContentPostMetrics.Add(new ContentPostMetric
        {
            Id = Guid.NewGuid(),
            WorkspaceId = snapshot.WorkspaceId,
            ContentPostId = snapshot.ContentPostId,
            Impressions = snapshot.Impressions,
            Likes = snapshot.Likes,
            Comments = snapshot.Comments,
            Shares = snapshot.Shares,
            Clicks = snapshot.Clicks,
            ProfileVisits = snapshot.ProfileVisits,
            FollowersGained = snapshot.FollowersGained,
            // Recorded even when every metric is null. A successful read that
            // found nothing is a measurement, and collapsing it into "no
            // snapshot yet" would make the two indistinguishable to a reader.
            RecordedAt = snapshot.RecordedAt,
            CreatedAt = now
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
