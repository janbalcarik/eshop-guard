using EshopGuard.Data.Entities.Ops;
using EshopGuard.Jobs.Queue;

namespace EshopGuard.Jobs.Tests;

public sealed class PriorityTests(JobsTestDatabase db) : JobsTestBase(db)
{
    [Fact]
    public async Task NewerP0_GoesBeforeOlderP2()
    {
        var ids = await Direct.EnqueueManyAsync([TestJobs.Request(TestHandlers.Record, priority: JobPriority.P2), TestJobs.Request(TestHandlers.Record, priority: JobPriority.P0)]);

        var claimed = Assert.Single(await Direct.Queue.ClaimAsync(JobResourceClass.Cpu, 1, "w", Ct));

        Assert.Equal(ids[1], claimed.Id);
        Assert.Equal(JobPriority.P0, claimed.Priority);
        Assert.Equal(1, claimed.Attempt);
    }

    [Fact]
    public async Task SamePriority_IsFirstInFirstOut()
    {
        var ids = await Direct.EnqueueManyAsync(Enumerable.Range(0, 3).Select(_ => TestJobs.Request(TestHandlers.Record)));

        Assert.Equal(ids[0], Assert.Single(await Direct.Queue.ClaimAsync(JobResourceClass.Cpu, 1, "w", Ct)).Id);
        Assert.Equal(ids[1..], (await Direct.Queue.ClaimAsync(JobResourceClass.Cpu, 5, "w", Ct)).Select(j => j.Id));
    }

    [Fact]
    public async Task JobScheduledForLater_IsNotTaken()
    {
        await Direct.EnqueueAsync(TestJobs.Request(TestHandlers.Record, notBefore: DateTimeOffset.UtcNow.AddMinutes(10)));

        Assert.Empty(await Direct.Queue.ClaimAsync(JobResourceClass.Cpu, 5, "w", Ct));
    }

    [Fact]
    public async Task OtherResourceClass_IsNotTaken()
    {
        await Direct.EnqueueAsync(TestJobs.Request(TestHandlers.Record, JobResourceClass.Jev));

        Assert.Empty(await Direct.Queue.ClaimAsync(JobResourceClass.Cpu, 5, "w", Ct));
        Assert.Single(await Direct.Queue.ClaimAsync(JobResourceClass.Jev, 5, "w", Ct));
    }

    [Fact]
    public async Task OrderHolds_AcrossJobsWithAndWithoutConcurrencyKey()
    {
        var ids = await Direct.EnqueueManyAsync(
        [
            TestJobs.Request(TestHandlers.Record, priority: JobPriority.P2),
            TestJobs.Request(TestHandlers.Record, priority: JobPriority.P0, concurrencyKey: "domain:a.test"),
            TestJobs.Request(TestHandlers.Record, priority: JobPriority.P1),
            TestJobs.Request(TestHandlers.Record, priority: JobPriority.P1, concurrencyKey: "domain:b.test"),
        ]);

        // P0 with a key, then the older P1 without a key, then the newer P1 with a key, then P2.
        Assert.Equal(ids[1], Assert.Single(await Direct.Queue.ClaimAsync(JobResourceClass.Cpu, 1, "w", Ct)).Id);
        Assert.Equal(ids[2], Assert.Single(await Direct.Queue.ClaimAsync(JobResourceClass.Cpu, 1, "w", Ct)).Id);
        Assert.Equal(ids[3], Assert.Single(await Direct.Queue.ClaimAsync(JobResourceClass.Cpu, 1, "w", Ct)).Id);
        Assert.Equal(ids[0], Assert.Single(await Direct.Queue.ClaimAsync(JobResourceClass.Cpu, 1, "w", Ct)).Id);
    }

    [Fact]
    public async Task SlotsPerClass_LimitRunningJobs()
    {
        await Direct.EnqueueManyAsync(Enumerable.Range(0, 20).SelectMany(_ => new[]
        {
            TestJobs.Request(TestHandlers.Record, JobResourceClass.Cpu, payload: new { sleepMs = 2000 }),
            TestJobs.Request(TestHandlers.Record, JobResourceClass.Fetch, payload: new { sleepMs = 2000 }),
        }));
        await Workers.StartAsync("w", new Dictionary<string, string?> { ["Worker:Slots:Cpu"] = "2", ["Worker:Slots:Fetch"] = "10", ["Worker:LeaseSeconds"] = "10", ["Worker:HeartbeatSeconds"] = "1" });

        await Workers.WaitUntilAsync(
            async () => await Db.ScalarAsync<long>("SELECT count(*) FROM ops.jobs WHERE state = 'running' AND resource_class = 'fetch'") == 10,
            TimeSpan.FromSeconds(10), "ten fetch jobs running");
        for (var i = 0; i < 10; i++)
        {
            Assert.Equal(2L, await Db.ScalarAsync<long>("SELECT count(*) FROM ops.jobs WHERE state = 'running' AND resource_class = 'cpu'"));
            Assert.True(await Db.ScalarAsync<long>("SELECT count(*) FROM ops.jobs WHERE state = 'running' AND resource_class = 'fetch'") <= 10);
            await Task.Delay(100, Ct);
        }
    }
}
