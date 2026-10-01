using Api.Application.Scheduling;
using Api.Configuration;
using Microsoft.Extensions.Options;

namespace worker;

/// Orchestration only. All scheduling rules live in the application layer so the
/// worker never duplicates them.
public sealed class ScheduledPostWorkerService(
    IServiceScopeFactory scopeFactory,
    IOptions<SchedulingOptions> options,
    ILogger<ScheduledPostWorkerService> logger) : BackgroundService
{
    private readonly SchedulingOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pollInterval = TimeSpan.FromSeconds(_options.PollIntervalSeconds);

        logger.LogInformation(
            "Scheduled post worker started. Polling every {PollIntervalSeconds}s with a " +
            "batch size of {BatchSize} and a claim expiry of {ClaimExpiryMinutes} minutes. " +
            "Due posts are claimed and published through their workspace's connected " +
            "account.",
            _options.PollIntervalSeconds,
            _options.BatchSize,
            _options.ClaimExpiryMinutes);

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
                    "Scheduled post scan failed. The next poll will retry.");
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

        logger.LogInformation("Scheduled post worker stopped.");
    }

    internal async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var processor = scope.ServiceProvider
            .GetRequiredService<IScheduledPostProcessor>();

        await processor.RunOnceAsync(cancellationToken);
    }
}
