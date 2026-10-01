using EshopGuard.Data.Tenancy;
using EshopGuard.Data.Tests.Isolation;
using EshopGuard.Tests.Shared;
using Microsoft.EntityFrameworkCore;

namespace EshopGuard.Data.Tests.Model;

[Collection(DbCollection.Name)]
[Trait("Category", "Db")]
public sealed class ConcurrencyTokenTests(TwoTenantsFixture tenants)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public Task FixProposal_SecondSaveFails() => AssertSecondSaveFailsAsync(db => db.FixProposals.SingleAsync(x => x.Id == tenants.A.FixProposalId, Ct), x => x.Reason = Guid.NewGuid().ToString());

    [Fact]
    public Task FixGroup_SecondSaveFails() => AssertSecondSaveFailsAsync(db => db.FixGroups.SingleAsync(x => x.Id == tenants.A.FixGroupId, Ct), x => x.PageCount++);

    [Fact]
    public Task EvidenceItem_SecondSaveFails() => AssertSecondSaveFailsAsync(db => db.EvidenceItems.SingleAsync(x => x.Id == tenants.A.EvidenceId, Ct), x => x.Title = Guid.NewGuid().ToString());

    [Fact]
    public Task Subscription_SecondSaveFails() => AssertSecondSaveFailsAsync(db => db.Subscriptions.SingleAsync(x => x.Id == tenants.A.SubscriptionId, Ct), x => x.CancelAtPeriodEnd = !x.CancelAtPeriodEnd);

    private async Task AssertSecondSaveFailsAsync<T>(Func<EshopGuardDb, Task<T>> load, Action<T> change)
        where T : class
    {
        await using var first = TestDatabase.CreateDb("App", tenants.A.Tenant.TenantId);
        await using var second = TestDatabase.CreateDb("App", tenants.A.Tenant.TenantId);
        var a = await first.ExecuteInTenantTransactionAsync(() => load(first), Ct);
        var b = await second.ExecuteInTenantTransactionAsync(() => load(second), Ct);

        change(a);
        await first.ExecuteInTenantTransactionAsync(() => first.SaveChangesAsync(Ct), Ct);
        change(b);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.ExecuteInTenantTransactionAsync(() => second.SaveChangesAsync(Ct), Ct));
    }
}
