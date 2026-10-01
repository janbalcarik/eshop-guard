using EshopGuard.Data.Entities.Ops;
using EshopGuard.Jobs.Handlers;
using EshopGuard.Jobs.Processing;
using EshopGuard.Jobs.Queue;
using Microsoft.Extensions.DependencyInjection;

namespace EshopGuard.Jobs.Tests;

public sealed class UnknownKindTests(JobsTestDatabase db) : JobsTestBase(db)
{
    [Fact]
    public async Task UnknownKind_FailsAtOnce()
    {
        var id = (await Direct.EnqueueAsync(TestJobs.Request("run.unknown"))).JobId;
        await Workers.StartAsync("w", new Dictionary<string, string?> { ["Worker:Slots:Cpu"] = "1" });

        await Workers.WaitForEmptyQueueAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(["failed", 1, "job.unknown_kind: run.unknown"], Assert.Single(await Db.RowsAsync("SELECT state, attempts, last_error FROM ops.jobs WHERE id = $1", id)));
    }

    [Fact]
    public async Task TwoHandlersOfOneKind_FailTheStart()
    {
        var ex = await Assert.ThrowsAsync<JobQueueException>(() => Workers.StartAsync("w", null, services => services.AddScoped<IJobHandler, DuplicateRecordHandler>()));

        Assert.Equal(JobErrorCodes.DuplicateKind, ex.Code);
    }

    private sealed class DuplicateRecordHandler : IJobHandler
    {
        public string Kind => TestHandlers.Record;

        public JobResourceClass ResourceClass => JobResourceClass.Cpu;

        public Task<JobResult> ExecuteAsync(JobExecutionContext context, CancellationToken ct) => Task.FromResult(JobResult.Done);
    }
}

public sealed class WorkerRegistryTests(JobsTestDatabase db) : JobsTestBase(db)
{
    [Fact]
    public async Task Worker_RegistersWithSlots_SendsHeartbeats_DrainsAndUnregisters()
    {
        var worker = await Workers.StartAsync("w", new Dictionary<string, string?> { ["Worker:Slots:Cpu"] = "3", ["Worker:Slots:Fetch"] = "7", ["Worker:ShutdownSeconds"] = "1" });
        var row = Assert.Single(await Db.RowsAsync("SELECT slots ->> 'cpu', slots ->> 'fetch', draining, heartbeat_at FROM ops.workers WHERE id = $1", worker.Id));
        Assert.Equal(["3", "7", false], row[..3]);

        await Workers.WaitUntilAsync(
            async () => await Db.ScalarAsync<DateTime>("SELECT heartbeat_at FROM ops.workers WHERE id = $1", worker.Id) > (DateTime)row[3]!,
            TimeSpan.FromSeconds(5), "heartbeat");

        await Direct.EnqueueAsync(TestJobs.Request(TestHandlers.Block, payload: new { ignoreCancellation = 1 }));
        await Workers.WaitUntilAsync(() => Task.FromResult(!State.Blocked.IsEmpty), TimeSpan.FromSeconds(5), "a job runs");
        var stopping = worker.StopAsync();
        await Workers.WaitUntilAsync(
            async () => await Db.ScalarAsync<bool>("SELECT draining FROM ops.workers WHERE id = $1", worker.Id),
            TimeSpan.FromSeconds(5), "draining while the job runs");
        await stopping;

        Assert.Equal(0L, await Db.ScalarAsync<long>("SELECT count(*) FROM ops.workers"));
    }

    [Fact]
    public async Task StaleWorkers_AreDeleted_FreshOnesKept()
    {
        await Db.ExecuteAsync("Owner", """
            INSERT INTO ops.workers (id, started_at, heartbeat_at) VALUES
              ('stale', clock_timestamp() - interval '1 hour', clock_timestamp() - interval '11 minutes'),
              ('fresh', clock_timestamp() - interval '1 hour', clock_timestamp() - interval '1 minute')
            """);

        Assert.Equal(1, await Direct.Store.DeleteStaleWorkersAsync(TimeSpan.FromMinutes(10), Ct));

        Assert.Equal("fresh", await Db.ScalarAsync<string>("SELECT string_agg(id, ',') FROM ops.workers"));
    }
}

public sealed class MaintenanceHandlersTests(JobsTestDatabase db) : JobsTestBase(db)
{
    [Fact]
    public async Task EnsurePartitions_CreatesTheMissingMonthlyPartition()
    {
        var month = DateTime.UtcNow.AddMonths(EnsurePartitionsHandler.MonthsAhead);
        var partition = $"audit_log_y{month:yyyy}m{month:MM}";
        await Db.ExecuteAsync("Owner", $"DROP TABLE IF EXISTS ops.{partition}");
        var id = (await Direct.EnqueueAsync(new JobRequest(SystemJobKinds.EnsurePartitions, JobResourceClass.System, JobPriority.P4, JobRequest.EmptyPayload()))).JobId;
        var worker = await Workers.StartAsync("w", new Dictionary<string, string?> { ["Worker:Slots:System"] = "1" });

        await Workers.WaitForEmptyQueueAsync(TimeSpan.FromSeconds(10));

        Assert.Equal("succeeded", await Db.ScalarAsync<string>("SELECT state FROM ops.jobs WHERE id = $1", id));
        Assert.Equal(1L, await Db.ScalarAsync<long>("SELECT count(*) FROM pg_class WHERE relname = $1", partition));
        Assert.True(worker.Logs.Contains("partitions.ensured"));
    }

    [Fact]
    public async Task CleanupJobs_DeletesOldSucceededAndCanceled_KeepsFailedForThirtyDays()
    {
        await Db.ExecuteAsync("Owner", """
            INSERT INTO ops.jobs (kind, resource_class, priority, payload, state, attempts, max_attempts, not_before, finished_at, last_error) VALUES
              ('old.succeeded', 'cpu', 2, '{}', 'succeeded', 1, 5, now(), now() - interval '8 days', 'keep:no'),
              ('old.canceled',  'cpu', 2, '{}', 'canceled',  0, 5, now(), now() - interval '8 days', 'keep:no'),
              ('new.succeeded', 'cpu', 2, '{}', 'succeeded', 1, 5, now(), now() - interval '1 day',  'keep:yes'),
              ('failed.8d',     'cpu', 2, '{}', 'failed',    5, 5, now(), now() - interval '8 days', 'keep:yes'),
              ('failed.31d',    'cpu', 2, '{}', 'failed',    5, 5, now(), now() - interval '31 days','keep:no')
            """);
        var id = (await Direct.EnqueueAsync(new JobRequest(SystemJobKinds.CleanupJobs, JobResourceClass.System, JobPriority.P4, JobRequest.EmptyPayload()))).JobId;
        await Workers.StartAsync("w", new Dictionary<string, string?> { ["Worker:Slots:System"] = "1" });

        await Workers.WaitForEmptyQueueAsync(TimeSpan.FromSeconds(10));

        Assert.Equal("succeeded", await Db.ScalarAsync<string>("SELECT state FROM ops.jobs WHERE id = $1", id));
        Assert.Equal(["failed.8d", "new.succeeded"], (await Db.RowsAsync("SELECT kind FROM ops.jobs WHERE last_error LIKE 'keep:%' ORDER BY kind")).Select(r => (string)r[0]!));
    }
}
