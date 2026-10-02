using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Common;
using EshopGuard.Data.Tenancy;
using EshopGuard.Tests.Shared;
using Microsoft.EntityFrameworkCore;

namespace EshopGuard.Data.Tests.Catalog;

public sealed class ModelCatalogConsistencyTests
{
    private static readonly EshopGuardDb Db = TestDatabase.CreateDb("App", new TenantContext());

    private static string Name(Microsoft.EntityFrameworkCore.Metadata.IEntityType t) => $"{t.GetSchema()}.{t.GetTableName()}";

    [Fact]
    public void TenantEntityTypes_AreExactlyTheTenantTables()
    {
        var tenantTypes = Db.Model.GetEntityTypes().Where(t => typeof(ITenantOwned).IsAssignableFrom(t.ClrType)).Select(Name).Order(StringComparer.Ordinal);
        Assert.Equal(TableNames.TenantTables.Order(StringComparer.Ordinal), tenantTypes);
    }

    [Fact]
    public void AllEntityTypes_AreTenantOrGlobalTables()
    {
        Assert.Equal(TableNames.TenantTables.Concat(TableNames.GlobalTables).Order(StringComparer.Ordinal), Db.Model.GetEntityTypes().Select(Name).Order(StringComparer.Ordinal));
        Assert.Equal(64, Db.Model.GetEntityTypes().Count());
    }

    [Fact]
    public void GlobalTypesWithTenantColumn_AreNotTenantOwned()
    {
        var globalWithTenant = Db.Model.GetEntityTypes().Where(t => t.FindProperty("TenantId") is not null && TableNames.GlobalTables.Contains(Name(t))).ToList();
        Assert.NotEmpty(globalWithTenant);
        Assert.All(globalWithTenant, t => Assert.False(typeof(ITenantOwned).IsAssignableFrom(t.ClrType), t.ClrType.Name));
    }

    [Fact]
    public void TenantTypes_HaveTenantFilterAndSoftDeletableTypes_HaveSoftDeleteFilter()
    {
        foreach (var t in Db.Model.GetEntityTypes())
        {
            var keys = t.GetDeclaredQueryFilters().Select(f => f.Key).ToList();
            Assert.Equal(typeof(ITenantOwned).IsAssignableFrom(t.ClrType), keys.Contains(EshopGuardDb.TenantFilter));
            Assert.Equal(typeof(ISoftDeletable).IsAssignableFrom(t.ClrType), keys.Contains(EshopGuardDb.SoftDeleteFilter));
        }
    }
}
