using EshopGuard.Jobs.Queue;

namespace EshopGuard.Jobs.Workers;

/// <summary>Registry of workers (<c>ops.workers</c>), domain leases (<c>ops.domains</c>) and shared rate limits (<c>ops.rate_limit_buckets</c>).</summary>
public interface IWorkerStore
{
    /// <summary>Inserts or refreshes the worker's row (start time, slots, not draining).</summary>
    Task RegisterAsync(WorkerRegistration worker, CancellationToken ct = default);

    /// <summary>Refreshes the heartbeat; <c>false</c> when the row is gone (deleted as stale), so the worker registers again.</summary>
    Task<bool> HeartbeatAsync(string workerId, bool draining, CancellationToken ct = default);

    /// <summary>Deletes the worker's row (after a clean shutdown).</summary>
    Task UnregisterAsync(string workerId, CancellationToken ct = default);

    /// <summary>Deletes rows of workers without a heartbeat for <paramref name="olderThan"/>.</summary>
    Task<int> DeleteStaleWorkersAsync(TimeSpan olderThan, CancellationToken ct = default);

    /// <summary>
    /// Takes the domain for the job when it is free, expired or already the job's, and not blocked; otherwise <c>null</c>
    /// (the handler defers the job). The job's heartbeat extends the lease.
    /// </summary>
    Task<DomainLease?> TryAcquireDomainAsync(string domain, long jobId, TimeSpan lease, CancellationToken ct = default);

    /// <summary>Extends the job's domain lease; <c>false</c> when the job no longer holds it.</summary>
    Task<bool> RenewDomainAsync(string domain, long jobId, TimeSpan lease, CancellationToken ct = default);

    /// <summary>Releases the job's lease and stores the politeness state; a lease of another job is left untouched.</summary>
    Task ReleaseDomainAsync(string domain, long jobId, DomainPolitenessState state, CancellationToken ct = default);

    /// <summary>
    /// Takes <paramref name="tokens"/> from the bucket in one statement. P2–P4 may draw only down to the share reserved for
    /// P0–P1. A missing bucket throws <see cref="RateLimitBucketMissingException"/>; a batch that could never be granted
    /// throws <see cref="ArgumentOutOfRangeException"/> (<c>ratelimit.batch_too_large</c>).
    /// </summary>
    Task<RateLimitReservation> TryReserveAsync(string bucketKey, int tokens, JobPriority priority, CancellationToken ct = default);

    /// <summary>Gives back unused tokens (e.g. after a cache hit), never above the capacity.</summary>
    Task ReturnTokensAsync(string bucketKey, int tokens, CancellationToken ct = default);
}
