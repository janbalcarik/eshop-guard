using EshopGuard.Data;
using Microsoft.EntityFrameworkCore.Storage;

namespace EshopGuard.Jobs.Queue;

/// <summary>Enqueue and cancel in the open transaction of an <see cref="EshopGuardDb"/> (e.g. together with a new run).</summary>
public static class EshopGuardDbJobExtensions
{
    /// <summary>
    /// Enqueues the job in <c>db.Database.CurrentTransaction</c>; without an open transaction
    /// <see cref="JobQueueException"/> <c>job.enqueue_requires_transaction</c> (a run must never exist without its job).
    /// </summary>
    public static Task<EnqueueResult> EnqueueJobAsync(this EshopGuardDb db, IJobQueue queue, JobRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(queue);
        return queue.EnqueueAsync(request, CurrentTransaction(db), ct);
    }

    /// <summary>Cancels the run's waiting jobs in <c>db.Database.CurrentTransaction</c> (after setting <c>cancel_requested</c> in it).</summary>
    public static Task<int> CancelRunJobsAsync(this EshopGuardDb db, IJobQueue queue, Guid runId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(queue);
        return queue.CancelRunJobsAsync(runId, CurrentTransaction(db), ct);
    }

    private static System.Data.Common.DbTransaction CurrentTransaction(EshopGuardDb db)
    {
        ArgumentNullException.ThrowIfNull(db);
        return db.Database.CurrentTransaction?.GetDbTransaction()
            ?? throw new JobQueueException(JobErrorCodes.EnqueueRequiresTransaction, "open a transaction (BeginTransactionAsync) before enqueueing");
    }
}
