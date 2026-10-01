using System.Threading.RateLimiting;
using EshopGuard.Core.Jev;
using Microsoft.Extensions.Options;

namespace EshopGuard.Core.Storage;

/// <summary>External service whose calls are limited.</summary>
public enum RateResource
{
    /// <summary>The Jev API (requests per minute of the key).</summary>
    Jev,

    /// <summary>The OpenAI API.</summary>
    OpenAi,
}

/// <summary>Priority of the work asking for a permit; a shared limiter keeps a share of the limit for P0 and P1.</summary>
public enum RequestPriority
{
    /// <summary>Interactive (the user waits).</summary>
    P0 = 0,

    /// <summary>Paid or user-started analysis.</summary>
    P1 = 1,

    /// <summary>Regular work (the CLI).</summary>
    P2 = 2,

    /// <summary>Monitoring.</summary>
    P3 = 3,

    /// <summary>Maintenance.</summary>
    P4 = 4,
}

/// <summary>
/// Permits for calls of an external service. The CLI limits one process (<see cref="LocalRateLimiter"/>); the web
/// application shares the limit of the key among all workers (change 8, <c>ops.rate_limit_buckets</c>).
/// </summary>
public interface IRateLimiter
{
    /// <summary>Waits for <paramref name="permits"/> permits; dispose the result after the call.</summary>
    ValueTask<IDisposable> AcquireAsync(RateResource resource, int permits, RequestPriority priority, CancellationToken ct);
}

/// <summary>
/// Limits of one process: Jev requests per minute spread evenly over seconds (rounded down, so the limit is never
/// exceeded), OpenAI without a limit here (concurrency is limited by the rewrite settings). Priority is not used.
/// </summary>
internal sealed class LocalRateLimiter : IRateLimiter, IDisposable
{
    private readonly TokenBucketRateLimiter _jev;

    public LocalRateLimiter(IOptions<JevOptions> options)
    {
        var perSecond = Math.Max(1, options.Value.RequestsPerMinute / 60);
        _jev = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = perSecond,
            TokensPerPeriod = perSecond,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            QueueLimit = int.MaxValue,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true,
        });
    }

    public async ValueTask<IDisposable> AcquireAsync(RateResource resource, int permits, RequestPriority priority, CancellationToken ct) =>
        resource == RateResource.Jev ? await _jev.AcquireAsync(permits, ct) : NoLimit.Instance;

    public void Dispose() => _jev.Dispose();

    private sealed class NoLimit : IDisposable
    {
        public static readonly NoLimit Instance = new();

        public void Dispose()
        {
        }
    }
}
