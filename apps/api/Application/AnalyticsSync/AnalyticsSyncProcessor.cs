using Api.Configuration;
using Api.Infrastructure.Metrics;
using Microsoft.Extensions.Options;

namespace Api.Application.AnalyticsSync;

/// <summary>
/// Scans for published posts whose metrics are due and reads them.
/// </summary>
/// <remarks>
/// Deliberately stateless. A post is not claimed, because the only thing two
/// overlapping workers could both do is read the same post's metrics and each
/// write a snapshot; the newest snapshot wins on read, so the duplicate is
/// wasted work rather than wrong data. A lease here would have to expire before
/// a crashed worker's post could be retried, which is the same problem the
/// attempt timestamp already solves.
/// </remarks>
public sealed class AnalyticsSyncProcessor(
    IAnalyticsSyncStore store,
    IPublishedPostMetricsSyncService syncService,
    IOptions<AnalyticsSyncOptions> options,
    ILogger<AnalyticsSyncProcessor> logger) : IAnalyticsSyncProcessor
{
    private readonly AnalyticsSyncOptions _options = options.Value;

    public async Task<AnalyticsSyncRunResult> RunOnceAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var dueBefore = now - TimeSpan.FromHours(_options.ResyncIntervalHours);

        var due = await store.FindDueAsync(dueBefore, _options.BatchSize, cancellationToken);
        if (due.Count == 0)
        {
            logger.LogDebug("No published content posts are due for a metrics sync.");
            return new AnalyticsSyncRunResult(0, 0, 0);
        }

        var synced = 0;
        var skipped = 0;
        var failed = 0;

        foreach (var candidate in due)
        {
            // The catch is deliberate. One post failing in an unexpected way must
            // not abandon the rest of the batch, and its attempt is already
            // recorded, so it is retried on the next resync interval.
            try
            {
                var result = await syncService.SyncAsync(candidate, cancellationToken);
                switch (result.Outcome)
                {
                    case MetricsSyncOutcome.Synced:
                        synced++;
                        break;
                    case MetricsSyncOutcome.Skipped:
                        skipped++;
                        break;
                    case MetricsSyncOutcome.Failed:
                        failed++;
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"Unhandled metrics sync outcome {result.Outcome}.");
                }

                ApplicationMetrics.RecordAnalyticsSyncAttempt(
                    result.Outcome,
                    result.FailureReason);
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
                    "Syncing metrics for content post {ContentPostId} of workspace {WorkspaceId} " +
                    "threw an unexpected error. No snapshot was recorded and the post will be " +
                    "retried after the next resync interval.",
                    candidate.ContentPostId,
                    candidate.WorkspaceId);

                // Counted with no reason because the sync service never described
                // this one. The log above carries the exception.
                ApplicationMetrics.RecordAnalyticsSyncAttempt(
                    MetricsSyncOutcome.Failed,
                    reason: null);
            }
        }

        logger.LogInformation(
            "Metrics sync complete: {DueFound} due, {Synced} synced, {Skipped} skipped, " +
            "{Failed} failed.",
            due.Count,
            synced,
            skipped,
            failed);

        return new AnalyticsSyncRunResult(due.Count, synced, skipped, failed);
    }
}
