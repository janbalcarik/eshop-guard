namespace EshopGuard.Api.Tests.Billing;

/// <summary>
/// Price lists of the tests (change 12). Price lists are global, so every test uses a market of its own (code <c>x…</c>,
/// hidden from the web) and points its tenant to it; the tiers and prices are those of the design (SK/EUR).
/// </summary>
internal static class BillingSeed
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

    /// <summary>A market of its own with the currency (hidden from the web, jurisdiction sk).</summary>
    public static async Task<string> MarketAsync(string currency = "EUR")
    {
        var code = "x" + Guid.NewGuid().ToString("N")[..7];
        await ApiTestBase.AdminAsync(
            """
            INSERT INTO ref.markets (code, country_code, default_locale, ui_locales, jurisdiction, currency, web_status, checks_status, created_at, updated_at)
            VALUES ($1, 'SK', 'sk', '{sk}', 'sk', $2, 'hidden', 'none', now(), now())
            """, code, currency);
        return code;
    }

    /// <summary>
    /// A published and active price list of the market with the tiers of the design, prices of Stripe <c>price_{list}_{tier}_{kind}</c>
    /// and optionally a volume discount; <paramref name="validFrom"/> defaults to an hour ago.
    /// </summary>
    public static async Task<Guid> PublishedAsync(
        string market, string currency = "EUR", DateTimeOffset? validFrom = null, (int From, decimal Percent)? discount = null, string mode = "test", decimal factor = 2,
        IReadOnlyDictionary<string, decimal>? monthly = null)
    {
        var id = Guid.CreateVersion7();
        await ApiTestBase.AdminAsync(
            """
            INSERT INTO billing.price_lists (id, name, market_code, currency, valid_from, published_at, activated_at, status, notice_days, stripe_mode, sync_status,
                                             fair_use_other_pages_factor, created_at, updated_at)
            VALUES ($1, 'test', $2, $3, $4, $4, $4, 'published', 30, $5, 'synced', $6, now(), now())
            """, id, market, currency, validFrom ?? DateTimeOffset.UtcNow.AddHours(-1), mode, factor);
        var key = id.ToString("N")[..8];
        foreach (var (code, min, max, analysis, month, yearly) in Tiers)
        {
            var priced = analysis is not null;
            var monthlyPrice = monthly is not null && monthly.TryGetValue(code, out var custom) ? custom : month;
            await ApiTestBase.AdminAsync(
                """
                INSERT INTO billing.price_tiers (price_list_id, code, min_products, max_products, analysis_price, monitoring_monthly, monitoring_yearly,
                    stripe_price_analysis, stripe_price_monthly, stripe_price_yearly, created_at, updated_at)
                VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, now(), now())
                """, id, code, min, max, analysis, monthlyPrice, yearly,
                priced ? $"price_{key}_{code}_analysis" : null, priced ? $"price_{key}_{code}_monthly" : null, priced ? $"price_{key}_{code}_yearly" : null);
        }

        if (discount is { } d)
        {
            await ApiTestBase.AdminAsync(
                "INSERT INTO billing.volume_discounts (price_list_id, from_shop_number, percent, stripe_coupon_id, stripe_mode, created_at, updated_at) VALUES ($1, $2, $3, $4, $5, now(), now())",
                id, d.From, d.Percent, $"coupon_{key}_{d.From}", mode);
        }

        return id;
    }

    /// <summary>The tenant in the market (its currency is not fixed yet, so the currency of the market applies).</summary>
    public static Task UseMarketAsync(Guid tenantId, string market) =>
        ApiTestBase.AdminAsync("UPDATE iam.tenants SET market_code = $2, currency = NULL WHERE id = $1", tenantId, market);
}
