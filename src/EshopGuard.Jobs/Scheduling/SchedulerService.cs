using EshopGuard.Data.Connections;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;

namespace EshopGuard.Jobs.Scheduling;

/// <summary>
/// Scheduler inside every worker. Each tick runs in a transaction with <c>pg_try_advisory_xact_lock</c>: only the worker that
/// gets the lock works, the others skip the tick. Nothing is kept in memory but when a task last ran here, so a second
/// instance duplicates nothing and takes over in the next tick when the first one stops. The transaction lock also works
/// behind PgBouncer in transaction mode.
/// </summary>
public sealed class SchedulerService(
    EshopGuardDataSource dataSource,
    IEnumerable<IScheduledTask> tasks,
    IOptions<SchedulerOptions> options,
    TimeProvider time,
    ILogger<SchedulerService> logger) : BackgroundService
{
    /// <summary>Key of the tick lock: the ASCII bytes of "EGSCHED1".</summary>
    public const long LockKey = 0x4547_5343_4845_4431;

    private readonly IReadOnlyList<IScheduledTask> _tasks = [.. tasks];
    private readonly Dictionary<IScheduledTask, DateTimeOffset> _lastRun = [];

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("scheduler.disabled");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.TickSeconds), time);
        try
        {
            do
            {
                try
                {
                    await TickAsync(stoppingToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning("scheduler.tick_failed {ExceptionType}", ex.GetType().Name);
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    /// <summary>One tick; <c>false</c> when another worker holds the lock.</summary>
    internal async Task<bool> TickAsync(CancellationToken ct)
    {
        await using var connection = await dataSource.Source.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        await using (var tryLock = new NpgsqlCommand("SELECT pg_try_advisory_xact_lock(@key)", connection, transaction))
        {
            tryLock.Parameters.Add(new NpgsqlParameter("key", NpgsqlDbType.Bigint) { Value = LockKey });
            if (await tryLock.ExecuteScalarAsync(ct).ConfigureAwait(false) is not true)
            {
                await transaction.RollbackAsync(ct).ConfigureAwait(false);
                return false;
            }
        }

        var now = time.GetUtcNow();
        var context = new ScheduledTaskContext(now, transaction);
        foreach (var task in _tasks)
        {
            if (_lastRun.TryGetValue(task, out var last) && now - last < task.Interval)
            {
                continue;
            }

            // A failed task rolls back to its savepoint, so the next task still has a usable transaction.
            await transaction.SaveAsync("task", ct).ConfigureAwait(false);
            try
            {
                await task.RunAsync(context, ct).ConfigureAwait(false);
                await transaction.ReleaseAsync("task", ct).ConfigureAwait(false);
                _lastRun[task] = now;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                await transaction.RollbackAsync("task", ct).ConfigureAwait(false);
                logger.LogError("scheduler.task_failed {Task} {Code} {ExceptionType}", task.Name, CodeOf(ex), ex.GetType().Name);
            }
        }

        await transaction.CommitAsync(ct).ConfigureAwait(false);
        return true;
    }

    private static string? CodeOf(Exception ex) => ex switch
    {
        JobQueueException q => q.Code,
        PostgresException p => "db." + p.SqlState,
        _ => null,
    };
}
