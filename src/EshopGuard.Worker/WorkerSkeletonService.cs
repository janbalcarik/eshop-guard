using Microsoft.Extensions.Options;

namespace EshopGuard.Worker;

/// <summary>
/// Skeleton of the worker: announces itself, waits for shutdown and reports it. Job handling comes with change 4.
/// </summary>
public sealed class WorkerSkeletonService(IOptions<WorkerOptions> options, ILogger<WorkerSkeletonService> logger) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("worker.started {WorkerId}", options.Value.EffectiveId);
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutdown requested.
        }
    }

    /// <inheritdoc />
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
        logger.LogInformation("worker.stopped {WorkerId}", options.Value.EffectiveId);
    }
}
