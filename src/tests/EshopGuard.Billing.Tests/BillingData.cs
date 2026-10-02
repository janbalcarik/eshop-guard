using static EshopGuard.Billing.Tests.BillingTestHost;

namespace EshopGuard.Billing.Tests;

/// <summary>
/// Rows of the tests of billing (change 12) written as <c>eshopguard_admin</c>: a market of its own per test (price lists are
/// global), price lists with the tiers of the design, tenants, e-shops and subscriptions as the worker would have them.
/// </summary>
internal static class BillingData
{
    /// <summary>The tiers of the design: code, from, to, analysis, monthly, yearly (null = by agreement).</summary>
    public static readonly (string Code, int Min, int? Max, decimal? Analysis, decimal? Monthly, decimal? Yearly)[] Tiers =
    [
        ("t500", 0, 500, 39, 9, 90),
        ("t2000", 501, 2000, 69, 19, 190),
        ("t5000", 2001, 5000, 99, 29, 290),
        ("t20000", 5001, 20000, 199, 59, 590),
        ("custom", 20001, null, null, null, null),
    ];

    public static async Task<string> MarketAsync(string currency = "EUR")
    {
        var code = "x" + Guid.NewGuid().ToString("N")[..7];
        await AdminAsync(
            """
            INSERT INTO ref.markets (code, country_code, default_locale, ui_locales, jurisdiction, currency, web_status, checks_status, created_at, updated_at)
            VALUES ($1, 'SK', 'sk', '{sk}', 'sk', $2, 'hidden', 'none', now(), now())
            """, code, currency);
        return code;
    }

    /// <summary>A price list of the market: a draft without ids of Stripe, or published (and active) with fake ids.</summary>
    public static async Task<Guid> PriceListAsync(
        string market, string status = "draft", string currency = "EUR", DateTimeOffset? validFrom = null, (int From, decimal Percent)? discount = null,
        string? mode = null, IReadOnlyDictionary<string, decimal>? monthly = null, int noticeDays = 30)
    {
        var id = Guid.CreateVersion7();
        var published = status != "draft";
        var at = validFrom ?? DateTimeOffset.UtcNow.AddHours(-1);
        await AdminAsync(
            """
            INSERT INTO billing.price_lists (id, name, market_code, currency, valid_from, published_at, activated_at, status, notice_days, stripe_mode, sync_status,
                                             fair_use_other_pages_factor, created_at, updated_at)
            VALUES ($1, 'test', $2, $3, $4, $5, $5, $6, $7, $8, $9, 2, now(), now())
            """, id, market, currency, at, published ? at : null, status, noticeDays, published ? mode ?? "test" : mode, published ? "synced" : "pending");
        var key = id.ToString("N")[..8];
        foreach (var (code, min, max, analysis, month, yearly) in Tiers)
        {
            var withIds = published && analysis is not null;
            var price = monthly is not null && monthly.TryGetValue(code, out var custom) ? custom : month;
            await AdminAsync(
                """
                INSERT INTO billing.price_tiers (price_list_id, code, min_products, max_products, analysis_price, monitoring_monthly, monitoring_yearly,
                    stripe_price_analysis, stripe_price_monthly, stripe_price_yearly, lookup_key_analysis, lookup_key_monthly, lookup_key_yearly, created_at, updated_at)
                VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, now(), now())
                """, id, code, min, max, analysis, price, yearly,
                withIds ? $"price_{key}_{code}_analysis" : null, withIds ? $"price_{key}_{code}_monthly" : null, withIds ? $"price_{key}_{code}_yearly" : null,
                analysis is null ? null : $"{market}_{currency.ToLowerInvariant()}_{code}_analysis",
                analysis is null ? null : $"{market}_{currency.ToLowerInvariant()}_{code}_monthly",
                analysis is null ? null : $"{market}_{currency.ToLowerInvariant()}_{code}_yearly");
        }

        if (discount is { } d)
        {
            await AdminAsync(
                "INSERT INTO billing.volume_discounts (price_list_id, from_shop_number, percent, stripe_coupon_id, stripe_mode, created_at, updated_at) VALUES ($1, $2, $3, $4, $5, now(), now())",
                id, d.From, d.Percent, published ? $"coupon_{key}_{d.From}" : null, published ? mode ?? "test" : null);
        }

        return id;
    }

    public static async Task<Guid> TenantAsync(string market, string country = "SK", string? icDph = null, string taxIdStatus = "none", DateTimeOffset? founderUntil = null)
    {
        var id = Guid.CreateVersion7();
        await AdminAsync(
            """
            INSERT INTO iam.tenants (id, name, legal_name, ico, ic_dph, street, city, postal_code, country_code, billing_email, locale, market_code, status,
                                     tax_id_status, founder_until, created_at, updated_at)
            VALUES ($1, 'Bylinkovo', 'Bylinkovo s.r.o.', '12345678', $2, 'Hlavná 1', 'Trenčín', '91101', $3, 'fakturacia@bylinkovo.test', 'sk', $4, 'active',
                    $5, $6, now(), now())
            """, id, icDph, country, market, taxIdStatus, founderUntil);
        return id;
    }

    public static async Task<Guid> ShopAsync(Guid tenantId, string status = "active")
    {
        var id = Guid.CreateVersion7();
        var domain = $"bylinkovo-{Guid.NewGuid():N}"[..20] + ".sk";
        await AdminAsync(
            """
            INSERT INTO shop.shops (id, tenant_id, domain, base_url, base_path, home_country, platform, source_mode, status, created_at, updated_at)
            VALUES ($1, $2, $3, $4, '/', 'SK', 'shoptet', 'web', $5, now(), now())
            """, id, tenantId, domain, $"https://{domain}/", status);
        return id;
    }

    /// <summary>A running subscription of the e-shop in the tier with its next period from <paramref name="periodEnd"/>.</summary>
    public static async Task<Guid> SubscriptionAsync(
        Guid tenantId, Guid shopId, Guid priceListId, string tier, decimal unitPrice, DateTimeOffset periodEnd, string status = "active", string? stripePriceId = null,
        string? stripeSubscriptionId = null)
    {
        var id = Guid.CreateVersion7();
        await AdminAsync(
            """
            INSERT INTO billing.subscriptions (id, tenant_id, shop_id, stripe_subscription_id, status, interval, price_list_id, tier_code, unit_price, stripe_price_id,
                                               current_period_start, current_period_end, created_at, updated_at)
            VALUES ($1, $2, $3, $4, $5, 'month', $6, $7, $8, $9, $10, $11, now(), now())
            """, id, tenantId, shopId, stripeSubscriptionId ?? "sub_" + id.ToString("N"), status, priceListId, tier, unitPrice, stripePriceId, periodEnd.AddMonths(-1), periodEnd);
        return id;
    }
}
