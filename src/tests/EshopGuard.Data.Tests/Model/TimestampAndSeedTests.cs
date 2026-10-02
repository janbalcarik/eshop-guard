using EshopGuard.Data.Entities.Ref;
using EshopGuard.Data.Entities.Shops;
using EshopGuard.Data.Tenancy;
using EshopGuard.Data.Tests.Isolation;
using EshopGuard.Tests.Shared;
using Microsoft.EntityFrameworkCore;

namespace EshopGuard.Data.Tests.Model;

[Collection(DbCollection.Name)]
[Trait("Category", "Db")]
public sealed class TimestampAndSeedTests(TwoTenantsFixture tenants)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Update_MovesUpdatedAtAfterCreatedAt_InUtc()
    {
        await using var db = TestDatabase.CreateDb("App", tenants.A.Tenant.TenantId);
        var shop = new Shop { Domain = $"ts{Guid.NewGuid():N}.sk", BaseUrl = "https://ts.sk/", BasePath = "/", HomeCountry = "sk", Platform = ShopPlatform.Other, SourceMode = ShopSourceMode.Web, Status = ShopStatus.Draft };
        await db.ExecuteInTenantTransactionAsync(async () =>
        {
            db.Add(shop);
            await db.SaveChangesAsync(Ct);
        }, Ct);
        Assert.Equal(shop.CreatedAt, shop.UpdatedAt);
        Assert.Equal(TimeSpan.Zero, shop.CreatedAt.Offset);

        await Task.Delay(5, Ct);
        await db.ExecuteInTenantTransactionAsync(async () =>
        {
            shop.Name = "Changed";
            await db.SaveChangesAsync(Ct);
        }, Ct);

        await using var read = TestDatabase.CreateDb("App", tenants.A.Tenant.TenantId);
        var stored = await read.ExecuteInTenantTransactionAsync(() => read.Shops.AsNoTracking().SingleAsync(s => s.Id == shop.Id, Ct), Ct);
        Assert.True(stored.UpdatedAt > stored.CreatedAt);
        Assert.Equal(TimeSpan.Zero, stored.UpdatedAt.Offset);
    }

    [Fact]
    public async Task ReferenceData_HasLocalesAndMarkets()
    {
        await using var db = TestDatabase.CreateDb("App", new TenantContext());
        var locales = await db.Locales.AsNoTracking().OrderBy(l => l.Code).ToListAsync(Ct);
        // Tests of billing (change 12) add hidden markets of their own (code x…) to the shared test database.
        var markets = await db.Markets.AsNoTracking().Where(m => !m.Code.StartsWith("x")).OrderBy(m => m.Code).ToListAsync(Ct);

        Assert.Equal(["cs", "sk"], locales.Select(l => l.Code));
        Assert.All(locales, l => Assert.False(l.Enabled));
        Assert.Equal([("cz", "CZK", MarketChecksStatus.Limited), ("sk", "EUR", MarketChecksStatus.Full)], markets.Select(m => (m.Code, m.Currency, m.ChecksStatus)));
    }
}
