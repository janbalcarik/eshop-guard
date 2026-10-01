using EshopGuard.Data.Entities.Ops;
using EshopGuard.Jobs.Queue;
using EshopGuard.Jobs.Workers;

namespace EshopGuard.Jobs.Tests;

/// <summary>The four tests of the source document (part 5): no double processing, killed worker, stuck worker, domain turns.</summary>
public sealed class NoDoubleProcessingTests(JobsTestDatabase db) : JobsTestBase(db)
{
    [Fact]
    public async Task FourWorkersWithEightSlots_ProcessThousandJobsExactlyOnce()
    {
        await Direct.EnqueueManyAsync(Enumerable.Range(0, 1000).Select(_ => TestJobs.Request(TestHandlers.Record)));
        await Workers.StartManyAsync(4, new Dictionary<string, string?> { ["Worker:Slots:Cpu"] = "8" });

        await Workers.WaitForEmptyQueueAsync(TimeSpan.FromSeconds(120));

        Assert.Equal(1000L, await Db.ScalarAsync<long>("SELECT count(*) FROM jobs_test.effects"));
        Assert.Equal(1000L, await Db.ScalarAsync<long>("SELECT count(DISTINCT job_id) FROM jobs_test.effects"));
        Assert.Equal(1000L, await Db.ScalarAsync<long>("SELECT count(*) FROM ops.jobs WHERE state = 'succeeded' AND attempts = 1"));
        Assert.True(await Db.ScalarAsync<long>("SELECT count(DISTINCT worker_id) FROM jobs_test.effects") > 1, "work was spread over the workers");
    }
}

public sealed class KilledWorkerTests(JobsTestDatabase db) : JobsTestBase(db)
{
    [Fact]
    public async Task KilledWorker_JobReturnsAfterLease_AndAnotherWorkerFinishesIt()
    {
        var jobId = (await Direct.EnqueueAsync(TestJobs.Request(TestHandlers.Block, payload: new { blockAttempts = new[] { 1 } }))).JobId;
        var a = await Workers.StartAsync("a", new Dictionary<string, string?> { ["Worker:Slots:Cpu"] = "1" });
        await Workers.WaitUntilAsync(() => Task.FromResult(!State.Blocked.IsEmpty), TimeSpan.FromSeconds(10), "worker A took the job");

        await a.CrashAsync();
        Assert.Equal("running", await Db.ScalarAsync<string>("SELECT state FROM ops.jobs WHERE id = $1", jobId));

        var b = await Workers.StartAsync("b", new Dictionary<string, string?> { ["Worker:Slots:Cpu"] = "1", ["Scheduler:Enabled"] = "true" });
        await Workers.WaitUntilAsync(
            async () => await Db.ScalarAsync<string>("SELECT last_error FROM ops.jobs WHERE id = $1", jobId) == JobErrorCodes.LeaseExpired,
            TimeSpan.FromSeconds(15), "the scheduler returned the job");
        await Workers.WaitUntilAsync(
            async () => await Db.ScalarAsync<string>("SELECT state FROM ops.jobs WHERE id = $1", jobId) == "succeeded",
            TimeSpan.FromSeconds(15), "worker B finished the job");

        Assert.Equal(2, await Db.ScalarAsync<int>("SELECT attempts FROM ops.jobs WHERE id = $1", jobId));
        var effects = await Db.RowsAsync("SELECT worker_id, attempt FROM jobs_test.effects WHERE job_id = $1", jobId);
        var effect = Assert.Single(effects);
        Assert.Equal(b.Id, effect[0]);
        Assert.Equal(2, effect[1]);
    }
}

public sealed class StuckWorkerTests(JobsTestDatabase db) : JobsTestBase(db)
{
    [Fact]
    public async Task StuckWorker_LosesTheLease_AndCannotWriteItsResult()
    {
        var jobId = (await Direct.EnqueueAsync(TestJobs.Request(TestHandlers.Block, payload: new { blockAttempts = new[] { 1 }, ignoreCancellation = 1 }))).JobId;
        var a = await Workers.StartAsync("a", new Dictionary<string, string?> { ["Worker:Slots:Cpu"] = "1" });
        await Workers.WaitUntilAsync(() => Task.FromResult(!State.Blocked.IsEmpty), TimeSpan.FromSeconds(10), "worker A took the job");
        a.Processing.SuspendJobHeartbeats();

        var b = await Workers.StartAsync("b", new Dictionary<string, string?> { ["Worker:Slots:Cpu"] = "1", ["Scheduler:Enabled"] = "true" });
        await Workers.WaitUntilAsync(
            async () => await Db.ScalarAsync<string>("SELECT state FROM ops.jobs WHERE id = $1", jobId) == "succeeded",
            TimeSpan.FromSeconds(20), "worker B took over and finished the job");

        // Worker A wakes up and tries to complete its attempt 1.
        State.Gate.TrySetResult();
        await Workers.WaitUntilAsync(() => Task.FromResult(!State.Errors.IsEmpty), TimeSpan.FromSeconds(10), "worker A got LeaseLostException");

        Assert.IsType<LeaseLostException>(Assert.Single(State.Errors));
        var row = Assert.Single(await Db.RowsAsync("SELECT state, attempts, lease_owner FROM ops.jobs WHERE id = $1", jobId));
        Assert.Equal(["succeeded", 2, null], row);
        var effect = Assert.Single(await Db.RowsAsync("SELECT worker_id, attempt FROM jobs_test.effects WHERE job_id = $1", jobId));
        Assert.Equal([b.Id, 2], effect);
        Assert.True(a.Logs.Contains(JobErrorCodes.LeaseLost));
    }
}

public sealed class DomainAlternationTests(JobsTestDatabase db) : JobsTestBase(db)
{
    [Fact]
    public async Task TwoTenantsOnOneDomain_TakeTurnsBatchByBatch_AndNeverOverlap()
    {
        var runA = await TestRuns.CreateAsync("shop.test");
        var runB = await TestRuns.CreateAsync("shop.test");
        var key = JobKeys.Domain("www.Shop.test");
        Assert.Equal("domain:shop.test", key);
        await Direct.EnqueueManyAsync(
        [
            TestJobs.Request(TestHandlers.FetchDomain, JobResourceClass.Fetch, payload: new { domain = "shop.test", batch = 1, batches = 5, tag = "A" },
                tenantId: runA.TenantId, shopId: runA.ShopId, runId: runA.RunId, concurrencyKey: key),
            TestJobs.Request(TestHandlers.FetchDomain, JobResourceClass.Fetch, payload: new { domain = "shop.test", batch = 1, batches = 5, tag = "B" },
                tenantId: runB.TenantId, shopId: runB.ShopId, runId: runB.RunId, concurrencyKey: key),
        ]);

        await Workers.StartManyAsync(2, new Dictionary<string, string?> { ["Worker:Slots:Fetch"] = "4" });
        await Workers.WaitUntilAsync(
            async () => await Db.ScalarAsync<long>("SELECT count(*) FROM jobs_test.effects") == 10,
            TimeSpan.FromSeconds(60), "ten batches");
        await Workers.WaitForEmptyQueueAsync(TimeSpan.FromSeconds(10));

        var intervals = State.Intervals.OrderBy(i => i.Start).ToList();
        Assert.Equal("ABABABABAB", string.Concat(intervals.Select(i => i.Tag)));
        for (var i = 1; i < intervals.Count; i++)
        {
            Assert.True(intervals[i].Start >= intervals[i - 1].End, $"batch {i} overlaps the previous one");
        }

        Assert.Null(await Db.ScalarAsync<long?>("SELECT lease_job_id FROM ops.domains WHERE domain = 'shop.test'"));
        Assert.Equal(250, await Db.ScalarAsync<int>("SELECT crawl_delay_ms FROM ops.domains WHERE domain = 'shop.test'"));
    }
}
