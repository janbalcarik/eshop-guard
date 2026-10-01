using EshopGuard.Data.Entities.Billing;
using EshopGuard.Data.Entities.Checks;
using EshopGuard.Data.Entities.Iam;
using EshopGuard.Data.Tenancy;
using EshopGuard.Tests.Shared;

namespace EshopGuard.Data.Tests.Isolation;

/// <summary>Global rows a tenant needs: the tenant itself and a user (written as <c>eshopguard_app</c>).</summary>
internal sealed record TestTenant(Guid TenantId, Guid UserId);

/// <summary>Creates fresh tenants and global catalogue rows; every run uses new ids, nothing is ever deleted.</summary>
internal static class TestTenants
{
    public static async Task<TestTenant> CreateAsync(string name, CancellationToken ct)
    {
        await using var db = TestDatabase.CreateDb("App", new TenantContext());
        var tenant = new Tenant { Name = name, CountryCode = "SK", Locale = "sk", MarketCode = "sk", Status = TenantStatus.Active };
        var user = new User { Email = $"test-{Guid.NewGuid():N}@example.invalid", Locale = "sk" };
        db.AddRange(tenant, user);
        await db.SaveChangesAsync(ct);
        return new TestTenant(tenant.Id, user.Id);
    }

    /// <summary>A rule set and a price list (global catalogues; the worker may write them, the API only reads).</summary>
    public static async Task<(Guid RuleSetId, Guid PriceListId)> CreateCataloguesAsync(CancellationToken ct)
    {
        await using var db = TestDatabase.CreateDb("Worker", new TenantContext());
        var ruleSet = new RuleSet
        {
            Module = "test-" + Guid.NewGuid().ToString("N")[..8], Version = "v1", QuestionLanguage = "en",
            QuestionSetHash = new byte[32], Definition = Json("{}"), Texts = Json("{}"), SourceHash = new byte[32],
        };
        var priceList = new PriceList { Name = "Test", MarketCode = "sk", Currency = "EUR", ValidFrom = DateTimeOffset.UtcNow, Status = PriceListStatus.Draft };
        db.AddRange(ruleSet, priceList);
        await db.SaveChangesAsync(ct);
        return (ruleSet.Id, priceList.Id);
    }

    public static System.Text.Json.JsonDocument Json(string json) => System.Text.Json.JsonDocument.Parse(json);
}
