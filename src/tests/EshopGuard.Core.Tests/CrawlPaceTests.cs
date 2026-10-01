using System.Diagnostics;
using EshopGuard.Core.Crawl;
using EshopGuard.Core.Models;
using EshopGuard.Core.Options;
using Microsoft.Extensions.DependencyInjection;

namespace EshopGuard.Core.Tests;

/// <summary>
/// Adaptive crawl pace: faster while the server answers quickly, slower when it is slow or asks to wait, Crawl-delay.
/// </summary>
public class CrawlPaceTests
{
    private static readonly FetchResponse Ok = new() { Url = new Uri("https://shop.example/"), StatusCode = 200, Body = [] };

    [Fact]
    public void Gate_SpeedsUpToTheMaximumWhileAnswersAreFast()
    {
        var gate = new AdaptiveGate(startRate: 1, maxRate: 3, adaptive: true);

        for (var i = 0; i < 20; i++)
        {
            gate.Report(Ok, TimeSpan.FromMilliseconds(100));
        }

        Assert.Equal(3, gate.Rate, precision: 6);
    }

    [Fact]
    public void Gate_SlowsDownOnSlowAnswersAndErrors()
    {
        var gate = new AdaptiveGate(startRate: 3, maxRate: 3, adaptive: true);

        gate.Report(Ok, TimeSpan.FromSeconds(2));
        Assert.Equal(2.1, gate.Rate, precision: 6);

        gate.Report(new FetchResponse { Url = Ok.Url, StatusCode = 500 }, TimeSpan.FromMilliseconds(100));
        Assert.Equal(1.47, gate.Rate, precision: 6);
    }

    [Fact]
    public async Task Gate_WaitsAndHalvesThePaceWhenTheServerAsks()
    {
        var gate = new AdaptiveGate(startRate: 2, maxRate: 3, adaptive: true);
        var tooMany = new FetchResponse { Url = Ok.Url, StatusCode = 429, RetryAfter = TimeSpan.FromMilliseconds(300) };

        var wait = gate.Report(tooMany, TimeSpan.FromMilliseconds(50));
        var watch = Stopwatch.StartNew();
        await gate.WaitAsync(TestContext.Current.CancellationToken);

        Assert.Equal(TimeSpan.FromMilliseconds(300), wait);
        Assert.True(watch.Elapsed >= TimeSpan.FromMilliseconds(250));
        Assert.Equal(1, gate.Rate, precision: 6);
        Assert.Equal(1, gate.Throttled);
    }

    [Fact]
    public void Gate_KeepsAFixedPaceButHonoursCrawlDelay()
    {
        var fixedGate = new AdaptiveGate(startRate: 0.5, maxRate: 0.5, adaptive: false);
        fixedGate.Report(Ok, TimeSpan.FromMilliseconds(100));
        Assert.Equal(0.5, fixedGate.Rate, precision: 6);

        var gate = new AdaptiveGate(startRate: 1, maxRate: 3, adaptive: true);
        gate.Limit(1 / 2.0); // Crawl-delay: 2
        for (var i = 0; i < 10; i++)
        {
            gate.Report(Ok, TimeSpan.FromMilliseconds(100));
        }

        Assert.Equal(0.5, gate.Rate, precision: 6);
    }

    [Theory]
    [InlineData("User-agent: *\nCrawl-delay: 2\nDisallow: /kosik\n", 2.0)]
    [InlineData("User-agent: EshopGuard\nCrawl-delay: 5\n\nUser-agent: *\nCrawl-delay: 1\n", 5.0)]
    [InlineData("User-agent: *\nDisallow: /kosik\n", null)]
    public void Robots_ReadsCrawlDelayOfTheApplicableGroup(string content, double? seconds)
    {
        var robots = RobotsTxt.Parse(content, "EshopGuard");

        Assert.Equal(seconds, robots.CrawlDelay?.TotalSeconds);
    }

    [Fact]
    public async Task Scan_WaitsAndRetriesWhenAPageAnswers429()
    {
        var fetcher = new OnceTooManyFetcher(FileSystemPageFetcher.ForSlovakFixture(), "/produkt-1.html");
        await using var provider = TestServices.Create(fetcher);

        var result = await provider.GetRequiredService<IEshopGuard>().ScanSiteAsync(
            FileSystemPageFetcher.DefaultBaseUrl, new ScanOptions { Country = "sk" }, ct: TestContext.Current.CancellationToken);

        Assert.Equal(1, result.Stats.CrawlThrottled);
        Assert.Contains(result.Pages, p => p.Url.EndsWith("/produkt-1.html", StringComparison.Ordinal));
        Assert.Equal(0, result.Stats.PagesFailed);
    }

    /// <summary>Answers 429 with a short Retry-After the first time the path is requested.</summary>
    private sealed class OnceTooManyFetcher(IPageFetcher inner, string path) : IPageFetcher
    {
        private int _answered;

        public Task<FetchResponse> FetchAsync(Uri url, CancellationToken ct) =>
            url.AbsolutePath == path && Interlocked.Exchange(ref _answered, 1) == 0
                ? Task.FromResult(new FetchResponse { Url = url, StatusCode = 429, RetryAfter = TimeSpan.FromMilliseconds(50) })
                : inner.FetchAsync(url, ct);
    }
}
