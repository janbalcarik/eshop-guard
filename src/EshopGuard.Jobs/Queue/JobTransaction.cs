using EshopGuard.Data;
using Npgsql;

namespace EshopGuard.Jobs.Queue;

/// <summary>
/// The completion transaction of a job (tenant of the job set, job row locked with fencing). The handler writes its results
/// through <see cref="Db"/> or plain SQL on <see cref="Connection"/> and <see cref="Transaction"/>, and enqueues continuations
/// with <see cref="EnqueueAsync"/>; everything commits together with the job's <c>succeeded</c> state, or nothing does.
/// </summary>
public sealed class JobTransaction
{
    private readonly PgJobQueue _queue;

    internal JobTransaction(EshopGuardDb db, NpgsqlTransaction transaction, PgJobQueue queue, ClaimedJob job)
    {
        Db = db;
        Transaction = transaction;
        _queue = queue;
        Job = job;
    }

    /// <summary>Context of the job's tenant, enlisted in the transaction (<c>SaveChangesAsync</c> writes in it).</summary>
    public EshopGuardDb Db { get; }

    /// <summary>The transaction for plain SQL.</summary>
    public NpgsqlTransaction Transaction { get; }

    /// <summary>The connection of <see cref="Transaction"/>.</summary>
    public NpgsqlConnection Connection => Transaction.Connection!;

    /// <summary>The job being completed.</summary>
    public ClaimedJob Job { get; }

    /// <summary>
    /// Enqueues a continuation in this transaction. For a run whose cancellation was requested nothing is created
    /// (<c>Created = false</c>, state <c>Canceled</c>); the run row stays locked until COMMIT, so a concurrent cancellation
    /// waits and then cancels what was created here.
    /// </summary>
    public Task<EnqueueResult> EnqueueAsync(JobRequest request, CancellationToken ct = default) =>
        _queue.EnqueueCoreAsync(request, Transaction, checkRunCanceled: true, ct);
}
