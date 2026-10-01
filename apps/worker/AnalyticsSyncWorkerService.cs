using Api.Application.AnalyticsSync;
using Api.Configuration;
using Microsoft.Extensions.Options;

namespace worker;

/// Orchestration only. The resync rules live in the application layer so the
/// worker never duplicates them.
public sealed class AnalyticsSyncWorkerService(
    IServiceScopeFactory scopeFactory,
    IOptions<AnalyticsSyncOptions> options,
    ILogger<AnalyticsSyncWorkerService> logger) : BackgroundService
{
    private readonly AnalyticsSyncOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pollInterval = TimeSpan.FromSeconds(_options.PollIntervalSeconds);

        logger.LogInformation(
            "Analytics sync worker started. Polling every {PollIntervalSeconds}s with a batch " +
            "size of {BatchSize} and a resync interval of {ResyncIntervalHours} hours. Published " +
            "posts are measured through their workspace's connected account.",
            _options.PollIntervalSeconds,
            _options.BatchSize,
            _options.ResyncIntervalHours);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                // Never let a transient failure stop the poll loop.
                logger.LogError(
                    exception,
                    "Analytics sync scan failed. The next poll will retry.");
            }

            try
            {
                await Task.Delay(pollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("Analytics sync worker stopped.");
    }

    internal async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var processor = scope.ServiceProvider
            .GetRequiredService<IAnalyticsSyncProcessor>();

        await processor.RunOnceAsync(cancellationToken);
    }
}
