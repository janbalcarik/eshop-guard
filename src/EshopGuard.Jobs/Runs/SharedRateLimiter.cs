using EshopGuard.Core.Storage;
using EshopGuard.Jobs.Queue;
using EshopGuard.Jobs.Workers;

namespace EshopGuard.Jobs.Runs;

/// <summary>
/// The limit of calls of Jev and OpenAI shared by all workers (<c>ops.rate_limit_buckets</c>, change 4): every call takes its
/// permit from the bucket of the key in one statement; a share of it is kept for P0–P1. When the bucket is empty the call
/// waits for the time the bucket gives; a missing bucket fails the call (no call without a limit).
/// </summary>
public sealed class SharedRateLimiter(IWorkerStore store, TimeProvider time) : IRateLimiter
{
    private static readonly TimeSpan MinWait = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan MaxWait = TimeSpan.FromSeconds(5);

    public async ValueTask<IDisposable> AcquireAsync(RateResource resource, int permits, RequestPriority priority, CancellationToken ct)
    {
        var bucket = resource == RateResource.Jev ? "jev" : "openai";
        while (true)
        {
            switch (await store.TryReserveAsync(bucket, permits, (JobPriority)(int)priority, ct).ConfigureAwait(false))
            {
                case RateLimitReservation.Granted:
                    return Permit.Instance;
                case RateLimitReservation.Denied denied when denied.RetryAfter == TimeSpan.MaxValue:
                    throw new InvalidOperationException($"{JobErrorCodes.RateLimitBucketMissing}: the bucket {bucket} does not refill");
                case RateLimitReservation.Denied denied:
                    var wait = denied.RetryAfter < MinWait ? MinWait : denied.RetryAfter > MaxWait ? MaxWait : denied.RetryAfter;
                    await Task.Delay(wait, time, ct).ConfigureAwait(false);
                    break;
            }
        }
    }

    private sealed class Permit : IDisposable
    {
        public static readonly Permit Instance = new();

        public void Dispose()
        {
        }
    }
}
