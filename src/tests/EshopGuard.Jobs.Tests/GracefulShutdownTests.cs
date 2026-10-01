using EshopGuard.Jobs.Queue;

namespace EshopGuard.Jobs.Tests;

public sealed class GracefulShutdownTests(JobsTestDatabase db) : JobsTestBase(db)
{
    private static readonly Dictionary<string, string?> Settings = new()
    {
        ["Worker:Slots:Cpu"] = "1",
        ["Worker:LeaseSeconds"] = "10",
        ["Worker:HeartbeatSeconds"] = "1",
        ["Worker:ShutdownSeconds"] = "3",
    };

    [Fact]
    public async Task ShortJob_Finishes_AndNothingMoreIsTaken()
    {
        var ids = await Direct.EnqueueManyAsync(
        [
            TestJobs.Request(TestHandlers.Record, payload: new { sleepMs = 1000 }),
            .. Enumerable.Range(0, 3).Select(_ => TestJobs.Request(TestHandlers.Record)),
        ]);
        var worker = await Workers.StartAsync("w", Settings);
        await Workers.WaitUntilAsync(
            async () => await Db.ScalarAsync<string>("SELECT state FROM ops.jobs WHERE id = $1", ids[0]) == "running",
            TimeSpan.FromSeconds(5), "the first job runs");

        await worker.StopAsync();

        Assert.Equal(["succeeded", "queued", "queued", "queued"], (await Db.RowsAsync("SELECT state FROM ops.jobs ORDER BY id")).Select(r => (string)r[0]!));
        Assert.Equal(0L, await Db.ScalarAsync<long>("SELECT count(*) FROM ops.workers"));
        Assert.True(worker.Logs.Contains("worker.stopped " + worker.Id));
    }

    [Fact]
    public async Task LongJob_GoesBackWithoutCountingTheAttempt_AndAnotherWorkerFinishesIt()
    {
        var id = (await Direct.EnqueueAsync(TestJobs.Request(TestHandlers.Block))).JobId;
        var a = await Workers.StartAsync("a", Settings);
        await Workers.WaitUntilAsync(() => Task.FromResult(!State.Blocked.IsEmpty), TimeSpan.FromSeconds(5), "the job runs");

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await a.StopAsync();

        Assert.InRange(stopwatch.Elapsed, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3));
        Assert.Equal(["queued", 0, JobErrorCodes.WorkerShutdown, null], Assert.Single(await Db.RowsAsync("SELECT state, attempts, last_error, lease_owner FROM ops.jobs WHERE id = $1", id)));
        Assert.Equal(0L, await Db.ScalarAsync<long>("SELECT count(*) FROM ops.workers"));

        State.Gate.TrySetResult();
        var b = await Workers.StartAsync("b", Settings);
        await Workers.WaitForEmptyQueueAsync(TimeSpan.FromSeconds(10));
        Assert.Equal([b.Id, 1], Assert.Single(await Db.RowsAsync("SELECT worker_id, attempt FROM jobs_test.effects WHERE job_id = $1", id)));
    }

    [Fact]
    public async Task HandlerIgnoringCancellation_IsReturnedAnyway_AndCannotWriteLater()
    {
        var id = (await Direct.EnqueueAsync(TestJobs.Request(TestHandlers.Block, payload: new { ignoreCancellation = 1 }))).JobId;
        var a = await Workers.StartAsync("a", Settings);
        await Workers.WaitUntilAsync(() => Task.FromResult(!State.Blocked.IsEmpty), TimeSpan.FromSeconds(5), "the job runs");

        await a.StopAsync();
        Assert.Equal(["queued", 0, JobErrorCodes.WorkerShutdown], Assert.Single(await Db.RowsAsync("SELECT state, attempts, last_error FROM ops.jobs WHERE id = $1", id)));

        State.Gate.TrySetResult();
        await Workers.WaitUntilAsync(() => Task.FromResult(!State.Errors.IsEmpty), TimeSpan.FromSeconds(5), "the late handler tried to complete");
        Assert.IsType<LeaseLostException>(Assert.Single(State.Errors));
        Assert.Equal(0L, await Db.ScalarAsync<long>("SELECT count(*) FROM jobs_test.effects"));
    }
}
