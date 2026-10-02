using EshopGuard.Jobs.Tests.Runs.Support;

namespace EshopGuard.Jobs.Tests.Runs;

/// <summary>
/// A worker killed in the middle of a run (design of change 8, task 13.3): its lease expires, another worker repeats the batch
/// and the run ends with the same pages and findings as without the crash, nothing written twice.
/// </summary>
public sealed class WorkerCrashTests(JobsTestDatabase database) : JobsTestBase(database)
{
    [Fact]
    public async Task CrashDuringTheCrawl_TheRunEndsWithTheSameResultAsWithoutIt()
    {
        var reference = await RunAsync(crash: false);
        var crashed = await RunAsync(crash: true);

        Assert.Equal(reference.Pages, crashed.Pages);
        Assert.Equal(reference.Versions, crashed.Versions);
        Assert.Equal(reference.Findings, crashed.Findings);
        Assert.Equal(reference.Occurrences, crashed.Occurrences);
    }

    private async Task<(long Pages, long Versions, long Findings, long Occurrences)> RunAsync(bool crash)
    {
        var shop = await RunTests.CreateShopAsync();
        var storage = Directory.CreateTempSubdirectory("eshopguard-crash-").FullName;
        var fetcher = new SlowFetcher(RunTests.SlovakSite(shop.BaseUrl), TimeSpan.FromMilliseconds(100));
        var settings = RunTests.WorkerSettings(storage, slots: 1);
        settings["Runs:FetchBatchPages"] = "4";
        settings["Scheduler:Enabled"] = "true";
        var workers = new List<TestWorker>
        {
            await Workers.StartAsync("a", settings, RunTests.Services(fetcher, new DeterministicTestJevClient())),
            await Workers.StartAsync("b", settings, RunTests.Services(fetcher, new DeterministicTestJevClient())),
        };
        var runId = (await RunTests.ServiceAsync(workers[0].Host.Services, shop.TenantId, s => s.CreateFullAnalysisAsync(shop.ShopId, null, Ct))).RunId!.Value;
        await RunTests.WaitForStatusAsync(Db, shop, runId, TimeSpan.FromSeconds(30), workers, "awaiting_payment");
        await RunTests.ServiceAsync(workers[0].Host.Services, shop.TenantId, s => s.ApproveWithoutPaymentAsync(runId, Guid.CreateVersion7(), "test", Ct));
        if (crash)
        {
            await RunTests.WaitForStatusAsync(Db, shop, runId, TimeSpan.FromSeconds(30), workers, "crawling");
            string? owner = null;
            while (owner is null)
            {
                owner = await Db.ScalarAsync<string>("SELECT lease_owner FROM ops.jobs WHERE run_id = $1 AND kind = 'run.fetch' AND state = 'running' LIMIT 1", runId);
                await Task.Delay(20, Ct);
            }

            await workers.Single(w => w.Id == owner).CrashAsync();
        }

        var status = await RunTests.WaitForStatusAsync(Db, shop, runId, TimeSpan.FromSeconds(120), workers, RunTests.Final);
        Assert.True(status is "finished" or "partial", status);
        foreach (var worker in workers)
        {
            await worker.StopAsync();
        }

        return (
            await RunTests.ScalarAsync<long>(Db, shop.TenantId, "SELECT count(*) FROM content.pages WHERE shop_id = $1", shop.ShopId),
            await RunTests.ScalarAsync<long>(Db, shop.TenantId, "SELECT count(*) FROM content.page_versions WHERE shop_id = $1", shop.ShopId),
            await RunTests.ScalarAsync<long>(Db, shop.TenantId, "SELECT count(*) FROM checks.findings WHERE shop_id = $1", shop.ShopId),
            await RunTests.ScalarAsync<long>(Db, shop.TenantId, "SELECT count(*) FROM checks.finding_occurrences WHERE shop_id = $1", shop.ShopId));
    }
}
