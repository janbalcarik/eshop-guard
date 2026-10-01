using EshopGuard.Data.Entities.Ops;

namespace EshopGuard.Jobs.Processing;

/// <summary>
/// Handler of one job kind. Runs in its own DI scope with the job's tenant set. Results are written only through
/// <see cref="JobExecutionContext.CompleteAsync"/> (in the transaction that marks the job done); effects outside the database
/// need deterministic keys, because a job may run more than once.
/// </summary>
public interface IJobHandler
{
    /// <summary>Kind, e.g. <c>system.ensure_partitions</c>.</summary>
    string Kind { get; }

    /// <summary>Resource class the jobs of this kind are enqueued with.</summary>
    JobResourceClass ResourceClass { get; }

    /// <summary>Runs the job; <paramref name="ct"/> is canceled when the lease is lost or the worker shuts down.</summary>
    Task<JobResult> ExecuteAsync(JobExecutionContext context, CancellationToken ct);
}
