using Npgsql;

namespace EshopGuard.Jobs.Scheduling;

/// <summary>
/// Short work of a scheduler tick: enqueue jobs or return expired leases, never the long work itself. Runs only on the
/// worker that holds the tick lock.
/// </summary>
public interface IScheduledTask
{
    /// <summary>Name in logs.</summary>
    string Name { get; }

    /// <summary>How often the task runs on this worker; <see cref="TimeSpan.Zero"/> = every tick.</summary>
    TimeSpan Interval { get; }

    /// <summary>Runs the task; <see cref="ScheduledTaskContext.Transaction"/> is the tick's transaction (savepoint per task).</summary>
    Task RunAsync(ScheduledTaskContext context, CancellationToken ct);
}

/// <summary>Tick time (UTC) and the tick's transaction, which holds the scheduler lock until COMMIT.</summary>
public sealed record ScheduledTaskContext(DateTimeOffset Now, NpgsqlTransaction Transaction);
