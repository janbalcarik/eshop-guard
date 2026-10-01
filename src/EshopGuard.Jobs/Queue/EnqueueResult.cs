using EshopGuard.Data.Entities.Ops;

namespace EshopGuard.Jobs.Queue;

/// <summary>
/// Result of enqueueing. <paramref name="Created"/> = false: a job with the same <c>dedupe_key</c> already existed
/// (its id and state are returned), or the run is canceled and no continuation was created (id 0, state <c>Canceled</c>).
/// </summary>
public sealed record EnqueueResult(long JobId, bool Created, JobState State);
