namespace EshopGuard.Jobs;

/// <summary>Codes of the job queue in <c>last_error</c>, logs and exceptions (never texts of pages or keys).</summary>
public static class JobErrorCodes
{
    /// <summary><c>EnqueueJobAsync</c> was called on a context without an open transaction.</summary>
    public const string EnqueueRequiresTransaction = "job.enqueue_requires_transaction";

    /// <summary>The job request is invalid (empty kind, priority outside 0–4, max attempts below 1).</summary>
    public const string InvalidRequest = "job.invalid_request";

    /// <summary>The payload is larger than <see cref="Queue.JobRequest.MaxPayloadBytes"/> (page texts do not belong in the queue).</summary>
    public const string PayloadTooLarge = "job.payload_too_large";

    /// <summary>No handler is registered for the kind; the job fails permanently.</summary>
    public const string UnknownKind = "job.unknown_kind";

    /// <summary>Two handlers claim the same kind (startup error).</summary>
    public const string DuplicateKind = "job.duplicate_kind";

    /// <summary>The handler threw an exception it did not handle; the job is retried.</summary>
    public const string Unhandled = "job.unhandled";

    /// <summary>The worker lost the lease (another worker took the job over); nothing is written.</summary>
    public const string LeaseLost = "job.lease_lost";

    /// <summary>The lease expired without a heartbeat (crashed or stuck worker); the scheduler returned the job.</summary>
    public const string LeaseExpired = "job.lease_expired";

    /// <summary>The worker shut down before the job finished; the job went back without counting the attempt.</summary>
    public const string WorkerShutdown = "worker.shutdown";

    /// <summary>The row <c>ops.system_settings('jobs.paused_classes')</c> is missing (migration F2 not applied).</summary>
    public const string PausedClassesMissing = "job.paused_classes_missing";

    /// <summary>Invalid settings under <c>Worker</c> (startup error).</summary>
    public const string WorkerConfigInvalid = "config.worker_invalid";

    /// <summary>The rate limit bucket does not exist; the call must not proceed without a limit.</summary>
    public const string RateLimitBucketMissing = "ratelimit.bucket_missing";

    /// <summary>The reservation can never be granted (more tokens than the priority may draw); the batch must be smaller.</summary>
    public const string RateLimitBatchTooLarge = "ratelimit.batch_too_large";
}

/// <summary>Error of the job queue with a code (subclass of <see cref="InvalidOperationException"/>).</summary>
public sealed class JobQueueException(string code, string message) : InvalidOperationException($"{code}: {message}")
{
    /// <summary>Code from <see cref="JobErrorCodes"/>.</summary>
    public string Code { get; } = code;
}
