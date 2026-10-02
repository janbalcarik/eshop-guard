using EshopGuard.Core.Crawl;
using EshopGuard.Core.Jev;
using EshopGuard.Jobs.Tests.Runs.Support;

namespace EshopGuard.Jobs.Tests.Runs;

/// <summary>
/// Fairness of the queue between tenants (design of change 8, task 4.5): a full analysis of a big synthetic e-shop of one
/// tenant puts all its batches of Jev into the queue at once; the free sample of another tenant started later must not wait
/// for them. The cap of running Jev jobs per tenant (<c>Worker:TenantCaps:Jev</c>, 4 of the 8 slots) leaves slots to the
/// sample, so its batches run between those of the analysis and it ends first. Jev answers with an artificial latency.
/// </summary>
public sealed class FairnessTests(JobsTestDatabase database) : JobsTestBase(database)
{
    private const int TenantCap = 4;

    [Fact]
    public Task SampleOfAnotherTenant_DoesNotWaitForTheBatchesOfABigAnalysis() => RunAsync(products: 800, TimeSpan.FromMinutes(4));

    [Fact(Explicit = true)]
    public Task SampleOfAnotherTenant_DoesNotWaitForAnAnalysisOf5000Pages() => RunAsync(products: 5_000, TimeSpan.FromMinutes(20));

    private async Task RunAsync(int products, TimeSpan timeout)
    {
        var big = await RunTests.CreateShopAsync();
        var small = await RunTests.CreateShopAsync();
        var fetcher = new HostFetcher(new Dictionary<string, IPageFetcher>
        {
            [big.BaseUrl.Host] = new SyntheticShopFetcher(big.BaseUrl, products, "Tento výrobok je ekologický a šetrný k prírode."),
            [small.BaseUrl.Host] = RunTests.SlovakSite(small.BaseUrl),
        });
        var storage = Directory.CreateTempSubdirectory("eshopguard-fairness-").FullName;
        var settings = RunTests.WorkerSettings(storage);
        settings["Worker:Slots:Fetch"] = "8";
        settings["Worker:Slots:Cpu"] = "4";
        settings["Worker:Slots:Jev"] = "8";
        settings["Worker:TenantCaps:Jev"] = TenantCap.ToString(System.Globalization.CultureInfo.InvariantCulture);
        settings["Runs:FullAnalysis:MaxPages"] = (products + 100).ToString(System.Globalization.CultureInfo.InvariantCulture);
        settings["Runs:FullAnalysis:SampleProducts"] = (products + 100).ToString(System.Globalization.CultureInfo.InvariantCulture);
        settings["Runs:SieveBatchChunks"] = "20";
        settings["Runs:EvaluateBatchSegments"] = "40";
        var worker = await Workers.StartAsync("w0", settings, RunTests.Services(fetcher, new SlowJevClient(new DeterministicTestJevClient(), TimeSpan.FromMilliseconds(10))));

        var analysisId = (await RunTests.ServiceAsync(worker.Host.Services, big.TenantId, s => s.CreateFullAnalysisAsync(big.ShopId, null, Ct))).RunId!.Value;
        await RunTests.WaitForStatusAsync(Db, big, analysisId, TimeSpan.FromMinutes(2), [worker], "awaiting_payment");
        await RunTests.ServiceAsync(worker.Host.Services, big.TenantId, s => s.ApproveWithoutPaymentAsync(analysisId, Guid.CreateVersion7(), "test", Ct));
        await RunTests.WaitForStatusAsync(Db, big, analysisId, timeout, [worker], "evaluating");
        var waiting = (await Db.RowsAsync("SELECT id FROM ops.jobs WHERE run_id = $1 AND resource_class = 'jev' AND state = 'queued'", analysisId)).Select(r => (long)r[0]!).ToHashSet();

        var sampleId = (await RunTests.ServiceAsync(worker.Host.Services, small.TenantId, s => s.CreateFreeSampleAsync(small.ShopId, null, Ct))).RunId!.Value;
        var sampleStatus = await RunTests.WaitForStatusAsync(Db, small, sampleId, timeout, [worker], RunTests.Final);
        var analysisStatus = await RunTests.ScalarAsync<string>(Db, big.TenantId, "SELECT status FROM checks.runs WHERE id = $1", analysisId);

        Assert.True(sampleStatus is "finished" or "partial", sampleStatus);
        Assert.True(waiting.Count > TenantCap, $"the analysis had only {waiting.Count} batches of Jev waiting");
        Assert.DoesNotContain(analysisStatus, RunTests.Final);

        // The first batch of the sample started before all the batches of the analysis already waiting when the sample was
        // created (first in, first out would run them all first), and the analysis never ran more Jev jobs at once than its cap.
        var jobs = (await Db.RowsAsync(
            "SELECT id, run_id, started_at, finished_at FROM ops.jobs WHERE run_id IN ($1, $2) AND resource_class = 'jev' AND started_at IS NOT NULL", analysisId, sampleId))
            .Select(r => (Id: (long)r[0]!, Run: (Guid)r[1]!, Start: (DateTime)r[2]!, End: r[3] as DateTime?))
            .ToList();
        var sampleJev = jobs.Where(j => j.Run == sampleId).ToList();
        Assert.NotEmpty(sampleJev);
        Assert.Contains(jobs, j => waiting.Contains(j.Id) && j.Start > sampleJev.Min(s => s.Start));
        var analysisJev = jobs.Where(j => j.Run == analysisId).ToList();
        Assert.All(analysisJev, j => Assert.InRange(analysisJev.Count(o => o.Start <= j.Start && (o.End ?? DateTime.MaxValue) > j.Start), 1, TenantCap));

        TestContext.Current.TestOutputHelper!.WriteLine(
            $"{products} pages: {waiting.Count} Jev batches of the analysis waiting, the sample ran {sampleJev.Count} Jev batches; analysis {analysisStatus} when the sample ended.");
        await RunTests.ServiceAsync(worker.Host.Services, big.TenantId, s => s.RequestCancelAsync(analysisId, null, Ct));
    }

    /// <summary>The fixture of each host.</summary>
    private sealed class HostFetcher(Dictionary<string, IPageFetcher> byHost) : IPageFetcher
    {
        public Task<FetchResponse> FetchAsync(Uri url, CancellationToken ct) => byHost[url.Host].FetchAsync(url, ct);
    }

    /// <summary>Jev with a latency per call (the real one answers in tens of milliseconds to seconds).</summary>
    private sealed class SlowJevClient(IJevClient inner, TimeSpan latency) : IJevClient
    {
        public async Task<JevResult> EvaluateAsync(object state, IReadOnlyDictionary<string, JevQuestion> questions, CancellationToken ct)
        {
            await Task.Delay(latency, ct);
            return await inner.EvaluateAsync(state, questions, ct);
        }
    }
}
