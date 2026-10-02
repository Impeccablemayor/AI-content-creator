using Api.Application.AnalyticsSync;
using Api.Configuration;
using Api.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Api.Tests;

[Collection(ApplicationMetricsCollection.Name)]
public sealed class AnalyticsSyncProcessorTests
{
    [Fact]
    public async Task Nothing_due_makes_no_calls()
    {
        var store = new FakeAnalyticsSyncStore();
        var service = new CountingSyncService();
        var processor = CreateProcessor(store, service);

        var result = await processor.RunOnceAsync(CancellationToken.None);

        Assert.Equal(new AnalyticsSyncRunResult(0, 0, 0), result);
        Assert.Empty(service.Candidates);
    }

    /// <summary>
    /// The resync interval is the only thing keeping a post nobody can measure
    /// from being retried on every poll, so it is pushed to the scan rather than
    /// filtered in memory.
    /// </summary>
    [Fact]
    public async Task The_scan_is_asked_only_for_posts_older_than_the_resync_interval()
    {
        var store = new FakeAnalyticsSyncStore
        {
            Candidates = [Candidate()]
        };
        var processor = CreateProcessor(store, new CountingSyncService(), resyncIntervalHours: 6);

        await processor.RunOnceAsync(CancellationToken.None);

        var dueBefore = Assert.IsType<DateTime>(store.DueBefore);
        Assert.InRange(
            dueBefore,
            DateTime.UtcNow.AddHours(-6) - TimeSpan.FromMinutes(1),
            DateTime.UtcNow.AddHours(-6) + TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task The_batch_size_is_pushed_to_the_scan()
    {
        var store = new FakeAnalyticsSyncStore();
        var processor = CreateProcessor(store, new CountingSyncService(), batchSize: 7);

        await processor.RunOnceAsync(CancellationToken.None);

        Assert.Equal(7, store.Limit);
    }

    [Fact]
    public async Task Each_due_post_is_synced_and_tallied_by_outcome()
    {
        var store = new FakeAnalyticsSyncStore
        {
            Candidates = [Candidate(), Candidate(), Candidate()]
        };
        var service = new CountingSyncService();
        service.Outcomes.Enqueue(MetricsSyncOutcome.Synced);
        service.Outcomes.Enqueue(MetricsSyncOutcome.Failed);
        service.Outcomes.Enqueue(MetricsSyncOutcome.Skipped);
        var processor = CreateProcessor(store, service);

        var result = await processor.RunOnceAsync(CancellationToken.None);

        Assert.Equal(new AnalyticsSyncRunResult(3, 1, 1, 1), result);
        Assert.Equal(3, service.Candidates.Count);
    }

    /// <summary>
    /// One post failing in an unexpected way must not abandon the rest of the
    /// batch.
    /// </summary>
    [Fact]
    public async Task A_post_that_throws_does_not_abandon_the_batch()
    {
        var store = new FakeAnalyticsSyncStore
        {
            Candidates = [Candidate(), Candidate()]
        };
        var service = new CountingSyncService { ThrowsOnFirst = true };
        service.Outcomes.Enqueue(MetricsSyncOutcome.Synced);
        var processor = CreateProcessor(store, service);

        var result = await processor.RunOnceAsync(CancellationToken.None);

        Assert.Equal(new AnalyticsSyncRunResult(2, 1, 0, 1), result);
        Assert.Equal(2, service.Candidates.Count);
    }

    [Fact]
    public async Task A_cancelled_run_is_not_swallowed()
    {
        var store = new FakeAnalyticsSyncStore
        {
            Candidates = [Candidate()]
        };
        var service = new CountingSyncService { Throws = new OperationCanceledException() };
        var processor = CreateProcessor(store, service);

        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            processor.RunOnceAsync(cancellation.Token));
    }

    [Fact]
    public async Task A_successful_sync_is_counted()
    {
        var store = new FakeAnalyticsSyncStore { Candidates = [Candidate()] };
        var processor = CreateProcessor(store, new CountingSyncService());

        var before = SyncAttemptCount("synced", "none");

        await processor.RunOnceAsync(CancellationToken.None);

        Assert.Equal(before + 1, SyncAttemptCount("synced", "none"));
    }

    [Fact]
    public async Task A_failed_sync_is_counted_with_its_reason()
    {
        // A revoked credential and a provider outage both fail, and they are not
        // the same incident.
        var store = new FakeAnalyticsSyncStore { Candidates = [Candidate()] };
        var service = new CountingSyncService();
        service.Outcomes.Enqueue(MetricsSyncOutcome.Failed);
        service.Reasons.Enqueue(MetricsSyncFailureReason.CredentialsExpired);
        var processor = CreateProcessor(store, service);

        var before = SyncAttemptCount("failed", "credentials_expired");

        await processor.RunOnceAsync(CancellationToken.None);

        Assert.Equal(before + 1, SyncAttemptCount("failed", "credentials_expired"));
    }

    [Fact]
    public async Task A_sync_that_threw_is_counted_as_a_failure_without_a_reason()
    {
        var store = new FakeAnalyticsSyncStore { Candidates = [Candidate()] };
        var processor = CreateProcessor(
            store,
            new CountingSyncService { Throws = new InvalidOperationException("boom") });

        var before = SyncAttemptCount("failed", "none");

        await processor.RunOnceAsync(CancellationToken.None);

        Assert.Equal(before + 1, SyncAttemptCount("failed", "none"));
    }

    [Fact]
    public async Task Every_candidate_in_a_batch_is_counted_separately()
    {
        var store = new FakeAnalyticsSyncStore
        {
            Candidates = [Candidate(), Candidate(), Candidate()]
        };
        var processor = CreateProcessor(store, new CountingSyncService());

        var before = SyncAttemptCount("synced", "none");

        await processor.RunOnceAsync(CancellationToken.None);

        Assert.Equal(before + 3, SyncAttemptCount("synced", "none"));
    }

    [Fact]
    public async Task A_scan_with_nothing_due_records_no_attempt()
    {
        // A worker that found nothing did not try and fail to read metrics, and
        // counting it as a failure would make an idle hour look like an outage.
        var processor = CreateProcessor(
            new FakeAnalyticsSyncStore { Candidates = [] },
            new CountingSyncService());

        var before = SyncAttemptCount("synced", "none");

        await processor.RunOnceAsync(CancellationToken.None);

        Assert.Equal(before, SyncAttemptCount("synced", "none"));
    }

    private static long SyncAttemptCount(string outcome, string reason) =>
        MetricsScrape.CounterOrZero(
            "mayorx_analytics_sync_attempts_total",
            ("outcome", outcome),
            ("reason", reason));

    private static AnalyticsSyncProcessor CreateProcessor(
        IAnalyticsSyncStore store,
        IPublishedPostMetricsSyncService service,
        int batchSize = 10,
        int resyncIntervalHours = 24) =>
        new(
            store,
            service,
            Options.Create(new AnalyticsSyncOptions
            {
                PollIntervalSeconds = 300,
                BatchSize = batchSize,
                ResyncIntervalHours = resyncIntervalHours
            }),
            NullLogger<AnalyticsSyncProcessor>.Instance);

    private static MetricsSyncCandidate Candidate() =>
        new(
            Guid.NewGuid(),
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            ContentPlatform.X,
            "external-1",
            DateTime.UtcNow.AddDays(-1));

    private sealed class CountingSyncService : IPublishedPostMetricsSyncService
    {
        public List<MetricsSyncCandidate> Candidates { get; } = [];

        public Queue<MetricsSyncOutcome> Outcomes { get; } = new();

        public Queue<MetricsSyncFailureReason?> Reasons { get; } = new();

        public bool ThrowsOnFirst { get; init; }

        public Exception? Throws { get; init; }

        public Task<MetricsSyncAttemptResult> SyncAsync(
            MetricsSyncCandidate candidate,
            CancellationToken cancellationToken)
        {
            Candidates.Add(candidate);

            if (Throws is not null)
            {
                throw Throws;
            }

            if (ThrowsOnFirst && Candidates.Count == 1)
            {
                throw new InvalidOperationException("boom");
            }

            return Task.FromResult(new MetricsSyncAttemptResult(
                candidate.ContentPostId,
                Outcomes.Count > 0 ? Outcomes.Dequeue() : MetricsSyncOutcome.Synced,
                Reasons.Count > 0 ? Reasons.Dequeue() : null,
                null));
        }
    }

    private sealed class FakeAnalyticsSyncStore : IAnalyticsSyncStore
    {
        public IReadOnlyList<MetricsSyncCandidate> Candidates { get; init; } = [];

        public DateTime? DueBefore { get; private set; }

        public int Limit { get; private set; }

        public Task<IReadOnlyList<MetricsSyncCandidate>> FindDueAsync(
            DateTime dueBefore,
            int limit,
            CancellationToken cancellationToken)
        {
            DueBefore = dueBefore;
            Limit = limit;

            return Task.FromResult(Candidates);
        }

        public Task<IReadOnlyList<MetricsSyncAccountCandidate>> FindAccountsAsync(
            Guid workspaceId,
            ContentPlatform platform,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<MetricsSyncAccountCandidate>>([]);

        public Task<bool> TryRecordAttemptAsync(
            Guid contentPostId,
            DateTime attemptedAt,
            CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task AddSnapshotAsync(
            ContentPostMetricSnapshot snapshot,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
