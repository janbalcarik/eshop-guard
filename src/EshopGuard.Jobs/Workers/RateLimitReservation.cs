namespace EshopGuard.Jobs.Workers;

/// <summary>Result of reserving tokens from a shared bucket.</summary>
public abstract record RateLimitReservation
{
    private RateLimitReservation()
    {
    }

    /// <summary>The tokens were taken; <paramref name="Remaining"/> stay in the bucket.</summary>
    public sealed record Granted(double Remaining) : RateLimitReservation;

    /// <summary>
    /// Not enough tokens for the priority; try again after <paramref name="RetryAfter"/>
    /// (<see cref="TimeSpan.MaxValue"/> for a bucket that does not refill).
    /// </summary>
    public sealed record Denied(TimeSpan RetryAfter) : RateLimitReservation;
}

/// <summary>The bucket does not exist; a call must not proceed without a limit (<c>ratelimit.bucket_missing</c>).</summary>
public sealed class RateLimitBucketMissingException(string bucketKey)
    : InvalidOperationException($"{JobErrorCodes.RateLimitBucketMissing}: {bucketKey}")
{
    /// <summary>Code <c>ratelimit.bucket_missing</c>.</summary>
    public string Code => JobErrorCodes.RateLimitBucketMissing;

    /// <summary>Key of the missing bucket.</summary>
    public string BucketKey { get; } = bucketKey;
}
