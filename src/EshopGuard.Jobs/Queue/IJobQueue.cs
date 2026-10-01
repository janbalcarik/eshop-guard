using System.Data.Common;
using EshopGuard.Data;
using EshopGuard.Data.Entities.Ops;

namespace EshopGuard.Jobs.Queue;

/// <summary>
/// Job queue in <c>ops.jobs</c>: enqueue in the caller's transaction, claim with <c>FOR UPDATE SKIP LOCKED</c>, leases with
/// heartbeat and fencing (<c>lease_owner</c> + <c>attempts</c>). Processing is at least once; results are written only in
/// <see cref="CompleteAsync(ClaimedJob, Func{JobTransaction, Task}?, CancellationToken)"/>.
/// </summary>
public interface IJobQueue
{
    /// <summary>
    /// Inserts the job in the caller's transaction and notifies workers after its commit (not at all after a rollback).
    /// An existing <c>dedupe_key</c> returns the existing job with <c>Created = false</c>.
    /// </summary>
    Task<EnqueueResult> EnqueueAsync(JobRequest request, DbTransaction transaction, CancellationToken ct = default);

    /// <summary>Takes up to <paramref name="max"/> ready jobs of the class, ordered by priority and id; one per concurrency key.</summary>
    Task<IReadOnlyList<ClaimedJob>> ClaimAsync(JobResourceClass resourceClass, int max, string workerId, CancellationToken ct = default);

    /// <summary>Extends the lease (and the domain lease the job holds) and reports whether the job's run was canceled.</summary>
    Task<HeartbeatResult> HeartbeatAsync(ClaimedJob job, CancellationToken ct = default);

    /// <summary>
    /// In one transaction (tenant of the job): locks the job row with fencing, runs <paramref name="writeResults"/> and marks
    /// the job <c>succeeded</c>. Throws <see cref="LeaseLostException"/> and rolls back when the worker no longer owns the job.
    /// </summary>
    Task CompleteAsync(ClaimedJob job, Func<JobTransaction, Task>? writeResults, CancellationToken ct = default);

    /// <summary>
    /// Same as <see cref="CompleteAsync(ClaimedJob, Func{JobTransaction, Task}?, CancellationToken)"/> on the given context
    /// (the handler's scope, whose tenant must be the job's tenant).
    /// </summary>
    Task CompleteAsync(ClaimedJob job, EshopGuardDb db, Func<JobTransaction, Task>? writeResults, CancellationToken ct = default);

    /// <summary>
    /// Returns the job to the queue with a growing backoff (or <paramref name="retryAfter"/>), or marks it <c>failed</c> when
    /// <paramref name="permanent"/> or the attempts are used up. Returns the new state; <see cref="LeaseLostException"/> without the lease.
    /// </summary>
    Task<JobState> FailAsync(ClaimedJob job, JobError error, bool permanent, TimeSpan? retryAfter = null, CancellationToken ct = default);

    /// <summary>Returns the job to the queue after <paramref name="delay"/> without counting the attempt.</summary>
    Task DeferAsync(ClaimedJob job, TimeSpan delay, string reasonCode, CancellationToken ct = default);

    /// <summary>Marks a running job <c>canceled</c> (its run was canceled); <see cref="LeaseLostException"/> without the lease.</summary>
    Task MarkCanceledAsync(ClaimedJob job, CancellationToken ct = default);

    /// <summary>Cancels a waiting job; <c>false</c> when it is not <c>queued</c> (running jobs finish).</summary>
    Task<bool> CancelAsync(long jobId, CancellationToken ct = default);

    /// <summary>
    /// Cancels the run's waiting jobs in the caller's transaction. The caller sets <c>checks.runs.cancel_requested</c> in the
    /// same transaction <b>before</b> this call, so a continuation being written concurrently either sees the cancellation or is canceled here.
    /// </summary>
    Task<int> CancelRunJobsAsync(Guid runId, DbTransaction transaction, CancellationToken ct = default);

    /// <summary>Whether the run's cancellation was requested (read in a transaction of the run's tenant; an invisible run counts as canceled).</summary>
    Task<bool> IsRunCancelRequestedAsync(Guid tenantId, Guid runId, CancellationToken ct = default);

    /// <summary>Returns jobs with an expired lease (crashed or stuck worker) to the queue with a backoff, or fails them.</summary>
    Task<int> RequeueExpiredLeasesAsync(int max, CancellationToken ct = default);

    /// <summary>Stops all workers from taking jobs of the class (credit exhausted, key rejected).</summary>
    Task PauseResourceClassAsync(JobResourceClass resourceClass, string reasonCode, CancellationToken ct = default);

    /// <summary>Lets workers take jobs of the class again (manual decision).</summary>
    Task ResumeResourceClassAsync(JobResourceClass resourceClass, CancellationToken ct = default);

    /// <summary>Paused classes (cached for <c>Jobs:PausedClassesCacheSeconds</c>).</summary>
    Task<IReadOnlySet<JobResourceClass>> GetPausedClassesAsync(CancellationToken ct = default);

    /// <summary>Deletes up to <paramref name="batchSize"/> finished jobs: succeeded and canceled older than the first age, failed older than the second.</summary>
    Task<int> DeleteFinishedAsync(TimeSpan succeededOlderThan, TimeSpan failedOlderThan, int batchSize, CancellationToken ct = default);
}
