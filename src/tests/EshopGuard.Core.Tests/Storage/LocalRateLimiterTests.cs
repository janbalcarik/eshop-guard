using System.Diagnostics;
using EshopGuard.Core.Jev;
using EshopGuard.Core.Storage;

namespace EshopGuard.Core.Tests;

/// <summary>The limit of one process (task 3.7): Jev requests per minute spread over seconds, OpenAI without a limit.</summary>
public sealed class LocalRateLimiterTests
{
    [Fact]
    public async Task Jev1200PerMinute_GivesAtMost20PermitsPerSecond()
    {
        using var limiter = new LocalRateLimiter(Microsoft.Extensions.Options.Options.Create(new JevOptions { RequestsPerMinute = 1200 }));
        var clock = Stopwatch.StartNew();
        var times = new List<TimeSpan>();
        for (var i = 0; i < 45; i++)
        {
            using var permit = await limiter.AcquireAsync(RateResource.Jev, 1, RequestPriority.P2, TestContext.Current.CancellationToken);
            times.Add(clock.Elapsed);
        }

        // Any window of one second holds at most 20 permits (plus timer slack of the bucket).
        for (var i = 0; i + 20 < times.Count; i++)
        {
            Assert.True(times[i + 20] - times[i] >= TimeSpan.FromMilliseconds(900), $"permits {i}–{i + 20} within {(times[i + 20] - times[i]).TotalMilliseconds} ms");
        }
    }

    [Fact]
    public async Task CancelWhileWaiting_ThrowsOperationCanceled()
    {
        using var limiter = new LocalRateLimiter(Microsoft.Extensions.Options.Options.Create(new JevOptions { RequestsPerMinute = 60 }));
        using var first = await limiter.AcquireAsync(RateResource.Jev, 1, RequestPriority.P2, TestContext.Current.CancellationToken);
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await limiter.AcquireAsync(RateResource.Jev, 1, RequestPriority.P2, cancel.Token));
    }

    [Fact]
    public async Task OpenAi_HasNoLimitHere()
    {
        using var limiter = new LocalRateLimiter(Microsoft.Extensions.Options.Options.Create(new JevOptions { RequestsPerMinute = 60 }));
        var clock = Stopwatch.StartNew();
        for (var i = 0; i < 100; i++)
        {
            using var permit = await limiter.AcquireAsync(RateResource.OpenAi, 1, RequestPriority.P1, TestContext.Current.CancellationToken);
        }

        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(1));
    }
}
