using System.Net;
using EshopGuard.Core.Crawl;
using EshopGuard.Core.Options;
using EshopGuard.Core.Pipeline;
using EshopGuard.Core.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace EshopGuard.Core.Tests;

/// <summary>
/// Conditional download (task 5.7): a page that did not change since the last run answers 304 and is neither downloaded,
/// extracted nor counted; the same text gives no new version; without validators no conditional header is sent.
/// </summary>
public sealed class ConditionalFetchTests
{
    [Fact]
    public async Task HttpFetcher_SendsValidators_And304IsNotModifiedNotARedirect()
    {
        const string etag = "\"v1\"";
        await using var server = new LocalHttpServer(request => request.Headers.GetValueOrDefault("If-None-Match") == etag
            ? new LocalHttpServer.Answer(304, Headers: new Dictionary<string, string> { ["ETag"] = etag })
            : new LocalHttpServer.Answer(200, "<html><body>Text</body></html>", Headers: new Dictionary<string, string> { ["ETag"] = etag, ["Last-Modified"] = "Wed, 30 Sep 2026 10:00:00 GMT" }));
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostAddressResolver>(new HttpPageFetcherSsrfTests.FakeResolver([IPAddress.Loopback]));
        services.AddEshopGuard(o =>
        {
            o.Crawl.AllowPrivateNetwork = true;
            o.Jev.UseMock = true;
            o.Rewrite.UseMock = true;
        });
        await using var provider = services.BuildServiceProvider();
        var fetcher = provider.GetRequiredService<IPageFetcher>();
        var url = new Uri($"http://shop.test:{server.Port}/stranka");

        var first = await fetcher.FetchAsync(new FetchRequest(url), TestContext.Current.CancellationToken);
        var second = await fetcher.FetchAsync(new FetchRequest(url) { IfNoneMatch = first.ETag, IfModifiedSince = first.LastModified }, TestContext.Current.CancellationToken);

        Assert.True(first.IsSuccess);
        Assert.Equal(etag, first.ETag);
        Assert.Equal(new DateTimeOffset(2026, 9, 30, 10, 0, 0, TimeSpan.Zero), first.LastModified);
        Assert.True(second.NotModified);
        Assert.Null(second.RedirectLocation);
        Assert.Null(second.Error);
        var requests = server.Requests.ToList();
        Assert.False(requests[0].Headers.ContainsKey("If-None-Match"));
        Assert.Equal(etag, requests[1].Headers["If-None-Match"]);
        Assert.Equal("Wed, 30 Sep 2026 10:00:00 GMT", requests[1].Headers["If-Modified-Since"]);
    }

    [Fact]
    public async Task UnchangedPages_Answer304_AndAreNotExtractedOrCounted()
    {
        var fetcher = new FileSystemPageFetcher(Path.Combine(AppContext.BaseDirectory, "Fixtures", "site-sk"), FileSystemPageFetcher.DefaultBaseUrl) { UseETags = true };
        await using var provider = TestServices.Create(fetcher);
        var first = await CrawlAsync(provider, validators: null);
        var validators = first.Pages
            .Where(p => p.Outcome == FetchOutcome.Ok && p.ETag is not null)
            .ToDictionary(p => p.FinalUrl.AbsoluteUri, p => new ConditionalHeaders(p.ETag, p.LastModified));

        var second = await CrawlAsync(provider, validators);

        // The home page is downloaded again: its links lead to the legal pages and its extraction was not stored.
        Assert.NotEmpty(validators);
        var home = Assert.Single(second.Pages, p => p.IsHome);
        Assert.Equal(FetchOutcome.Ok, home.Outcome);
        Assert.All(second.Pages.Where(p => !p.IsHome), p => Assert.Equal(FetchOutcome.NotModified, p.Outcome));
        Assert.All(second.Pages.Where(p => !p.IsHome), p => Assert.Null(p.Extract));
        Assert.Equal(validators.Count, second.Pages.Count);
        Assert.Equal(1, second.Frontier.Fetched);
        Assert.Contains(fetcher.Conditional, c => c.IfNoneMatch is not null);
    }

    [Fact]
    public async Task UnchangedHomePage_WithStoredExtraction_IsNotDownloadedAgain()
    {
        var fetcher = new FileSystemPageFetcher(Path.Combine(AppContext.BaseDirectory, "Fixtures", "site-sk"), FileSystemPageFetcher.DefaultBaseUrl) { UseETags = true };
        await using var provider = TestServices.Create(fetcher);
        var first = await CrawlAsync(provider, validators: null, persist: true);
        var validators = first.Pages.ToDictionary(p => p.FinalUrl.AbsoluteUri, p => new ConditionalHeaders(p.ETag, p.LastModified));

        var second = await CrawlAsync(provider, validators);

        Assert.All(second.Pages, p => Assert.Equal(FetchOutcome.NotModified, p.Outcome));
        Assert.Equal(first.Pages.Select(p => p.FinalUrl), second.Pages.Select(p => p.FinalUrl));
        Assert.Equal(0, second.Frontier.Fetched);
    }

    [Fact]
    public async Task Scan_WithoutValidators_SendsNoConditionalHeader_AndTheSameTextAddsNoVersion()
    {
        var fetcher = FileSystemPageFetcher.ForSlovakFixture();
        var pages = new CountingPageStore();
        await using var provider = TestServices.Create(fetcher, register: s => s.AddSingleton<IPageStore>(pages));
        var guard = provider.GetRequiredService<IEshopGuard>();

        var first = await guard.ScanSiteAsync(FileSystemPageFetcher.DefaultBaseUrl, new ScanOptions(), ct: TestContext.Current.CancellationToken);
        var added = pages.Added;
        await guard.ScanSiteAsync(FileSystemPageFetcher.DefaultBaseUrl, new ScanOptions(), ct: TestContext.Current.CancellationToken);

        Assert.All(fetcher.Conditional, c => Assert.Null(c.IfNoneMatch));
        Assert.Equal(first.Pages.Count, added);
        Assert.Equal(added, pages.Added);
        Assert.Equal(2 * first.Pages.Count, pages.Calls);
        Assert.Equal(first.Pages.Count, (await pages.GetCurrentVersionsAsync("fixture.test", TestContext.Current.CancellationToken)).Count);
    }

    private static async Task<FetchBatchResult> CrawlAsync(IServiceProvider provider, IReadOnlyDictionary<string, ConditionalHeaders>? validators, bool persist = false)
    {
        var site = new SiteScope(FileSystemPageFetcher.DefaultBaseUrl);
        var discovery = await provider.GetRequiredService<DiscoveryStep>()
            .DiscoverAsync(new DiscoveryInput(site, new CrawlLimits(200, 100, [], [], null)), null, TestContext.Current.CancellationToken);
        return await provider.GetRequiredService<FetchStep>().FetchBatchAsync(
            new FetchBatchInput(site, discovery.Robots, discovery.Frontier, discovery.Pace, 1000, TimeSpan.FromMinutes(5), ExtractInline: true)
            {
                Validators = validators ?? new Dictionary<string, ConditionalHeaders>(),
                PersistExtracts = persist,
            },
            null,
            TestContext.Current.CancellationToken);
    }

    /// <summary>Pages in memory that count the versions really added.</summary>
    private sealed class CountingPageStore : IPageStore
    {
        private readonly InMemoryPageStore _inner = new();
        private int _added;
        private int _calls;

        public int Added => Volatile.Read(ref _added);

        public int Calls => Volatile.Read(ref _calls);

        public Task<IReadOnlyDictionary<string, PageValidators>> GetValidatorsAsync(string site, IReadOnlyList<string> urls, CancellationToken ct = default) =>
            _inner.GetValidatorsAsync(site, urls, ct);

        public Task UpsertPageAsync(PageRecord page, CancellationToken ct = default) => _inner.UpsertPageAsync(page, ct);

        public async Task<bool> AddVersionAsync(PageVersionRecord version, CancellationToken ct = default)
        {
            Interlocked.Increment(ref _calls);
            var added = await _inner.AddVersionAsync(version, ct);
            if (added)
            {
                Interlocked.Increment(ref _added);
            }

            return added;
        }

        public Task<IReadOnlyList<PageVersionRecord>> GetCurrentVersionsAsync(string site, CancellationToken ct = default) => _inner.GetCurrentVersionsAsync(site, ct);

        public Task<IReadOnlyList<string>> FindByFingerprintAsync(string site, long fingerprint, CancellationToken ct = default) => _inner.FindByFingerprintAsync(site, fingerprint, ct);
    }
}
