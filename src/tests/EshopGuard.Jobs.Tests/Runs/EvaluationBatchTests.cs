using EshopGuard.Core.Jev;
using EshopGuard.Jobs.Runs;
using EshopGuard.Jobs.Tests.Runs.Support;

namespace EshopGuard.Jobs.Tests.Runs;

/// <summary>
/// Batches of Jev (design of change 8, task 5.7): the estimate is stored before the first paid call, a second run over the
/// same texts pays nothing, a free sample over its cap pays nothing at all.
/// </summary>
public sealed class EvaluationBatchTests(JobsTestDatabase database) : JobsTestBase(database)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(90);

    [Fact]
    public async Task Estimate_IsStoredBeforeTheFirstCall_AndASecondRunTakesEveryAnswerFromTheCache()
    {
        var shop = await RunTests.CreateShopAsync();
        var jev = new FirstCallProbe(Db);
        var storage = Directory.CreateTempSubdirectory("eshopguard-eval-").FullName;
        var worker = await Workers.StartAsync("w0", RunTests.WorkerSettings(storage), RunTests.Services(RunTests.SlovakSite(shop.BaseUrl), jev));

        var first = await RunFullAsync(worker, shop);
        var callsOfFirst = jev.Calls;
        var second = await RunFullAsync(worker, shop);

        Assert.Equal("segmented", jev.BasisAtFirstCall);
        Assert.True(jev.CallsUpperAtFirstCall > 0);
        Assert.True(callsOfFirst > 0);
        Assert.Equal(callsOfFirst, jev.Calls);
        Assert.Equal(0L, await UsageAsync(shop, second, "calls"));
        Assert.True(await UsageAsync(shop, second, "cache_hits") > 0);
        Assert.True(await UsageAsync(shop, first, "calls") > 0);
    }

    [Fact]
    public async Task FreeSampleOverItsCap_FailsWithoutAnyCall()
    {
        var shop = await RunTests.CreateShopAsync();
        var jev = new DeterministicTestJevClient();
        var storage = Directory.CreateTempSubdirectory("eshopguard-eval-").FullName;
        var settings = RunTests.WorkerSettings(storage);
        settings["Runs:FreeSample:MaxInternalUsd"] = "0.0000001";
        var worker = await Workers.StartAsync("w0", settings, RunTests.Services(RunTests.SlovakSite(shop.BaseUrl), jev));
        var runId = (await RunTests.ServiceAsync(worker.Host.Services, shop.TenantId, s => s.CreateFreeSampleAsync(shop.ShopId, null, Ct))).RunId!.Value;

        Assert.Equal("failed", await RunTests.WaitForStatusAsync(Db, shop, runId, Timeout, [worker], RunTests.Final));
        Assert.Equal(RunCodes.SampleBudgetExceeded, await RunTests.ScalarAsync<string>(Db, shop.TenantId, "SELECT error FROM checks.runs WHERE id = $1", runId));
        Assert.Equal(0, jev.Calls);
        Assert.Equal(0L, await RunTests.ScalarAsync<long>(Db, shop.TenantId, "SELECT coalesce(sum(calls), 0)::bigint FROM usage.usage_records WHERE run_id = $1 AND provider = 'jev'", runId));
        Assert.Equal(0m, await RunTests.ScalarAsync<decimal>(Db, shop.TenantId, "SELECT coalesce(sum(cost_usd), 0) FROM usage.usage_records WHERE run_id = $1", runId));
        Assert.Equal(0L, await Db.ScalarAsync<long>("SELECT count(*) FROM ops.jobs WHERE run_id = $1 AND kind IN ('run.sieve', 'run.evaluate', 'run.rewrite')", runId));
    }

    private async Task<Guid> RunFullAsync(TestWorker worker, RunShop shop)
    {
        var runId = (await RunTests.ServiceAsync(worker.Host.Services, shop.TenantId, s => s.CreateFullAnalysisAsync(shop.ShopId, null, Ct))).RunId!.Value;
        await RunTests.WaitForStatusAsync(Db, shop, runId, Timeout, [worker], "awaiting_payment");
        await RunTests.ServiceAsync(worker.Host.Services, shop.TenantId, s => s.ApproveWithoutPaymentAsync(runId, Guid.CreateVersion7(), "test", Ct));
        Assert.Equal("finished", await RunTests.WaitForStatusAsync(Db, shop, runId, Timeout, [worker], RunTests.Final));
        return runId;
    }

    private Task<long> UsageAsync(RunShop shop, Guid runId, string column) =>
        RunTests.ScalarAsync<long>(Db, shop.TenantId, $"SELECT coalesce(sum({column}), 0)::bigint FROM usage.usage_records WHERE run_id = $1 AND provider = 'jev'", runId);
}

/// <summary>The Jev of the tests; at its first call it reads the internal estimate of the run that asks.</summary>
internal sealed class FirstCallProbe(JobsTestDatabase db) : IJevClient
{
    private readonly DeterministicTestJevClient _inner = new();
    private readonly SemaphoreSlim _first = new(1, 1);
    private bool _probed;

    public string? BasisAtFirstCall { get; private set; }

    public long CallsUpperAtFirstCall { get; private set; }

    public int Calls => _inner.Calls;

    public async Task<JevResult> EvaluateAsync(object state, IReadOnlyDictionary<string, JevQuestion> questions, CancellationToken ct)
    {
        if (!_probed)
        {
            await _first.WaitAsync(ct);
            try
            {
                if (!_probed && RunAmbient.Current is { } run)
                {
                    var rows = await RunTests.RowsAsync(db, run.TenantId,
                        "SELECT estimate -> 'internal' ->> 'basis', (estimate -> 'internal' ->> 'jev_calls_upper')::bigint FROM checks.runs WHERE id = $1", run.RunId);
                    BasisAtFirstCall = (string?)rows[0][0];
                    CallsUpperAtFirstCall = (long?)rows[0][1] ?? 0;
                    _probed = true;
                }
            }
            finally
            {
                _first.Release();
            }
        }

        return await _inner.EvaluateAsync(state, questions, ct);
    }
}
