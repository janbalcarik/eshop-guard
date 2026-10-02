using System.Diagnostics;
using EshopGuard.Jobs.Tests.Runs.Support;

namespace EshopGuard.Jobs.Tests.Runs;

/// <summary>
/// Memory of a full analysis of a synthetic e-shop with 5 000 product pages (design of change 8, task 5.8): while the run is
/// segmenting (<c>run.segment</c>) and ruling (<c>run.rules</c>), the peak of the live managed objects (after a full
/// collection, every 500 ms), of the managed heap with its garbage and of the working set of the whole test process, written
/// to the output of the test. A measurement, so it runs only on demand (about two minutes).
/// </summary>
public sealed class RulesMemoryTests(JobsTestDatabase database) : JobsTestBase(database)
{
    public const int Products = 5_000;

    [Fact(Explicit = true)]
    public async Task FullAnalysisOf5000Pages_PeakMemoryOfSegmentAndRules()
    {
        var shop = await RunTests.CreateShopAsync();
        var storage = Directory.CreateTempSubdirectory("eshopguard-memory-").FullName;
        var settings = RunTests.WorkerSettings(storage, slots: 4);
        settings["Runs:FullAnalysis:MaxPages"] = "5100";
        settings["Runs:FullAnalysis:SampleProducts"] = "5100";
        var fetcher = new SyntheticShopFetcher(shop.BaseUrl, Products, "Tento výrobok je ekologický a šetrný k prírode.");
        var worker = await Workers.StartAsync("w0", settings, RunTests.Services(fetcher, new DeterministicTestJevClient()));

        var runId = (await RunTests.ServiceAsync(worker.Host.Services, shop.TenantId, s => s.CreateFullAnalysisAsync(shop.ShopId, null, Ct))).RunId!.Value;
        await RunTests.WaitForStatusAsync(Db, shop, runId, TimeSpan.FromMinutes(2), [worker], "awaiting_payment");
        await RunTests.ServiceAsync(worker.Host.Services, shop.TenantId, s => s.ApproveWithoutPaymentAsync(runId, Guid.CreateVersion7(), "test", Ct));

        var samples = new Dictionary<string, (long Live, long Heap, long WorkingSet, Stopwatch Time)>();
        var tick = 0;
        var process = Process.GetCurrentProcess();
        string status;
        var deadline = DateTime.UtcNow + TimeSpan.FromMinutes(30);
        do
        {
            status = await RunTests.ScalarAsync<string>(Db, shop.TenantId, "SELECT status FROM checks.runs WHERE id = $1", runId);
            process.Refresh();
            var heap = GC.GetTotalMemory(forceFullCollection: false);
            var live = tick++ % 10 == 0 ? GC.GetTotalMemory(forceFullCollection: true) : 0;
            if (!samples.TryGetValue(status, out var sample))
            {
                sample = (0, 0, 0, Stopwatch.StartNew());
            }

            samples[status] = (Math.Max(sample.Live, live), Math.Max(sample.Heap, heap), Math.Max(sample.WorkingSet, process.WorkingSet64), sample.Time);
            foreach (var other in samples.Where(s => s.Key != status))
            {
                other.Value.Time.Stop();
            }

            await Task.Delay(50, Ct);
        }
        while (!RunTests.Final.Contains(status) && DateTime.UtcNow < deadline);

        var pages = await RunTests.ScalarAsync<long>(Db, shop.TenantId, "SELECT count(*) FROM content.pages WHERE shop_id = $1", shop.ShopId);
        var output = TestContext.Current.TestOutputHelper!;
        output.WriteLine($"Run {runId}: {status}, {pages} pages, {fetcher.Requests} requests.");
        foreach (var (name, sample) in samples)
        {
            output.WriteLine(
                $"{name,-12} {sample.Time.Elapsed.TotalSeconds,7:0.0} s  live peak {sample.Live / 1048576.0,7:0.0} MB  heap with garbage {sample.Heap / 1048576.0,7:0.0} MB  working set {sample.WorkingSet / 1048576.0,7:0.0} MB");
        }

        Assert.True(status is "finished" or "partial", status);
        Assert.True(pages >= Products, $"pages {pages}");
        Assert.Contains("segmenting", samples.Keys);
        Assert.Contains("ruling", samples.Keys);
    }
}
