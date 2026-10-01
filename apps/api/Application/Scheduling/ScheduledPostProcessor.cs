using Api.Application.Publishing;
using Api.Configuration;
using Microsoft.Extensions.Options;

namespace Api.Application.Scheduling;

public sealed class ScheduledPostProcessor(
    IScheduledPostStore store,
    IScheduledPostPublishService publishService,
    IOptions<SchedulingOptions> options,
    ILogger<ScheduledPostProcessor> logger) : IScheduledPostProcessor
{
    private readonly SchedulingOptions _options = options.Value;
    public async Task<ScheduledPostRunResult> RunOnceAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var claimExpiry = ScheduledPostClaimPolicy.ResolveClaimExpiry(
            TimeSpan.FromMinutes(_options.ClaimExpiryMinutes));

        var due = await store.FindDueAsync(now, _options.BatchSize, cancellationToken);
        if (due.Count == 0)
        {
            logger.LogDebug("No due scheduled content posts found.");
            return new ScheduledPostRunResult(0, 0, 0);
        }

        var claimed = 0;
        var skipped = 0;
        var published = 0;
        var failed = 0;

        foreach (var candidate in due)
        {
            if (!ScheduledPostClaimPolicy.CanBeClaimed(candidate, now, claimExpiry))
            {
                skipped++;
                logger.LogDebug(
                    "Skipping content post {ContentPostId} because it is already claimed.",
                    candidate.ContentPostId);
                continue;
            }

            if (!await store.TryClaimAsync(
                candidate.ContentPostId,
                now,
                claimExpiry,
                cancellationToken))
            {
                skipped++;
                logger.LogInformation(
                    "Content post {ContentPostId} was claimed by another worker.",
                    candidate.ContentPostId);
                continue;
            }

            claimed++;
            logger.LogInformation(
                "Claimed due content post {ContentPostId} for workspace {WorkspaceId}, " +
                "idea {ContentIdeaId}, platform {Platform}, scheduled at {ScheduledAt}.",
                candidate.ContentPostId,
                candidate.WorkspaceId,
                candidate.ContentIdeaId,
                candidate.Platform,
                candidate.ScheduledAt);

            // Publishing runs under the claim that was just taken. The claim
            // timestamp is passed through so only this worker can finalise the
            // post.
            //
            // The catch is deliberate. A post that throws in an unexpected way
            // must not abandon the rest of the batch, and its claim is already
            // recorded, so it becomes eligible again when the claim expires
            // rather than being stuck forever.
            try
            {
                var result = await publishService.PublishAsync(candidate, now, cancellationToken);
                switch (result.Outcome)
                {
                    case PublishOutcome.Published:
                        published++;
                        break;
                    case PublishOutcome.Failed:
                    case PublishOutcome.Skipped:
                        failed++;
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"Unhandled publish outcome {result.Outcome}.");
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                failed++;
                logger.LogError(
                    exception,
                    "Publishing content post {ContentPostId} for workspace {WorkspaceId} " +
                    "threw an unexpected error. The post remains Scheduled and will be " +
                    "retried when its claim expires.",
                    candidate.ContentPostId,
                    candidate.WorkspaceId);
            }
        }

        logger.LogInformation(
            "Scheduled post scan complete: {DueFound} due, {Claimed} claimed, " +
            "{Skipped} skipped, {Published} published, {Failed} not published.",
            due.Count,
            claimed,
            skipped,
            published,
            failed);

        return new ScheduledPostRunResult(due.Count, claimed, skipped, published, failed);
    }
}
