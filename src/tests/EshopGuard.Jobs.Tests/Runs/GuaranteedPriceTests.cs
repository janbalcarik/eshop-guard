using System.Diagnostics.Metrics;
using System.Text.Json;
using EshopGuard.Data.Entities.Checks;
using EshopGuard.Data.Tenancy;
using EshopGuard.Jobs.Runs;
using EshopGuard.Jobs.Tests.Runs.Support;
using Microsoft.Extensions.DependencyInjection;

namespace EshopGuard.Jobs.Tests.Runs;

/// <summary>
/// The price of a full analysis is guaranteed by its order: a run that costs more than its internal estimate checks everything,
/// sends only an operational warning and changes nothing for the customer; the customer never sees an internal amount
/// (design of change 8, "Garantovaná cena"; tasks 8.3 and 8.6).
/// </summary>
public sealed class GuaranteedPriceTests(JobsTestDatabase database) : JobsTestBase(database)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(90);

    [Fact]
    public async Task RunOverItsEstimate_IsNotStopped_WarnsOperations_AndShowsTheCustomerNoInternalAmount()
    {
        long alerts = 0;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Name == "eshopguard.run.cost_over_estimate")
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, value, _, _) => Interlocked.Add(ref alerts, value));
        listener.Start();

        var shop = await RunTests.CreateShopAsync();
        var storage = Directory.CreateTempSubdirectory("eshopguard-price-").FullName;
        var settings = RunTests.WorkerSettings(storage);
        settings["Runs:CostAlertRatio"] = "0.000001";
        var worker = await Workers.StartAsync("w0", settings, RunTests.Services(RunTests.SlovakSite(shop.BaseUrl), new DeterministicTestJevClient()));
        var runId = (await RunTests.ServiceAsync(worker.Host.Services, shop.TenantId, s => s.CreateFullAnalysisAsync(shop.ShopId, null, Ct))).RunId!.Value;
        Assert.Equal("awaiting_payment", await RunTests.WaitForStatusAsync(Db, shop, runId, Timeout, [worker], [.. RunTests.Final, "awaiting_payment"]));
        await RunTests.ServiceAsync(worker.Host.Services, shop.TenantId, s => s.ApproveWithoutPaymentAsync(runId, Guid.CreateVersion7(), "test", Ct));

        var status = await RunTests.WaitForStatusAsync(Db, shop, runId, Timeout, [worker], RunTests.Final);

        Assert.True(status is "finished" or "partial", status);
        Assert.True(await RunTests.ScalarAsync<decimal>(Db, shop.TenantId, "SELECT coalesce(sum(cost_usd), 0) FROM usage.usage_records WHERE run_id = $1", runId) > 0);
        Assert.Contains(worker.Logs.Logs, l => l.Message.Contains("run.cost_over_estimate", StringComparison.Ordinal));
        listener.RecordObservableInstruments();
        Assert.True(Interlocked.Read(ref alerts) >= 1, "metric eshopguard.run.cost_over_estimate");
        Assert.Equal(0L, await RunTests.ScalarAsync<long>(Db, shop.TenantId, "SELECT count(*) FROM billing.orders WHERE shop_id = $1", shop.ShopId));

        var view = await ReadAsync(worker, shop, m => m.GetAsync(runId, Ct));
        Assert.NotNull(view);
        Assert.Equal(runId, view.Id);
        Assert.True(view.PagesChecked > 0);
        var json = JsonSerializer.Serialize(view);
        foreach (var hidden in new[] { "usd", "internal", "jev", "typesafe", "openai", "token", "cost" })
        {
            Assert.DoesNotContain(hidden, json, StringComparison.OrdinalIgnoreCase);
        }

        var events = await RunTests.RowsAsync(Db, shop.TenantId, "SELECT data::text FROM checks.run_events WHERE run_id = $1", runId);
        Assert.DoesNotContain(events, e => ((string)e[0]!).Contains("usd", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(await RunTests.ScalarAsync<string>(Db, shop.TenantId, "SELECT estimate -> 'internal' ->> 'total_usd' FROM checks.runs WHERE id = $1", runId));
    }

    [Fact]
    public async Task ReadModel_GivesTheNewestRunOfAKind_AndNothingOfAnotherTenant()
    {
        var shop = await RunTests.CreateShopAsync();
        var other = await RunTests.CreateShopAsync();
        var storage = Directory.CreateTempSubdirectory("eshopguard-price-").FullName;
        var worker = await Workers.StartAsync("w0", RunTests.WorkerSettings(storage), RunTests.Services(RunTests.SlovakSite(shop.BaseUrl), new DeterministicTestJevClient()));
        var runId = (await RunTests.ServiceAsync(worker.Host.Services, shop.TenantId, s => s.CreateFreeSampleAsync(shop.ShopId, null, Ct))).RunId!.Value;
        await RunTests.WaitForStatusAsync(Db, shop, runId, Timeout, [worker], RunTests.Final);

        var latest = await ReadAsync(worker, shop, m => m.GetLatestAsync(shop.ShopId, RunKind.FreeSample, Ct));
        var none = await ReadAsync(worker, shop, m => m.GetLatestAsync(shop.ShopId, RunKind.FullAnalysis, Ct));
        var foreign = await ReadAsync(worker, other, m => m.GetAsync(runId, Ct));

        Assert.Equal(runId, latest!.Id);
        Assert.NotNull(latest.Sample);
        Assert.NotNull(latest.ScopeBasis);
        Assert.NotNull(latest.FinishedAt);
        Assert.True(latest.Progress.PagesFetched > 0);
        Assert.Null(none);
        Assert.Null(foreign);
    }

    private static async Task<RunView?> ReadAsync(TestWorker worker, RunShop shop, Func<IRunReadModel, Task<RunView?>> read)
    {
        await using var scope = worker.Host.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(shop.TenantId);
        return await read(scope.ServiceProvider.GetRequiredService<IRunReadModel>());
    }
}
