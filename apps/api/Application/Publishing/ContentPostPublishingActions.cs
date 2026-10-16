using Api.Application.AuditLogs;
using Api.Application.Authentication;
using Api.Application.ContentPosts;
using Api.Application.Scheduling;
using Api.Configuration;
using Api.Contracts.ContentPost;
using Api.Domain.Enums;
using Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Application.Publishing;

public sealed class ContentPostPublishingActions(
    AppDbContext dbContext,
    CurrentUserService currentUser,
    IAuditLogService auditLog,
    IContentPostService contentPostService,
    IScheduledPostStore scheduledPostStore,
    IScheduledPostPublishService publishService,
    IOptions<SchedulingOptions> schedulingOptions) : IContentPostPublishingActions
{
    private readonly TimeSpan _claimExpiry = ScheduledPostClaimPolicy.ResolveClaimExpiry(
        TimeSpan.FromMinutes(schedulingOptions.Value.ClaimExpiryMinutes));

    public async Task<PublishNowResponse?> PublishNowAsync(
        Guid contentPostId,
        CancellationToken cancellationToken)
    {
        var workspaceId = currentUser.WorkspaceId;
        var candidate = await dbContext.ContentPosts
            .AsNoTracking()
            .Where(post => post.Id == contentPostId && post.WorkspaceId == workspaceId)
            .Select(post => new ScheduledPostCandidate(
                post.Id,
                post.WorkspaceId,
                post.ContentIdeaId,
                post.Platform,
                post.Status,
                post.ScheduledAt,
                post.PublishClaimedAt))
            .SingleOrDefaultAsync(cancellationToken);

        if (candidate is null)
        {
            return null;
        }

        if (!ContentPostStatusTransitions.CanPublishImmediately(candidate.Status))
        {
            throw new InvalidContentPostTransitionException(
                $"Only approved or scheduled posts can be published immediately. This post is {candidate.Status}.");
        }

        var now = DateTime.UtcNow;
        if (!await scheduledPostStore.TryClaimForImmediatePublishAsync(
            contentPostId,
            workspaceId,
            now,
            _claimExpiry,
            cancellationToken))
        {
            throw new InvalidContentPostTransitionException(
                "This post is already being published or its status changed. Refresh and try again.");
        }

        var claimedCandidate = candidate with
        {
            Status = ContentPostStatus.Scheduled,
            ScheduledAt = now,
            PublishClaimedAt = now
        };
        var result = await publishService.PublishAsync(claimedCandidate, now, cancellationToken);

        if (result.Outcome == PublishOutcome.Published)
        {
            await auditLog.RecordAsync(
                AuditAction.Published,
                "ContentPost",
                contentPostId,
                new { status = candidate.Status.ToString() },
                new { status = ContentPostStatus.Published.ToString() },
                cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var post = await contentPostService.GetAsync(contentPostId, cancellationToken);
        if (post is null)
        {
            return null;
        }

        return new PublishNowResponse
        {
            Post = post,
            Outcome = result.Outcome.ToString(),
            FailureReason = result.FailureReason?.ToString(),
            ExternalPostId = result.ExternalPostId
        };
    }

    public async Task<ContentPostResponse?> CancelScheduleAsync(
        Guid contentPostId,
        CancellationToken cancellationToken)
    {
        var workspaceId = currentUser.WorkspaceId;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var post = await dbContext.ContentPosts
            .AsNoTracking()
            .Where(item => item.Id == contentPostId && item.WorkspaceId == workspaceId)
            .Select(item => new { item.Id, item.Status, item.ScheduledAt })
            .SingleOrDefaultAsync(cancellationToken);

        if (post is null)
        {
            return null;
        }

        if (!ContentPostStatusTransitions.CanCancelSchedule(post.Status))
        {
            throw new InvalidContentPostTransitionException(
                $"Only scheduled posts can have their schedule cancelled. This post is {post.Status}.");
        }

        var now = DateTime.UtcNow;
        if (!await scheduledPostStore.TryCancelScheduleAsync(
            contentPostId,
            workspaceId,
            now,
            _claimExpiry,
            cancellationToken))
        {
            throw new InvalidContentPostTransitionException(
                "This post is currently being published and cannot be cancelled. Refresh and try again.");
        }

        await auditLog.RecordAsync(
            AuditAction.ScheduleCancelled,
            "ContentPost",
            contentPostId,
            new
            {
                status = ContentPostStatus.Scheduled.ToString(),
                scheduledAt = post.ScheduledAt
            },
            new
            {
                status = ContentPostStatus.Approved.ToString(),
                scheduledAt = (DateTime?)null
            },
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await contentPostService.GetAsync(contentPostId, cancellationToken);
    }
}
