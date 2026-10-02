using Api.Application.AnalyticsSync;
using Api.Application.Scheduling;
using Api.Configuration;
using Api.Infrastructure.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using worker;

namespace Api.Tests;

/// <summary>
/// A worker run that throws has to be visible. The loop swallows the exception and
/// retries, which is right for a background poll and useless to an operator, so
/// the failure is counted as well as logged.
/// </summary>
/// <remarks>
/// The workers are started through the host's own entry point rather than a method
/// called directly, because the recording lives in the loop and the loop is what
/// decides whether a throw is a failure or a shutdown.
/// </remarks>
[Collection(ApplicationMetricsCollection.Name)]
public sealed class WorkerPollMetricsTests
{
    [Fact]
    public async Task A_scheduled_post_scan_that_completes_is_counted_as_a_succeeded_run()
    {
        var before = PollCount("scheduled_post_scan", "succeeded");

        await using var harness = WorkerHarness.Starting(
            pollIntervalSeconds: OnePoll,
            scheduledPost: new StubScheduledPostProcessor());

        await harness.StartAsync();
        await WaitForAsync(() => PollCount("scheduled_post_scan", "succeeded") == before + 1);

        Assert.Equal(before + 1, PollCount("scheduled_post_scan", "succeeded"));
    }

    [Fact]
    public async Task A_scheduled_post_scan_that_throws_is_counted_as_a_failed_run()
    {
        var before = PollCount("scheduled_post_scan", "failed");

        await using var harness = WorkerHarness.Starting(
            pollIntervalSeconds: OnePoll,
            scheduledPost: new ThrowingScheduledPostProcessor());

        await harness.StartAsync();
        await WaitForAsync(() => PollCount("scheduled_post_scan", "failed") == before + 1);

        Assert.Equal(before + 1, PollCount("scheduled_post_scan", "failed"));
    }

    [Fact]
    public async Task A_failed_scheduled_post_scan_does_not_also_count_as_a_succeeded_one()
    {
        // One run, one count. Two series moving for a single poll would make a
        // failure rate nobody could trust.
        var succeededBefore = PollCount("scheduled_post_scan", "succeeded");
        var failedBefore = PollCount("scheduled_post_scan", "failed");

        await using var harness = WorkerHarness.Starting(
            pollIntervalSeconds: OnePoll,
            scheduledPost: new ThrowingScheduledPostProcessor());

        await harness.StartAsync();
        await WaitForAsync(() => PollCount("scheduled_post_scan", "failed") == failedBefore + 1);

        Assert.Equal(succeededBefore, PollCount("scheduled_post_scan", "succeeded"));
    }

    [Fact]
    public async Task An_analytics_sync_scan_that_completes_is_counted_as_a_succeeded_run()
    {
        var before = PollCount("analytics_sync_scan", "succeeded");

        await using var harness = WorkerHarness.Starting(
            pollIntervalSeconds: OnePoll,
            analyticsSync: new StubAnalyticsSyncProcessor());

        await harness.StartAsync();
        await WaitForAsync(() => PollCount("analytics_sync_scan", "succeeded") == before + 1);

        Assert.Equal(before + 1, PollCount("analytics_sync_scan", "succeeded"));
    }

    [Fact]
    public async Task An_analytics_sync_scan_that_throws_is_counted_as_a_failed_run()
    {
        var before = PollCount("analytics_sync_scan", "failed");

        await using var harness = WorkerHarness.Starting(
            pollIntervalSeconds: OnePoll,
            analyticsSync: new ThrowingAnalyticsSyncProcessor());

        await harness.StartAsync();
        await WaitForAsync(() => PollCount("analytics_sync_scan", "failed") == before + 1);

        Assert.Equal(before + 1, PollCount("analytics_sync_scan", "failed"));
    }

    [Fact]
    public async Task A_worker_keeps_counting_each_poll_after_a_failure()
    {
        // The loop is meant to retry, so the count has to keep moving rather than
        // record one failure for an outage that lasts an hour.
        var before = PollCount("scheduled_post_scan", "failed");

        await using var harness = WorkerHarness.Starting(
            pollIntervalSeconds: 1,
            scheduledPost: new ThrowingScheduledPostProcessor());

        await harness.StartAsync();
        await WaitForAsync(
            () => PollCount("scheduled_post_scan", "failed") >= before + 2,
            TimeSpan.FromSeconds(30));

        Assert.True(
            PollCount("scheduled_post_scan", "failed") >= before + 2,
            "Two failing polls should have been counted as two failures.");
    }

    /// <summary>Long enough that a started worker polls exactly once.</summary>
    private const int OnePoll = 3600;

    private static long PollCount(string operation, string outcome) =>
        MetricsScrape.CounterOrZero(
            "mayorx_worker_poll_runs_total",
            ("operation", operation),
            ("outcome", outcome));

    private static async Task WaitForAsync(Func<bool> condition, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));

        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(25);
        }

        Assert.True(condition(), "The worker did not record the poll within the timeout.");
    }

    private sealed class WorkerHarness : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly List<BackgroundService> _workers = [];

        public static WorkerHarness Starting(
            int pollIntervalSeconds,
            IScheduledPostProcessor? scheduledPost = null,
            IAnalyticsSyncProcessor? analyticsSync = null)
        {
            var services = new ServiceCollection();

            if (scheduledPost is not null)
            {
                services.AddSingleton(scheduledPost);
            }

            if (analyticsSync is not null)
            {
                services.AddSingleton(analyticsSync);
            }

            var harness = new WorkerHarness(services.BuildServiceProvider());
            var scopeFactory = harness._provider.GetRequiredService<IServiceScopeFactory>();

            if (scheduledPost is not null)
            {
                harness._workers.Add(new ScheduledPostWorkerService(
                    scopeFactory,
                    Options.Create(new SchedulingOptions
                    {
                        PollIntervalSeconds = pollIntervalSeconds,
                        BatchSize = 25,
                        ClaimExpiryMinutes = 15
                    }),
                    NullLogger<ScheduledPostWorkerService>.Instance));
            }

            if (analyticsSync is not null)
            {
                harness._workers.Add(new AnalyticsSyncWorkerService(
                    scopeFactory,
                    Options.Create(new AnalyticsSyncOptions
                    {
                        PollIntervalSeconds = pollIntervalSeconds,
                        BatchSize = 10,
                        ResyncIntervalHours = 24
                    }),
                    NullLogger<AnalyticsSyncWorkerService>.Instance));
            }

            return harness;
        }

        private WorkerHarness(ServiceProvider provider) => _provider = provider;

        public async Task StartAsync()
        {
            foreach (var worker in _workers)
            {
                await worker.StartAsync(CancellationToken.None);
            }
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var worker in _workers)
            {
                await worker.StopAsync(CancellationToken.None);
            }

            await _provider.DisposeAsync();
        }
    }

    private sealed class StubScheduledPostProcessor : IScheduledPostProcessor
    {
        public Task<ScheduledPostRunResult> RunOnceAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new ScheduledPostRunResult(0, 0, 0));
    }

    private sealed class ThrowingScheduledPostProcessor : IScheduledPostProcessor
    {
        public Task<ScheduledPostRunResult> RunOnceAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The database was unreachable.");
    }

    private sealed class StubAnalyticsSyncProcessor : IAnalyticsSyncProcessor
    {
        public Task<AnalyticsSyncRunResult> RunOnceAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new AnalyticsSyncRunResult(0, 0, 0));
    }

    private sealed class ThrowingAnalyticsSyncProcessor : IAnalyticsSyncProcessor
    {
        public Task<AnalyticsSyncRunResult> RunOnceAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The database was unreachable.");
    }
}