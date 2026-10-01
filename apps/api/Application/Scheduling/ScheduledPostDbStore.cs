using Api.Domain.Enums;
using Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Api.Application.Scheduling;

public sealed class ScheduledPostDbStore(AppDbContext dbContext) : IScheduledPostStore
{
    public async Task<IReadOnlyList<ScheduledPostCandidate>> FindDueAsync(
        DateTime now,
        int limit,
        CancellationToken cancellationToken)
    {
        return await dbContext.ContentPosts
            .AsNoTracking()
            .Where(post =>
                post.Status == Domain.Enums.ContentPostStatus.Scheduled &&
                post.ScheduledAt != null &&
                post.ScheduledAt <= now)
            .OrderBy(post => post.ScheduledAt)
            .Select(post => new ScheduledPostCandidate(
                post.Id,
                post.WorkspaceId,
                post.ContentIdeaId,
                post.Platform,
                post.Status,
                post.ScheduledAt,
                post.PublishClaimedAt))
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> TryClaimAsync(
        Guid contentPostId,
        DateTime now,
        TimeSpan claimExpiry,
        CancellationToken cancellationToken)
    {
        var claimExpiredBefore = now - claimExpiry;

        // Conditional update is the claim: the row is only written while it is
        // still Scheduled and either unclaimed or holding an expired claim, so
        // two workers racing on the same post cannot both win.
        var affected = await dbContext.ContentPosts
            .Where(post =>
                post.Id == contentPostId &&
                post.Status == Domain.Enums.ContentPostStatus.Scheduled &&
                (post.PublishClaimedAt == null || post.PublishClaimedAt <= claimExpiredBefore))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(post => post.PublishClaimedAt, now),
                cancellationToken);

        return affected == 1;
    }

    public async Task<ScheduledPostPayload?> GetPublishPayloadAsync(
        Guid contentPostId,
        CancellationToken cancellationToken) =>
        await dbContext.ContentPosts
            .AsNoTracking()
            .Where(post => post.Id == contentPostId)
            .Select(post => new ScheduledPostPayload(
                post.Id,
                post.WorkspaceId,
                post.Platform,
                post.Status,
                post.Content,
                post.CallToAction,
                post.Hashtags))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<PublishAccountCandidate>> FindPublishAccountsAsync(
        Guid workspaceId,
        ContentPlatform platform,
        CancellationToken cancellationToken) =>
        await dbContext.SocialAccounts
            .AsNoTracking()
            .Where(account =>
                account.WorkspaceId == workspaceId &&
                account.Platform == platform)
            .OrderByDescending(account => account.ConnectedAt)
            .Select(account => new PublishAccountCandidate(
                account.Id,
                account.WorkspaceId,
                account.Platform,
                account.PlatformAccountId,
                account.Status,
                account.AccessTokenEncrypted,
                account.TokenExpiresAt))
            .ToListAsync(cancellationToken);

    public async Task<bool> TryMarkPublishedAsync(
        Guid contentPostId,
        DateTime claimedAt,
        DateTime publishedAt,
        string? externalPostId,
        CancellationToken cancellationToken)
    {
        // Matching the exact claim timestamp means only the worker that claimed
        // the post can finalise it. A worker whose claim expired mid-flight
        // cannot overwrite the result of whoever took over.
        var affected = await dbContext.ContentPosts
            .Where(post =>
                post.Id == contentPostId &&
                post.Status == Domain.Enums.ContentPostStatus.Scheduled &&
                post.PublishClaimedAt == claimedAt)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(post => post.Status, ContentPostStatus.Published)
                    .SetProperty(post => post.PublishedAt, publishedAt)
                    .SetProperty(post => post.PublishedExternalPostId, externalPostId)
                    .SetProperty(post => post.PublishFailureReason, (PublishFailureReason?)null)
                    .SetProperty(post => post.PublishClaimedAt, (DateTime?)null)
                    .SetProperty(post => post.UpdatedAt, publishedAt),
                cancellationToken);

        return affected == 1;
    }

    public async Task<bool> TryRecordPublishFailureAsync(
        Guid contentPostId,
        DateTime claimedAt,
        PublishFailureReason reason,
        DateTime failedAt,
        CancellationToken cancellationToken)
    {
        var affected = await dbContext.ContentPosts
            .Where(post =>
                post.Id == contentPostId &&
                post.Status == Domain.Enums.ContentPostStatus.Scheduled &&
                post.PublishClaimedAt == claimedAt)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(post => post.PublishFailureReason, reason)
                    .SetProperty(post => post.UpdatedAt, failedAt),
                cancellationToken);

        return affected == 1;
    }
}
