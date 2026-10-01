using EshopGuard.Data;
using EshopGuard.Data.Entities.Checks;
using EshopGuard.Data.Entities.Iam;
using EshopGuard.Data.Entities.Shops;
using EshopGuard.Data.Tenancy;

namespace EshopGuard.Jobs.Tests;

/// <summary>A tenant with a shop and a run (fresh ids on every call; nothing is deleted).</summary>
internal sealed record TestRun(Guid TenantId, Guid ShopId, Guid RunId)
{
    /// <summary>Context of the run's tenant as <c>eshopguard_app</c> (as the API would write).</summary>
    public EshopGuardDb AppDb()
    {
        var tenant = new TenantContext();
        tenant.Set(TenantId);
        return JobsTestDatabase.CreateDb("App", tenant);
    }
}

internal static class TestRuns
{
    public static async Task<TestRun> CreateAsync(string domain = "shop.test")
    {
        Guid tenantId;
        await using (var global = JobsTestDatabase.CreateDb("App", new TenantContext()))
        {
            var tenant = new Tenant { Name = "Jobs " + domain, CountryCode = "SK", Locale = "sk", MarketCode = "sk", Status = TenantStatus.Active };
            global.Add(tenant);
            await global.SaveChangesAsync();
            tenantId = tenant.Id;
        }

        var shop = new Shop
        {
            Domain = domain, BaseUrl = $"https://{domain}/", BasePath = "/", HomeCountry = "sk",
            Platform = ShopPlatform.Shoptet, SourceMode = ShopSourceMode.Web, Status = ShopStatus.Active, Modules = ["eco"],
        };
        var context = new TenantContext();
        context.Set(tenantId);
        await using var db = JobsTestDatabase.CreateDb("App", context);
        db.Add(shop);
        await db.ExecuteInTenantTransactionAsync(() => db.SaveChangesAsync());
        var run = new Run { ShopId = shop.Id, Kind = RunKind.FullAnalysis, Trigger = RunTrigger.User, Status = RunStatus.Queued, Priority = 2, Jurisdictions = ["sk"], Modules = ["eco"] };
        db.Add(run);
        await db.ExecuteInTenantTransactionAsync(() => db.SaveChangesAsync());
        return new TestRun(tenantId, shop.Id, run.Id);
    }
}
