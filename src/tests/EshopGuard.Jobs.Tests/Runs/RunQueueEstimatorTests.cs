using System.Text.Json.Nodes;
using EshopGuard.Data.Entities.Checks;
using EshopGuard.Data.Tenancy;
using EshopGuard.Jobs.Runs;
using EshopGuard.Jobs.Tests.Runs.Support;
using Microsoft.Extensions.DependencyInjection;

namespace EshopGuard.Jobs.Tests.Runs;

/// <summary>Position in the queue and the estimated end of a run (design of change 8, "Pozice ve frontě"; tasks 10.2 and 10.5).</summary>
public sealed class RunQueueEstimatorTests(JobsTestDatabase database) : JobsTestBase(database)
{
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task TwoEarlierSamplesWaiting_PositionIsTwo_AndWithoutHistoryNoEstimate()
    {
        var storage = Directory.CreateTempSubdirectory("eshopguard-queue-").FullName;
        var worker = await Workers.StartAsync("w0", RunTests.WorkerSettings(storage, slots: 0), RunTests.Services(RunTests.SlovakSite(new Uri("http://unused.test/")), new DeterministicTestJevClient()));
        var shops = new List<RunShop>();
        var runs = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            var shop = await RunTests.CreateShopAsync();
            shops.Add(shop);
            runs.Add((await RunTests.ServiceAsync(worker.Host.Services, shop.TenantId, s => s.CreateFreeSampleAsync(shop.ShopId, null, Ct))).RunId!.Value);
        }

        var first = await EstimateAsync(worker, shops[0], runs[0]);
        var third = await EstimateAsync(worker, shops[2], runs[2]);
        var foreign = await EstimateAsync(worker, shops[0], runs[2]);

        Assert.Equal(new RunQueueEstimate(0, null), first);
        Assert.Equal(new RunQueueEstimate(2, null), third);
        Assert.Equal(new RunQueueEstimate(null, null), foreign);
    }

    [Fact]
    public async Task AfterASampleEndedWithinTheHour_TheEstimateComesFromItsSteps()
    {
        var storage = Directory.CreateTempSubdirectory("eshopguard-queue-").FullName;
        var done = await RunTests.CreateShopAsync();
        var worker = await Workers.StartAsync("w0", RunTests.WorkerSettings(storage), RunTests.Services(RunTests.SlovakSite(done.BaseUrl), new DeterministicTestJevClient()));
        var finished = (await RunTests.ServiceAsync(worker.Host.Services, done.TenantId, s => s.CreateFreeSampleAsync(done.ShopId, null, Ct))).RunId!.Value;
        await RunTests.WaitForStatusAsync(Db, done, finished, TimeSpan.FromSeconds(90), [worker], RunTests.Final);
        await worker.Host.StopAsync(Ct);

        var waiting = new List<(RunShop Shop, Guid RunId)>();
        for (var i = 0; i < 2; i++)
        {
            var shop = await RunTests.CreateShopAsync();
            waiting.Add((shop, (await RunTests.ServiceAsync(worker.Host.Services, shop.TenantId, s => s.CreateFreeSampleAsync(shop.ShopId, null, Ct))).RunId!.Value));
        }

        var before = DateTimeOffset.UtcNow;
        var head = await EstimateAsync(worker, waiting[0].Shop, waiting[0].RunId);
        var second = await EstimateAsync(worker, waiting[1].Shop, waiting[1].RunId);

        Assert.Equal(0, head.Position);
        Assert.Equal(1, second.Position);
        Assert.NotNull(head.EstimatedFinishAt);
        Assert.True(head.EstimatedFinishAt >= before, head.EstimatedFinishAt.ToString());
        Assert.True(second.EstimatedFinishAt > head.EstimatedFinishAt, "a run ahead adds its duration");
        Assert.Equal(new RunQueueEstimate(null, null), await EstimateAsync(worker, done, finished));
    }

    [Fact]
    public void Estimate_AddsTheRestOfTheCurrentStep_TheStepsAfterIt_AndAWholeRunPerRunAhead()
    {
        var steps = new Dictionary<string, double>
        {
            [RunJobKinds.Discover] = 2, [RunJobKinds.Markets] = 3, [RunJobKinds.Fetch] = 40, [RunJobKinds.Segment] = 5,
            [RunJobKinds.Sieve] = 10, [RunJobKinds.Evaluate] = 20, [RunJobKinds.Rules] = 4, [RunJobKinds.Rewrite] = 6, [RunJobKinds.Finalize] = 1,
        };
        var whole = steps.Values.Sum();
        var crawling = new JsonObject { ["pages_planned"] = 100, ["pages_fetched"] = 25 };

        Assert.Equal(Now + TimeSpan.FromSeconds(whole + (2 * whole)), RunQueueEstimator.Estimate(RunStatus.Queued, new JsonObject(), 2, steps, Now));
        Assert.Equal(Now + TimeSpan.FromSeconds((40 * 0.75) + 5 + 10 + 20 + 4 + 6 + 1), RunQueueEstimator.Estimate(RunStatus.Crawling, crawling, 0, steps, Now));
        Assert.Null(RunQueueEstimator.Estimate(RunStatus.Queued, new JsonObject(), 0, null, Now));
    }

    [Theory]
    [InlineData("run.evaluate", 3, 4, 0.25)]
    [InlineData("run.evaluate", 4, 4, 0)]
    [InlineData("run.sieve", 0, 0, 1)]
    [InlineData("run.rules", 0, 0, 1)]
    public void Remaining_IsTheShareOfBatchesStillToDo(string kind, int done, int total, double expected)
    {
        var progress = new JsonObject { ["steps"] = new JsonObject { [kind[4..]] = new JsonObject { ["done"] = done, ["total"] = total } } };

        Assert.Equal(expected, RunQueueEstimator.Remaining(kind, progress), 6);
    }

    private static async Task<RunQueueEstimate> EstimateAsync(TestWorker worker, RunShop shop, Guid runId)
    {
        await using var scope = worker.Host.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(shop.TenantId);
        return await scope.ServiceProvider.GetRequiredService<IRunQueueEstimator>().EstimateAsync(runId, Ct);
    }
}
