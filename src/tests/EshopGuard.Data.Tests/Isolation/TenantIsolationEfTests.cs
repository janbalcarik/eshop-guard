using System.Reflection;
using EshopGuard.Data.Entities.Common;
using EshopGuard.Data.Tenancy;
using EshopGuard.Tests.Shared;
using Microsoft.EntityFrameworkCore;

namespace EshopGuard.Data.Tests.Isolation;

[Collection(DbCollection.Name)]
[Trait("Category", "Db")]
public sealed class TenantIsolationEfTests(TwoTenantsFixture tenants)
{
    public static TheoryData<string> TenantEntityTypes()
    {
        using var db = TestDatabase.CreateDb("App", new TenantContext());
        return [.. db.Model.GetEntityTypes().Where(t => typeof(ITenantOwned).IsAssignableFrom(t.ClrType)).Select(t => t.ClrType.FullName!).Order()];
    }

    [Theory]
    [MemberData(nameof(TenantEntityTypes))]
    public async Task EntityType_ReturnsOnlyRowsOfTheContextTenant_WithAndWithoutTheFilter(string typeName)
    {
        var type = typeof(EshopGuardDb).Assembly.GetType(typeName)!;
        var method = typeof(TenantIsolationEfTests).GetMethod(nameof(TenantIdsAsync), BindingFlags.NonPublic | BindingFlags.Static)!.MakeGenericMethod(type);
        await using var db = TestDatabase.CreateDb("App", tenants.A.Tenant.TenantId);

        foreach (var ignoreFilter in new[] { false, true })
        {
            var ids = await db.ExecuteInTenantTransactionAsync(() => (Task<List<Guid?>>)method.Invoke(null, [db, ignoreFilter])!, TestContext.Current.CancellationToken);
            Assert.NotEmpty(ids);
            Assert.All(ids, id => Assert.Equal(tenants.A.Tenant.TenantId, id));
        }
    }

    private static Task<List<Guid?>> TenantIdsAsync<T>(EshopGuardDb db, bool ignoreTenantFilter)
        where T : class
    {
        var query = db.Set<T>().AsNoTracking();
        if (ignoreTenantFilter)
        {
            // RLS in the database still stops rows of other tenants.
            query = query.IgnoreQueryFilters([EshopGuardDb.TenantFilter]);
        }

        return query.Select(e => EF.Property<Guid?>(e, "TenantId")).ToListAsync(TestContext.Current.CancellationToken);
    }
}
