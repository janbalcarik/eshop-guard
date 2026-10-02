using EshopGuard.Core.Crawl;
using EshopGuard.Jobs.Runs;
using EshopGuard.Jobs.Tests.Runs.Support;

namespace EshopGuard.Jobs.Tests.Runs;

/// <summary>What was not checked decides the end of a run (design of change 8, tasks 11.3 and 11.6; fail-closed).</summary>
public sealed class PartialRunTests(JobsTestDatabase database) : JobsTestBase(database)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(90);

    [Fact]
    public async Task PagesThatFail_MakeTheRunPartial_WithTheirCount()
    {
        var (shop, runId, status, _) = await RunAsync(url => url.AbsolutePath.StartsWith("/produkt-1", StringComparison.Ordinal) ? 500 : null);

        Assert.Equal("partial", status);
        var stats = await RunTests.RunJsonAsync(Db, shop, runId, "stats");
        var failed = (long)stats["unchecked"]!["failed"]!;
        Assert.True(failed >= 1, stats.ToJsonString());
        Assert.Equal(1L, await RunTests.ScalarAsync<long>(Db, shop.TenantId, "SELECT count(*) FROM checks.run_events WHERE run_id = $1 AND code = 'run.partial'", runId));
    }

    [Fact]
    public async Task RobotsForbiddingEverything_FailsTheRun_BeforeAnyPage()
    {
        var (shop, runId, status, fetcher) = await RunAsync(_ => null, robots: "User-agent: *\nDisallow: /\n");

        Assert.Equal("failed", status);
        Assert.Equal(RunCodes.RobotsDisallowAll, await RunTests.ScalarAsync<string>(Db, shop.TenantId, "SELECT error FROM checks.runs WHERE id = $1", runId));
        Assert.DoesNotContain(fetcher.Requested, u => u.AbsolutePath is not "/robots.txt");
        Assert.Equal(0L, await RunTests.ScalarAsync<long>(Db, shop.TenantId, "SELECT count(*) FROM content.pages WHERE shop_id = $1", shop.ShopId));
    }

    private async Task<(RunShop Shop, Guid RunId, string Status, FaultyFetcher Fetcher)> RunAsync(Func<Uri, int?> fail, string? robots = null)
    {
        var shop = await RunTests.CreateShopAsync();
        var storage = Directory.CreateTempSubdirectory("eshopguard-partial-").FullName;
        var fetcher = new FaultyFetcher(RunTests.SlovakSite(shop.BaseUrl), fail, robots);
        var worker = await Workers.StartAsync("w0", RunTests.WorkerSettings(storage), RunTests.Services(fetcher, new DeterministicTestJevClient()));
        var runId = (await RunTests.ServiceAsync(worker.Host.Services, shop.TenantId, s => s.CreateFullAnalysisAsync(shop.ShopId, null, Ct))).RunId!.Value;
        var waiting = await RunTests.WaitForStatusAsync(Db, shop, runId, Timeout, [worker], [.. RunTests.Final, "awaiting_payment"]);
        if (waiting == "awaiting_payment")
        {
            await RunTests.ServiceAsync(worker.Host.Services, shop.TenantId, s => s.ApproveWithoutPaymentAsync(runId, Guid.CreateVersion7(), "test", Ct));
        }

        var status = await RunTests.WaitForStatusAsync(Db, shop, runId, Timeout, [worker], RunTests.Final);
        return (shop, runId, status, fetcher);
    }
}

/// <summary>The fixture with failing pages (an HTTP status) and, optionally, its own robots.txt.</summary>
internal sealed class FaultyFetcher(IPageFetcher inner, Func<Uri, int?> fail, string? robots) : IPageFetcher
{
    public System.Collections.Concurrent.ConcurrentQueue<Uri> Requested { get; } = new();

    public Task<FetchResponse> FetchAsync(Uri url, CancellationToken ct)
    {
        Requested.Enqueue(url);
        if (robots is not null && url.AbsolutePath == "/robots.txt")
        {
            return Task.FromResult(new FetchResponse { Url = url, StatusCode = 200, MediaType = "text/plain", Body = System.Text.Encoding.UTF8.GetBytes(robots) });
        }

        return fail(url) is { } status ? Task.FromResult(new FetchResponse { Url = url, StatusCode = status }) : inner.FetchAsync(url, ct);
    }
}
