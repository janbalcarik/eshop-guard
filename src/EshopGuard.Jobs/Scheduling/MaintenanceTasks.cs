using System.Globalization;
using EshopGuard.Data.Entities.Ops;
using EshopGuard.Jobs.Handlers;
using EshopGuard.Jobs.Queue;
using EshopGuard.Jobs.Workers;

namespace EshopGuard.Jobs.Scheduling;

/// <summary>Returns jobs whose lease expired (crashed or stuck worker), 500 per tick.</summary>
public sealed class LeaseReaperTask(IJobQueue queue) : IScheduledTask
{
    /// <inheritdoc />
    public string Name => "lease_reaper";

    /// <inheritdoc />
    public TimeSpan Interval => TimeSpan.Zero;

    /// <inheritdoc />
    public Task RunAsync(ScheduledTaskContext context, CancellationToken ct) => queue.RequeueExpiredLeasesAsync(500, ct);
}

/// <summary>Enqueues a system job once per UTC day (dedupe key with the date; trying every tick costs one insert attempt).</summary>
public abstract class DailySystemJobTask(IJobQueue queue) : IScheduledTask
{
    /// <summary>Kind of the daily job.</summary>
    protected abstract string Kind { get; }

    /// <inheritdoc />
    public string Name => Kind;

    /// <inheritdoc />
    public TimeSpan Interval => TimeSpan.Zero;

    /// <summary>Dedupe key for the day, e.g. <c>system.ensure_partitions:2026-10-01</c>.</summary>
    public static string DedupeKey(string kind, DateTimeOffset now) =>
        kind + ":" + now.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <inheritdoc />
    public Task RunAsync(ScheduledTaskContext context, CancellationToken ct) => queue.EnqueueAsync(
        new JobRequest(Kind, JobResourceClass.System, JobPriority.P4, JobRequest.EmptyPayload(), DedupeKey: DedupeKey(Kind, context.Now)),
        context.Transaction, ct);
}

/// <summary>Daily <c>system.ensure_partitions</c> (monthly partitions ahead).</summary>
public sealed class PartitionMaintenanceTask(IJobQueue queue) : DailySystemJobTask(queue)
{
    /// <inheritdoc />
    protected override string Kind => SystemJobKinds.EnsurePartitions;
}

/// <summary>Daily <c>system.cleanup_jobs</c> (finished jobs).</summary>
public sealed class JobCleanupTask(IJobQueue queue) : DailySystemJobTask(queue)
{
    /// <inheritdoc />
    protected override string Kind => SystemJobKinds.CleanupJobs;
}

/// <summary>Deletes rows of workers without a heartbeat for 10 minutes, every 10 minutes.</summary>
public sealed class WorkerRegistryCleanupTask(IWorkerStore store) : IScheduledTask
{
    /// <summary>Age of a stale worker row.</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(10);

    /// <inheritdoc />
    public string Name => "worker_registry_cleanup";

    /// <inheritdoc />
    public TimeSpan Interval => StaleAfter;

    /// <inheritdoc />
    public Task RunAsync(ScheduledTaskContext context, CancellationToken ct) => store.DeleteStaleWorkersAsync(StaleAfter, ct);
}
