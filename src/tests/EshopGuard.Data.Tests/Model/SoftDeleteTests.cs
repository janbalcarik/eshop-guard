using EshopGuard.Data.Entities.Shops;
using EshopGuard.Data.Tenancy;
using EshopGuard.Data.Tests.Isolation;
using EshopGuard.Tests.Shared;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EshopGuard.Data.Tests.Model;

[Collection(DbCollection.Name)]
[Trait("Category", "Db")]
public sealed class SoftDeleteTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Shop NewShop(string domain) => new() { Domain = domain, BaseUrl = $"https://{domain}/", BasePath = "/", HomeCountry = "sk", Platform = ShopPlatform.Other, SourceMode = ShopSourceMode.Web, Status = ShopStatus.Draft };

    [Fact]
    public async Task Remove_SetsDeletedAt_HidesTheRow_AndAllowsTheSameDomainAgain()
    {
        var tenant = await TestTenants.CreateAsync("Soft delete", Ct);
        await using var db = TestDatabase.CreateDb("App", tenant.TenantId);
        var shop = NewShop("vegis.sk");
        await db.ExecuteInTenantTransactionAsync(async () =>
        {
            db.Add(shop);
            await db.SaveChangesAsync(Ct);
            db.Remove(shop);
            await db.SaveChangesAsync(Ct);
        }, Ct);

        await using var read = TestDatabase.CreateDb("App", tenant.TenantId);
        var (visible, withDeleted) = await read.ExecuteInTenantTransactionAsync(async () => (
            await read.Shops.CountAsync(s => s.Id == shop.Id, Ct),
            await read.Shops.IgnoreQueryFilters([EshopGuardDb.SoftDeleteFilter]).SingleAsync(s => s.Id == shop.Id, Ct)), Ct);
        Assert.Equal(0, visible);
        Assert.NotNull(withDeleted.DeletedAt);

        await read.ExecuteInTenantTransactionAsync(async () =>
        {
            read.Add(NewShop("vegis.sk"));
            await read.SaveChangesAsync(Ct);
        }, Ct);
    }

    [Fact]
    public async Task TwoActiveShopsWithTheSameDomain_AreRefused()
    {
        var tenant = await TestTenants.CreateAsync("Unique shop", Ct);
        await using var db = TestDatabase.CreateDb("App", tenant.TenantId);
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.ExecuteInTenantTransactionAsync(async () =>
        {
            db.AddRange(NewShop("vegis.sk"), NewShop("vegis.sk"));
            await db.SaveChangesAsync(Ct);
        }, Ct));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(ex.InnerException).SqlState);
    }
}
