using System.Net;
using System.Text.Json;

namespace EshopGuard.Api.Tests.Shops;

/// <summary>Helpers of the tests of the e-shops (change 10): an e-shop through the API, rows that only the worker writes.</summary>
public abstract class ShopTestBase : ApiTestBase
{
    /// <summary>A domain of its own (a free sample is once per domain across all tests).</summary>
    protected static string NewDomain(string prefix = "eshop") => $"{prefix}-{Guid.NewGuid():N}"[..(prefix.Length + 11)] + ".sk";

    internal static async Task<JsonElement> CreateShopAsync(Person person, string? url = null)
    {
        using var response = await person.Browser.PostAsync($"/api/t/{person.TenantId}/shops", new { url = url ?? "https://www." + NewDomain() + "/" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ApiClient.JsonAsync(response);
    }

    internal static async Task<JsonElement> GetJsonAsync(Person person, string path)
    {
        using var response = await person.Browser.GetAsync(path);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{path}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return await ApiClient.JsonAsync(response);
    }

    /// <summary>A run of the e-shop as the worker would write it (admin, RLS bypassed).</summary>
    protected static async Task<Guid> SeedRunAsync(Guid tenantId, Guid shopId, string kind, string status, string? stats = null, string? estimate = null, string? progress = null)
    {
        var runId = Guid.CreateVersion7();
        await AdminAsync(
            """
            INSERT INTO checks.runs (id, tenant_id, shop_id, kind, trigger, status, priority, jurisdictions, modules, stats, estimate, progress,
                started_at, finished_at, created_at, updated_at)
            VALUES ($1, $2, $3, $4, 'user', $5, 0, '{sk}', '{}', $6::jsonb, $7::jsonb, $8::jsonb, now(),
                CASE WHEN $5 IN ('finished', 'partial', 'failed', 'canceled') THEN now() END, now(), now())
            """,
            runId, tenantId, shopId, kind, status, stats, estimate, progress);
        return runId;
    }

    /// <summary>A subscription of the e-shop with a price list of its own.</summary>
    protected static async Task SeedSubscriptionAsync(Guid tenantId, Guid shopId, string status)
    {
        var priceList = Guid.CreateVersion7();
        await AdminAsync(
            "INSERT INTO billing.price_lists (id, name, market_code, currency, valid_from, status, notice_days, created_at, updated_at) " +
            "VALUES ($1, 'test', 'sk', 'EUR', now(), 'draft', 30, now(), now())", priceList);
        await AdminAsync(
            "INSERT INTO billing.subscriptions (tenant_id, shop_id, status, interval, price_list_id, tier_code, unit_price, created_at, updated_at) " +
            "VALUES ($1, $2, $3, 'month', $4, 't1', 10, now(), now())", tenantId, shopId, status, priceList);
    }
}
