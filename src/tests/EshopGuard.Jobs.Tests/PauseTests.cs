using EshopGuard.Data.Entities.Ops;
using Microsoft.Extensions.Logging;

namespace EshopGuard.Jobs.Tests;

public sealed class PauseTests(JobsTestDatabase db) : JobsTestBase(db)
{
    [Fact]
    public async Task CreditExhausted_PausesTheClass_UntilResumed()
    {
        var pausing = (await Direct.EnqueueAsync(TestJobs.Request(TestHandlers.Pause, JobResourceClass.Jev, tenantId: Guid.NewGuid()))).JobId;
        var worker = await Workers.StartAsync("w", new Dictionary<string, string?> { ["Worker:Slots:Jev"] = "2", ["Worker:Slots:Cpu"] = "2" });

        await Workers.WaitUntilAsync(
            async () => await Db.ScalarAsync<bool>("SELECT value ? 'jev' FROM ops.system_settings WHERE key = 'jobs.paused_classes'"),
            TimeSpan.FromSeconds(10), "jev paused");
        Assert.Equal("jev.credit_exhausted", await Db.ScalarAsync<string>("SELECT value -> 'jev' ->> 'reason' FROM ops.system_settings WHERE key = 'jobs.paused_classes'"));
        await Workers.WaitUntilAsync(
            async () => await Db.ScalarAsync<string>("SELECT state FROM ops.jobs WHERE id = $1", pausing) == "queued",
            TimeSpan.FromSeconds(5), "the job went back");
        Assert.Equal([0, "jev.credit_exhausted"], Assert.Single(await Db.RowsAsync("SELECT attempts, last_error FROM ops.jobs WHERE id = $1", pausing)));
        Assert.Contains(worker.Logs.Logs, l => l.Level == LogLevel.Error && l.Message.Contains("jobs.class_paused jev jev.credit_exhausted", StringComparison.Ordinal));

        var jev = (await Direct.EnqueueAsync(TestJobs.Request(TestHandlers.Record, JobResourceClass.Jev))).JobId;
        var cpu = (await Direct.EnqueueAsync(TestJobs.Request(TestHandlers.Record, JobResourceClass.Cpu))).JobId;
        await Task.Delay(3000, Ct);
        Assert.Equal("succeeded", await Db.ScalarAsync<string>("SELECT state FROM ops.jobs WHERE id = $1", cpu));
        Assert.Equal(0L, await Db.ScalarAsync<long>("SELECT count(*) FROM ops.jobs WHERE resource_class = 'jev' AND (state <> 'queued' OR attempts > 0)"));

        await Direct.Queue.ResumeResourceClassAsync(JobResourceClass.Jev, Ct);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await Workers.WaitUntilAsync(
            async () => await Db.ScalarAsync<long>("SELECT count(*) FROM ops.jobs WHERE id IN ($1, $2) AND state = 'succeeded'", pausing, jev) == 2,
            TimeSpan.FromSeconds(10), "jev jobs taken after resume");
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"resume took {stopwatch.Elapsed}");
        Assert.Equal(1, await Db.ScalarAsync<int>("SELECT attempts FROM ops.jobs WHERE id = $1", pausing));
    }
}
