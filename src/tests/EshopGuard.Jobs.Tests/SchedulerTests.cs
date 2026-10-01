using System.Collections.Concurrent;
using EshopGuard.Jobs.Handlers;
using EshopGuard.Jobs.Processing;
using EshopGuard.Jobs.Scheduling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EshopGuard.Jobs.Tests;

public sealed class SchedulerTests(JobsTestDatabase db) : JobsTestBase(db)
{
    private readonly ConcurrentQueue<Interval> _ticks = new();

    [Fact]
    public async Task TwoSchedulers_TickOneAtATime_EnqueueEachDailyJobOnce_AndTakeOver()
    {
        var settings = new Dictionary<string, string?> { ["Scheduler:Enabled"] = "true", ["Worker:Slots:System"] = "1" };
        var a = await Workers.StartAsync("a", settings, Recorder);
        var b = await Workers.StartAsync("b", settings, Recorder);

        await Task.Delay(TimeSpan.FromSeconds(10), Ct);

        var today = DateTimeOffset.UtcNow;
        foreach (var kind in new[] { SystemJobKinds.EnsurePartitions, SystemJobKinds.CleanupJobs })
        {
            Assert.Equal(1L, await Db.ScalarAsync<long>("SELECT count(*) FROM ops.jobs WHERE kind = $1", kind));
            Assert.Equal(DailySystemJobTask.DedupeKey(kind, today), await Db.ScalarAsync<string>("SELECT dedupe_key FROM ops.jobs WHERE kind = $1", kind));
        }

        await Workers.WaitUntilAsync(
            async () => await Db.ScalarAsync<long>("SELECT count(*) FROM ops.jobs WHERE kind LIKE 'system.%' AND state = 'succeeded'") == 2,
            TimeSpan.FromSeconds(10), "both daily jobs done");

        var ticks = _ticks.OrderBy(t => t.Start).ToList();
        Assert.True(ticks.Count >= 10, $"only {ticks.Count} ticks in 10 s");
        for (var i = 1; i < ticks.Count; i++)
        {
            Assert.True(ticks[i].Start >= ticks[i - 1].End, $"ticks of {ticks[i - 1].WorkerId} and {ticks[i].WorkerId} overlap");
        }

        await a.StopAsync();
        var before = _ticks.Count(t => t.WorkerId == b.Id);
        await Task.Delay(TimeSpan.FromSeconds(1.5), Ct);
        Assert.True(_ticks.Count(t => t.WorkerId == b.Id) >= before + 2, "worker B ticks after A stopped");
    }

    private void Recorder(IServiceCollection services) =>
        services.AddSingleton<IScheduledTask>(sp => new TickRecorder(_ticks, sp.GetRequiredService<IOptions<WorkerOptions>>().Value.EffectiveId));

    /// <summary>Records each tick with a short pause, so overlapping ticks would show.</summary>
    private sealed class TickRecorder(ConcurrentQueue<Interval> ticks, string workerId) : IScheduledTask
    {
        public string Name => "test.tick_recorder";

        public TimeSpan Interval => TimeSpan.Zero;

        public async Task RunAsync(ScheduledTaskContext context, CancellationToken ct)
        {
            var start = DateTimeOffset.UtcNow;
            await Task.Delay(50, ct);
            ticks.Enqueue(new Interval(null, 0, workerId, start, DateTimeOffset.UtcNow));
        }
    }
}

public sealed class SchedulerTaskFailureTests(JobsTestDatabase db) : JobsTestBase(db)
{
    [Fact]
    public async Task FailingTask_IsLogged_AndTheNextTaskStillRuns()
    {
        var worker = await Workers.StartAsync("a", new Dictionary<string, string?> { ["Scheduler:Enabled"] = "true" },
            services => services.AddSingleton<IScheduledTask, BrokenTask>());

        await Workers.WaitUntilAsync(
            async () => await Db.ScalarAsync<long>("SELECT count(*) FROM ops.jobs WHERE kind = $1", SystemJobKinds.CleanupJobs) == 1,
            TimeSpan.FromSeconds(5), "tasks after the broken one ran");
        await worker.Logs.WaitForAsync("scheduler.task_failed test.broken db.42P01", TimeSpan.FromSeconds(5));
    }

    /// <summary>Fails inside the tick transaction (unknown table); its savepoint is rolled back.</summary>
    private sealed class BrokenTask : IScheduledTask
    {
        public string Name => "test.broken";

        public TimeSpan Interval => TimeSpan.Zero;

        public async Task RunAsync(ScheduledTaskContext context, CancellationToken ct)
        {
            await using var command = new Npgsql.NpgsqlCommand("SELECT * FROM ops.no_such_table", context.Transaction.Connection, context.Transaction);
            await command.ExecuteNonQueryAsync(ct);
        }
    }
}
