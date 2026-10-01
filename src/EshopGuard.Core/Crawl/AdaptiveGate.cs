using System.Diagnostics;

namespace EshopGuard.Core.Crawl;

/// <summary>
/// Paces requests to the scanned site. Starts at the start rate; while the server answers quickly (under 0.5 s)
/// without errors, the rate rises by 0.25 requests per second up to the maximum; a slow answer (over 1.5 s) or an error
/// lowers it to 70 %, a 429 or 503 halves it and waits for Retry-After (at most 60 s, 10 s when not given).
/// A fixed rate (the <c>--rate</c> option) does not adapt, but still waits when the server asks for it.
/// A rate of zero or less means no delay at all (tests only).
/// </summary>
internal sealed class AdaptiveGate
{
    public const double MinRate = 0.2;
    private static readonly TimeSpan FastAnswer = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan SlowAnswer = TimeSpan.FromMilliseconds(1500);
    private static readonly TimeSpan DefaultRetryAfter = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan MaxRetryAfter = TimeSpan.FromSeconds(60);

    private readonly bool _adaptive;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly object _rateLock = new();
    private double _maxRate;
    private double _rate;
    private long _nextTimestamp;

    /// <param name="startRate">Requests per second at the start.</param>
    /// <param name="maxRate">Upper limit of the rate.</param>
    /// <param name="adaptive">False for a fixed rate.</param>
    public AdaptiveGate(double startRate, double maxRate, bool adaptive)
    {
        _adaptive = adaptive && startRate > 0;
        _maxRate = Math.Max(startRate, maxRate);
        _rate = startRate;
    }

    /// <summary>Current rate in requests per second.</summary>
    public double Rate
    {
        get
        {
            lock (_rateLock)
            {
                return _rate;
            }
        }
    }

    /// <summary>Requests the server answered with 429 or 503.</summary>
    public int Throttled { get; private set; }

    /// <summary>A gate that goes on from the saved state of an earlier batch.</summary>
    public static AdaptiveGate FromState(Pipeline.PaceState state)
    {
        var gate = new AdaptiveGate(state.Rate, state.MaxRate, state.Adaptive) { Throttled = state.Throttled };
        if (state.NextRequestAt is { } next && next > DateTimeOffset.UtcNow)
        {
            gate._nextTimestamp = Stopwatch.GetTimestamp() + (long)((next - DateTimeOffset.UtcNow).TotalSeconds * Stopwatch.Frequency);
        }

        return gate;
    }

    /// <summary>The state to go on with in the next batch.</summary>
    public Pipeline.PaceState ToState()
    {
        lock (_rateLock)
        {
            var wait = Stopwatch.GetElapsedTime(Stopwatch.GetTimestamp(), Interlocked.Read(ref _nextTimestamp));
            return new Pipeline.PaceState(_rate, _maxRate, _adaptive, Throttled, wait > TimeSpan.Zero ? DateTimeOffset.UtcNow + wait : null);
        }
    }

    /// <summary>Lowers the maximum, for example to 1/Crawl-delay from robots.txt.</summary>
    public void Limit(double maxRate)
    {
        lock (_rateLock)
        {
            if (_rate <= 0 || maxRate <= 0)
            {
                return;
            }

            _maxRate = Math.Min(_maxRate, maxRate);
            _rate = Math.Min(_rate, _maxRate);
        }
    }

    public async Task WaitAsync(CancellationToken ct)
    {
        if (Rate <= 0 && Throttled == 0)
        {
            return;
        }

        await _lock.WaitAsync(ct);
        try
        {
            var now = Stopwatch.GetTimestamp();
            if (_nextTimestamp > now)
            {
                await Task.Delay(Stopwatch.GetElapsedTime(now, _nextTimestamp), ct);
            }

            var rate = Rate;
            _nextTimestamp = Stopwatch.GetTimestamp() + (rate > 0 ? (long)(Stopwatch.Frequency / rate) : 0);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Adapts the rate to the answer. Returns the time to wait before retrying when the server asked to slow down
    /// (429 or 503), otherwise null.
    /// </summary>
    public TimeSpan? Report(FetchResponse response, TimeSpan elapsed)
    {
        if (response.StatusCode is 429 or 503)
        {
            Throttled++;
            var wait = response.RetryAfter is { } retry ? (retry > MaxRetryAfter ? MaxRetryAfter : retry < TimeSpan.Zero ? TimeSpan.Zero : retry) : DefaultRetryAfter;
            lock (_rateLock)
            {
                if (_rate > 0)
                {
                    _rate = Math.Max(MinRate, _rate / 2);
                }
            }

            PushNext(wait);
            return wait;
        }

        if (!_adaptive)
        {
            return null;
        }

        lock (_rateLock)
        {
            var failed = response.Error is not null || response.StatusCode >= 500;
            if (failed || elapsed > SlowAnswer)
            {
                _rate = Math.Max(MinRate, _rate * 0.7);
            }
            else if (elapsed < FastAnswer)
            {
                _rate = Math.Min(_maxRate, _rate + 0.25);
            }
        }

        return null;
    }

    private void PushNext(TimeSpan wait)
    {
        var next = Stopwatch.GetTimestamp() + (long)(wait.TotalSeconds * Stopwatch.Frequency);
        Interlocked.Exchange(ref _nextTimestamp, Math.Max(Interlocked.Read(ref _nextTimestamp), next));
    }
}
