using EshopGuard.Data.Connections;
using EshopGuard.Data.Entities.Ops;
using EshopGuard.Jobs.Queue;
using Microsoft.EntityFrameworkCore;

namespace EshopGuard.Jobs.Tests;

public sealed class CancelTests(JobsTestDatabase db) : JobsTestBase(db)
{
    [Fact]
    public async Task CancelingARun_CancelsItsWaitingJobs_InTheSameTransaction()
    {
        var run = await TestRuns.CreateAsync();
        await Direct.EnqueueManyAsync(Enumerable.Range(0, 5).Select(_ => TestJobs.Request(TestHandlers.Record, tenantId: run.TenantId, runId: run.RunId)));
        var other = (await Direct.EnqueueAsync(TestJobs.Request(TestHandlers.Record, notBefore: DateTimeOffset.UtcNow.AddMinutes(5)))).JobId;

        Assert.Equal(5, await CancelRunAsync(run));

        Assert.Equal(5L, await Db.ScalarAsync<long>("SELECT count(*) FROM ops.jobs WHERE run_id = $1 AND state = 'canceled' AND finished_at IS NOT NULL", run.RunId));
        Assert.Equal("queued", await Db.ScalarAsync<string>("SELECT state FROM ops.jobs WHERE id = $1", other));
        await Workers.StartAsync("w", new Dictionary<string, string?> { ["Worker:Slots:Cpu"] = "4" });
        await Task.Delay(1000, Ct);
        Assert.Equal(0L, await Db.ScalarAsync<long>("SELECT count(*) FROM jobs_test.effects"));
    }

    [Fact]
    public async Task RunningJobOfACanceledRun_EndsCanceled_WithinOneHeartbeat()
    {
        var run = await TestRuns.CreateAsync();
        var id = (await Direct.EnqueueAsync(TestJobs.Request(TestHandlers.CancelAware, tenantId: run.TenantId, runId: run.RunId))).JobId;
        await Workers.StartAsync("w", new Dictionary<string, string?> { ["Worker:Slots:Cpu"] = "1" });
        await Workers.WaitUntilAsync(() => Task.FromResult(!State.Blocked.IsEmpty), TimeSpan.FromSeconds(10), "the handler started");

        await CancelRunAsync(run);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await Workers.WaitUntilAsync(
            async () => await Db.ScalarAsync<string>("SELECT state FROM ops.jobs WHERE id = $1", id) == "canceled",
            TimeSpan.FromSeconds(5), "the job ended canceled");

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1.5), $"took {stopwatch.Elapsed}, heartbeat is 0.5 s");
        Assert.Equal(1L, await Db.ScalarAsync<long>("SELECT count(*) FROM ops.jobs"));
        Assert.Null(await Db.ScalarAsync<string>("SELECT lease_owner FROM ops.jobs WHERE id = $1", id));
    }

    [Fact]
    public async Task Heartbeat_ReportsTheCancellation()
    {
        var run = await TestRuns.CreateAsync();
        await Direct.EnqueueAsync(TestJobs.Request(TestHandlers.Record, tenantId: run.TenantId, runId: run.RunId));
        var job = Assert.Single(await Direct.Queue.ClaimAsync(JobResourceClass.Cpu, 1, "w", Ct));
        Assert.Equal(new HeartbeatResult(true, false), await Direct.Queue.HeartbeatAsync(job, Ct));

        await CancelRunAsync(run);

        Assert.Equal(new HeartbeatResult(true, true), await Direct.Queue.HeartbeatAsync(job, Ct));
    }

    [Fact]
    public async Task ContinuationOfACanceledRun_IsNotCreated()
    {
        var run = await TestRuns.CreateAsync();
        await Direct.EnqueueAsync(TestJobs.Request(TestHandlers.Record, tenantId: run.TenantId, runId: run.RunId));
        var job = Assert.Single(await Direct.Queue.ClaimAsync(JobResourceClass.Cpu, 1, "w", Ct));
        await CancelRunAsync(run);

        EnqueueResult? continuation = null;
        await Direct.Queue.CompleteAsync(job, async tx =>
            continuation = await tx.EnqueueAsync(TestJobs.Request(TestHandlers.Record, tenantId: run.TenantId, runId: run.RunId)), Ct);

        Assert.Equal(new EnqueueResult(0, false, JobState.Canceled), continuation);
        Assert.Equal(1L, await Db.ScalarAsync<long>("SELECT count(*) FROM ops.jobs"));
        Assert.Equal("succeeded", await Db.ScalarAsync<string>("SELECT state FROM ops.jobs WHERE id = $1", job.Id));
    }

    [Fact]
    public async Task ContinuationOfALiveRun_IsCreatedInTheCompletion()
    {
        var run = await TestRuns.CreateAsync();
        await Direct.EnqueueAsync(TestJobs.Request(TestHandlers.Record, tenantId: run.TenantId, runId: run.RunId));
        var job = Assert.Single(await Direct.Queue.ClaimAsync(JobResourceClass.Cpu, 1, "w", Ct));

        EnqueueResult? continuation = null;
        await Direct.Queue.CompleteAsync(job, async tx =>
            continuation = await tx.EnqueueAsync(TestJobs.Request(TestHandlers.Record, tenantId: run.TenantId, runId: run.RunId)), Ct);

        Assert.True(continuation!.Created);
        Assert.Equal("queued", await Db.ScalarAsync<string>("SELECT state FROM ops.jobs WHERE id = $1", continuation.JobId));
    }

    [Fact]
    public async Task CancelAsync_CancelsOnlyWaitingJobs()
    {
        var ids = await Direct.EnqueueManyAsync([TestJobs.Request(TestHandlers.Record), TestJobs.Request(TestHandlers.Record)]);
        var running = Assert.Single(await Direct.Queue.ClaimAsync(JobResourceClass.Cpu, 1, "w", Ct));

        Assert.False(await Direct.Queue.CancelAsync(running.Id, Ct));
        Assert.True(await Direct.Queue.CancelAsync(ids[1], Ct));

        Assert.Equal(["running", "canceled"], (await Db.RowsAsync("SELECT state FROM ops.jobs ORDER BY id")).Select(r => (string)r[0]!));
        await Direct.Queue.CompleteAsync(running, null, Ct);
        Assert.Equal("succeeded", await Db.ScalarAsync<string>("SELECT state FROM ops.jobs WHERE id = $1", running.Id));
    }

    /// <summary>As the API: in one transaction of the run's tenant set cancel_requested, then cancel the waiting jobs.</summary>
    private static async Task<int> CancelRunAsync(TestRun run)
    {
        await using var app = new DirectQueue(DatabaseRole.App);
        await using var context = run.AppDb();
        await using var transaction = await context.Database.BeginTransactionAsync(Ct);
        await context.Runs.Where(r => r.Id == run.RunId).ExecuteUpdateAsync(s => s.SetProperty(r => r.CancelRequested, true), Ct);
        var canceled = await context.CancelRunJobsAsync(app.Queue, run.RunId, Ct);
        await transaction.CommitAsync(Ct);
        return canceled;
    }
}
