using EshopGuard.Data.Entities.Ops;

namespace EshopGuard.Jobs.Tests;

public sealed class TenantCapTests(JobsTestDatabase db) : JobsTestBase(db)
{
    [Fact]
    public async Task TenantCap_LimitsRunningJobsOfOneTenant_AndLetsAnotherTenantIn()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await Direct.EnqueueManyAsync(Enumerable.Range(0, 50).Select(_ => TestJobs.Request(TestHandlers.Record, JobResourceClass.Jev, payload: new { sleepMs = 150, tag = "A" }, tenantId: tenantA)));
        await Direct.EnqueueManyAsync(Enumerable.Range(0, 5).Select(_ => TestJobs.Request(TestHandlers.Record, JobResourceClass.Jev, payload: new { sleepMs = 150, tag = "B" }, tenantId: tenantB)));
        await Workers.StartManyAsync(2, new Dictionary<string, string?> { ["Worker:Slots:Jev"] = "8", ["Worker:TenantCaps:Jev"] = "2" });

        var maxRunningA = 0L;
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (await Db.ScalarAsync<long>("SELECT count(*) FROM ops.jobs WHERE state IN ('queued', 'running')") > 0)
        {
            Assert.True(DateTime.UtcNow < deadline, "the queue did not finish in 60 s");
            maxRunningA = Math.Max(maxRunningA, await Db.ScalarAsync<long>("SELECT count(*) FROM ops.jobs WHERE state = 'running' AND tenant_id = $1", tenantA));
            await Task.Delay(100, Ct);
        }

        Assert.InRange(maxRunningA, 1, 2 + 2);
        var firstB = State.Intervals.Where(i => i.Tag == "B").Min(i => i.Start);
        var lastA = State.Intervals.Where(i => i.Tag == "A").Max(i => i.End);
        Assert.True(firstB < lastA, "tenant B started before tenant A finished");
        Assert.Equal(55L, await Db.ScalarAsync<long>("SELECT count(*) FROM jobs_test.effects"));
    }
}
