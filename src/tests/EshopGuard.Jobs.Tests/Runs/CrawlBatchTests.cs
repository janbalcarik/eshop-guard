using EshopGuard.Core.Crawl;
using EshopGuard.Jobs.Tests.Runs.Support;

namespace EshopGuard.Jobs.Tests.Runs;

/// <summary>
/// Batches of downloads (design of change 8, task 4.4): robots.txt, one download at a time per domain across runs and
/// tenants, the SSRF protection. The User-Agent is checked on the real fetcher (<c>HttpPageFetcherSsrfTests</c>) and at the
/// start of the worker (<c>StartupChecksTests</c>).
/// </summary>
public sealed class CrawlBatchTests(JobsTestDatabase database) : JobsTestBase(database)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(90);

    [Fact]
    public async Task AddressForbiddenByRobots_GetsNoRequest_AndIsListed()
    {
        var shop = await RunTests.CreateShopAsync();
        var fetcher = new FaultyFetcher(RunTests.SlovakSite(shop.BaseUrl), _ => null, "User-agent: *\nDisallow: /o-nas.html\n");
        var (runId, status) = await RunAsync(shop, fetcher);

        Assert.Equal("finished", status);
        Assert.DoesNotContain(fetcher.Requested, u => u.AbsolutePath == "/o-nas.html");
        Assert.Equal("robots_blocked", await RunTests.ScalarAsync<string>(Db, shop.TenantId, "SELECT state FROM checks.run_urls WHERE run_id = $1 AND url LIKE '%/o-nas.html'", runId));
        var stats = await RunTests.RunJsonAsync(Db, shop, runId, "stats");
        Assert.Equal(1L, (long)stats["unchecked"]!["robots_blocked"]!);
    }

    [Fact]
    public async Task AddressLeadingIntoAnInternalNetwork_IsSsrfBlocked_AndTheRunPartial()
    {
        var shop = await RunTests.CreateShopAsync();
        var fetcher = new BlockedFetcher(RunTests.SlovakSite(shop.BaseUrl), "/o-nas.html");
        var (runId, status) = await RunAsync(shop, fetcher);

        Assert.Equal("partial", status);
        Assert.Equal("ssrf_blocked", await RunTests.ScalarAsync<string>(Db, shop.TenantId, "SELECT state FROM checks.run_urls WHERE run_id = $1 AND url LIKE '%/o-nas.html'", runId));
    }

    [Fact]
    public async Task TwoTenantsAnalysingTheSameDomain_NeverDownloadFromItAtTheSameTime()
    {
        var domain = $"same-{Guid.NewGuid():N}"[..17] + ".test";
        var first = await RunTests.CreateShopAsync(domain);
        var second = await RunTests.CreateShopAsync(domain);
        var fetcher = new ConcurrencyFetcher(RunTests.SlovakSite(first.BaseUrl), TimeSpan.FromMilliseconds(30));
        var storage = Directory.CreateTempSubdirectory("eshopguard-crawl-").FullName;
        var settings = RunTests.WorkerSettings(storage, slots: 2);
        settings["Runs:FetchBatchPages"] = "3";
        var workers = new List<TestWorker>
        {
            await Workers.StartAsync("a", settings, RunTests.Services(fetcher, new DeterministicTestJevClient())),
            await Workers.StartAsync("b", settings, RunTests.Services(fetcher, new DeterministicTestJevClient())),
        };

        var runs = new List<(RunShop Shop, Guid RunId)>();
        foreach (var shop in new[] { first, second })
        {
            var runId = (await RunTests.ServiceAsync(workers[0].Host.Services, shop.TenantId, s => s.CreateFullAnalysisAsync(shop.ShopId, null, Ct))).RunId!.Value;
            runs.Add((shop, runId));
        }

        foreach (var (shop, runId) in runs)
        {
            await RunTests.WaitForStatusAsync(Db, shop, runId, Timeout, workers, "awaiting_payment");
            await RunTests.ServiceAsync(workers[0].Host.Services, shop.TenantId, s => s.ApproveWithoutPaymentAsync(runId, Guid.CreateVersion7(), "test", Ct));
        }

        foreach (var (shop, runId) in runs)
        {
            Assert.Equal("finished", await RunTests.WaitForStatusAsync(Db, shop, runId, Timeout, workers, RunTests.Final));
        }

        Assert.Equal(1, fetcher.MaxInFlight);
        Assert.True(fetcher.Requests > 30, $"requests {fetcher.Requests}");
    }

    private async Task<(Guid RunId, string Status)> RunAsync(RunShop shop, IPageFetcher fetcher)
    {
        var storage = Directory.CreateTempSubdirectory("eshopguard-crawl-").FullName;
        var worker = await Workers.StartAsync("w0", RunTests.WorkerSettings(storage), RunTests.Services(fetcher, new DeterministicTestJevClient()));
        var runId = (await RunTests.ServiceAsync(worker.Host.Services, shop.TenantId, s => s.CreateFullAnalysisAsync(shop.ShopId, null, Ct))).RunId!.Value;
        await RunTests.WaitForStatusAsync(Db, shop, runId, Timeout, [worker], "awaiting_payment");
        await RunTests.ServiceAsync(worker.Host.Services, shop.TenantId, s => s.ApproveWithoutPaymentAsync(runId, Guid.CreateVersion7(), "test", Ct));
        return (runId, await RunTests.WaitForStatusAsync(Db, shop, runId, Timeout, [worker], RunTests.Final));
    }
}

/// <summary>The fixture whose one address resolves into an internal network (the answer of the real fetcher's guard).</summary>
internal sealed class BlockedFetcher(IPageFetcher inner, string path) : IPageFetcher
{
    public Task<FetchResponse> FetchAsync(Uri url, CancellationToken ct) =>
        url.AbsolutePath == path ? Task.FromResult(new FetchResponse { Url = url, Error = SsrfGuard.Error }) : inner.FetchAsync(url, ct);
}

/// <summary>The fixture, each request taking a while; records how many requests were in flight at once.</summary>
internal sealed class ConcurrencyFetcher(IPageFetcher inner, TimeSpan delay) : IPageFetcher
{
    private int _inFlight;
    private int _max;
    private int _requests;

    public int MaxInFlight => Volatile.Read(ref _max);

    public int Requests => Volatile.Read(ref _requests);

    public async Task<FetchResponse> FetchAsync(Uri url, CancellationToken ct)
    {
        Interlocked.Increment(ref _requests);
        var now = Interlocked.Increment(ref _inFlight);
        int seen;
        while (now > (seen = Volatile.Read(ref _max)) && Interlocked.CompareExchange(ref _max, now, seen) != seen)
        {
        }

        try
        {
            await Task.Delay(delay, ct);
            return await inner.FetchAsync(url, ct);
        }
        finally
        {
            Interlocked.Decrement(ref _inFlight);
        }
    }
}
