using EshopGuard.Data.Entities.Ops;
using EshopGuard.Jobs.Workers;

namespace EshopGuard.Jobs.Tests;

public sealed class DomainLeaseTests(JobsTestDatabase db) : JobsTestBase(db)
{
    private static readonly TimeSpan Long = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task Holder_KeepsTheDomain_OthersGetNull()
    {
        Assert.NotNull(await Direct.Store.TryAcquireDomainAsync("shop.test", 1, Long, Ct));

        Assert.Null(await Direct.Store.TryAcquireDomainAsync("shop.test", 2, Long, Ct));
        Assert.NotNull(await Direct.Store.TryAcquireDomainAsync("shop.test", 1, Long, Ct));
        Assert.Equal(1L, await Db.ScalarAsync<long>("SELECT lease_job_id FROM ops.domains WHERE domain = 'shop.test'"));
    }

    [Fact]
    public async Task ExpiredLease_PassesToTheNextJob_WithThePolitenessState()
    {
        var first = await Direct.Store.TryAcquireDomainAsync("shop.test", 1, Long, Ct);
        await Direct.Store.ReleaseDomainAsync("shop.test", 1, first!.State with { Rate = 3, CrawlDelayMs = 500, RobotsTxt = "User-agent: *", Sitemaps = ["https://shop.test/sitemap.xml"] }, Ct);
        Assert.NotNull(await Direct.Store.TryAcquireDomainAsync("shop.test", 2, TimeSpan.FromMilliseconds(300), Ct));

        await Task.Delay(500, Ct);
        var taken = await Direct.Store.TryAcquireDomainAsync("shop.test", 3, Long, Ct);

        Assert.NotNull(taken);
        Assert.Equal(3, taken.State.Rate);
        Assert.Equal(500, taken.State.CrawlDelayMs);
        Assert.Equal("User-agent: *", taken.State.RobotsTxt);
        Assert.Equal(["https://shop.test/sitemap.xml"], taken.State.Sitemaps);
    }

    [Fact]
    public async Task ReleaseByAnotherJob_ChangesNothing()
    {
        await Direct.Store.TryAcquireDomainAsync("shop.test", 2, Long, Ct);

        await Direct.Store.ReleaseDomainAsync("shop.test", 1, new DomainPolitenessState(Rate: 99, ConsecutiveErrors: 7), Ct);

        Assert.Equal([2L, null, 0], Assert.Single(await Db.RowsAsync("SELECT lease_job_id, rate, consecutive_errors FROM ops.domains WHERE domain = 'shop.test'")));
        Assert.False(await Direct.Store.RenewDomainAsync("shop.test", 1, Long, Ct));
        Assert.True(await Direct.Store.RenewDomainAsync("shop.test", 2, Long, Ct));
    }

    [Fact]
    public async Task BlockedDomain_IsNotGiven()
    {
        await Db.ExecuteAsync("Owner", "INSERT INTO ops.domains (domain, consecutive_errors, blocked_until) VALUES ('blocked.test', 5, clock_timestamp() + interval '1 hour')");

        Assert.Null(await Direct.Store.TryAcquireDomainAsync("blocked.test", 1, Long, Ct));
    }

    [Fact]
    public async Task JobHeartbeat_ExtendsTheDomainLeaseOfTheJob()
    {
        await Direct.EnqueueAsync(TestJobs.Request(TestHandlers.FetchDomain, JobResourceClass.Fetch));
        var job = Assert.Single(await Direct.Queue.ClaimAsync(JobResourceClass.Fetch, 1, "w", Ct));
        await Direct.Store.TryAcquireDomainAsync("shop.test", job.Id, TimeSpan.FromMilliseconds(200), Ct);

        await Direct.Queue.HeartbeatAsync(job, Ct);

        // Direct: Worker:LeaseSeconds = 2.
        Assert.InRange(await Db.ScalarAsync<double>("SELECT extract(epoch FROM lease_until - clock_timestamp())::double precision FROM ops.domains WHERE domain = 'shop.test'"), 1.5, 2.1);
    }
}
