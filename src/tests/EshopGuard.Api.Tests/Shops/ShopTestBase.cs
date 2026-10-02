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

    /// <summary>
    /// The state the sample of change 8 leaves for <c>bylinkovo.sk</c>: a finished run with its basis, SK (seat, .sk, €) and
    /// CZ (raised by the Czech version, a verified quote) suggested with strong evidence, PL unsupported, versions sk (main)
    /// and cs (96 % of the descriptions in Czech) with 5 834 products each, pl unsupported; the e-shop in <c>sample</c>.
    /// </summary>
    internal static async Task<Guid> SeedBylinkovoAsync(Guid tenantId, Guid shopId, string domain, string runStatus = "finished", int? csProducts = 5834)
    {
        var runId = await SeedRunAsync(tenantId, shopId, "free_sample", runStatus,
            stats: """{"pages_checked":100,"unchecked":{"robots_blocked":7,"not_loaded":3,"over_limit":40},"sample":{"findings_by_severity":{"high":3,"medium":5,"low":3},"findings_by_checkability":{"text":4,"assess":5,"verify":2},"top_finding_ids":[],"pages_checked_by_language":{"sk":60,"cs":40},"jurisdictions":["sk","cz"]}}""",
            estimate: """{"basis":{"versions":[{"language":"sk","other_pages":38},{"language":"cs","other_pages":38}]}}""",
            progress: """{"pages_planned":100,"pages_fetched":100,"pages_processed":100}""");
        await AdminAsync("UPDATE shop.shops SET status = 'sample' WHERE id = $1", shopId);
        await SeedMarketAsync(tenantId, shopId, "SK", "suggested", "strong", true, runId,
            """{"quotes":[{"quote":"Bylinkovo s.r.o., Hlavná 1, Trenčín","source":"https://BASE/kontakt"},{"quote":"tld=sk","source":"signal"},{"quote":"currency=EUR","source":"signal"}],"home_basis":"quote","raised_by":null,"internal":{"reason":"interní","uncertain":""}}""".Replace("BASE", domain, StringComparison.Ordinal));
        await SeedMarketAsync(tenantId, shopId, "CZ", "suggested", "strong", false, runId,
            """{"quotes":[{"quote":"Doprava do Českej republiky 3,90 €","source":"https://BASE/doprava"}],"home_basis":null,"raised_by":"version=cs","internal":{"reason":"interní","uncertain":""}}""".Replace("BASE", domain, StringComparison.Ordinal));
        await SeedMarketAsync(tenantId, shopId, "PL", "unsupported", "delivery", false, runId, """{"quotes":[],"home_basis":null,"raised_by":null,"internal":{"reason":"","uncertain":""}}""");
        await SeedLanguageAsync(tenantId, shopId, "sk", $"https://{domain}/", "main", "active", 5834, 1.0f, runId);
        await SeedLanguageAsync(tenantId, shopId, "cs", $"https://{domain}/cz/", "hreflang", "active", csProducts, 0.96f, runId,
            """{"basis":"main_text","description_pages":20,"labeled_products":20,"translated_products":19,"foreign_text_products":1,"foreign_text_language":"sk"}""");
        await SeedLanguageAsync(tenantId, shopId, "pl", $"https://{domain}/pl/", "hreflang", "unsupported", 5834, null, runId);
        return runId;
    }

    protected static Task SeedMarketAsync(Guid tenantId, Guid shopId, string country, string status, string? level, bool isHome, Guid? runId, string? evidence, string source = "detected") =>
        AdminAsync(
            """
            INSERT INTO shop.shop_markets (tenant_id, shop_id, country_code, is_home, status, evidence_level, source, evidence, detection_run_id, created_at, updated_at)
            VALUES ($1, $2, $3, $4, $5, $6, $7, $8::jsonb, $9, now(), now())
            """, tenantId, shopId, country, isHome, status, level, source, evidence, runId);

    protected static Task SeedLanguageAsync(
        Guid tenantId, Guid shopId, string language, string baseUrl, string source, string status, int? products, float? translated, Guid? runId, string? description = null) =>
        AdminAsync(
            """
            INSERT INTO shop.shop_languages (tenant_id, shop_id, language, base_url, switch_method, source, status, translated_share, language_share,
                description_languages, sample_run_id, product_count, created_at, updated_at)
            VALUES ($1, $2, $3, $4, 'path', $5, $6, $7, '{}'::jsonb, $8::jsonb, $9, $10, now(), now())
            """, tenantId, shopId, language, baseUrl, source, status, translated, description, runId, products);
}
