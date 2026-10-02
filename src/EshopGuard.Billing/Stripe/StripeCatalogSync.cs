using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using EshopGuard.Billing.Jobs;
using EshopGuard.Billing.Pricing;
using EshopGuard.Data.Connections;
using EshopGuard.Jobs.Queue;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace EshopGuard.Billing.Stripe;

/// <summary>The outcome of a synchronization: published, or the error of Stripe (transient: the job tries again).</summary>
public sealed record CatalogSyncResult(bool Published, string? ErrorCode, bool Transient);

/// <summary>
/// The catalog of a price list in Stripe (requirement „Synchronizace ceníku do Stripe“, tasks 3.4, 3.5 and 3.8):
/// <list type="bullet">
/// <item>the products „analysis“ and „monitoring“ once per mode (<c>ops.system_settings</c> <c>billing:stripe_products:{mode}</c>);</item>
/// <item>three prices per tier with a price (analysis once, monitoring monthly and yearly; <c>tax_behavior = exclusive</c>), the
/// lookup keys stay on the old prices until the activation; a coupon per volume discount limited to the product of monitoring;</item>
/// <item>every call with the key <c>price:{priceListId}:{tier}:{kind}</c> / <c>coupon:{priceListId}:{from}</c>; each id is
/// stored at once, so a failed synchronization goes on where it stopped and never creates an object twice;</item>
/// <item>the price list becomes <c>published</c> only when every id is stored; activation (at <c>valid_from</c>) moves the
/// lookup keys, points the market to it and retires the previous one.</item>
/// </list>
/// </summary>
public sealed class StripeCatalogSync(
    EshopGuardDataSource dataSource, IStripeGateway stripe, IJobQueue queue, IOptions<BillingOptions> options, TimeProvider time, ILogger<StripeCatalogSync> logger)
{
    private string Mode => options.Value.Stripe.Mode;

    /// <summary>
    /// Creates what is missing in Stripe and publishes the price list in one transaction with the job of its activation (a
    /// second run publishes nothing twice). Errors of Stripe are stored as <c>sync_status = failed</c>.
    /// </summary>
    public async Task<CatalogSyncResult> SyncAsync(Guid priceListId, CancellationToken ct)
    {
        if (!options.Value.StripeEnabled)
        {
            return new CatalogSyncResult(false, BillingCodes.Unavailable, false);
        }

        await using var connection = await dataSource.Source.OpenConnectionAsync(ct).ConfigureAwait(false);
        var list = await ReadAsync(connection, priceListId, ct).ConfigureAwait(false);
        if (list is null)
        {
            return new CatalogSyncResult(false, BillingCodes.PriceListNotFound, false);
        }

        if (list.Status != "draft")
        {
            return new CatalogSyncResult(list.Status == "published", null, false);
        }

        if (list.StripeMode is { } stored && stored != Mode)
        {
            await MarkFailedAsync(connection, priceListId, BillingCodes.StripeModeMismatch, ct).ConfigureAwait(false);
            return new CatalogSyncResult(false, BillingCodes.StripeModeMismatch, false);
        }

        try
        {
            var products = await ProductsAsync(connection, ct).ConfigureAwait(false);
            foreach (var tier in list.Tiers.Where(t => t.AnalysisPrice is not null && t.MonitoringMonthly is not null))
            {
                await PriceAsync(connection, list, tier, PriceKinds.Analysis, products.Analysis, tier.AnalysisPrice!.Value, null, tier.StripePriceAnalysis, "stripe_price_analysis", ct)
                    .ConfigureAwait(false);
                await PriceAsync(connection, list, tier, PriceKinds.Monthly, products.Monitoring, tier.MonitoringMonthly!.Value, "month", tier.StripePriceMonthly, "stripe_price_monthly", ct)
                    .ConfigureAwait(false);
                if (tier.MonitoringYearly is { } yearly)
                {
                    await PriceAsync(connection, list, tier, PriceKinds.Yearly, products.Monitoring, yearly, "year", tier.StripePriceYearly, "stripe_price_yearly", ct).ConfigureAwait(false);
                }
            }

            foreach (var discount in list.Discounts.Where(d => d.StripeCouponId is null))
            {
                var coupon = await stripe.CreateCouponAsync(
                    new StripeCouponRequest(discount.Percent, products.Monitoring, $"{list.Market.ToUpperInvariant()} −{discount.Percent.ToString("0.##", CultureInfo.InvariantCulture)} % od {discount.FromShopNumber}. e-shopu",
                        Metadata(list.Id, ("from_shop_number", discount.FromShopNumber.ToString(CultureInfo.InvariantCulture)))),
                    $"coupon:{list.Id:N}:{discount.FromShopNumber}", ct).ConfigureAwait(false);
                await SaveAsync(connection, "UPDATE billing.volume_discounts SET stripe_coupon_id = $2, stripe_mode = $3, updated_at = now() WHERE id = $1", ct, discount.Id, coupon, Mode)
                    .ConfigureAwait(false);
            }
        }
        catch (StripeGatewayException e)
        {
            await MarkFailedAsync(connection, priceListId, e.Code, ct).ConfigureAwait(false);
            logger.LogWarning("price_list.sync_failed {PriceListId} {Code} {Status}", priceListId, e.Code, e.HttpStatus);
            return new CatalogSyncResult(false, e.Code, e.Transient);
        }

        var now = time.GetUtcNow();
        await using (var transaction = await BillingSql.BeginGlobalAsync(connection, null, ct).ConfigureAwait(false))
        {
            var updated = await BillingSql.ExecuteAsync(transaction,
                """
                UPDATE billing.price_lists SET status = 'published', published_at = $2, sync_status = 'synced', sync_error = NULL, stripe_mode = $3, updated_at = $2
                WHERE id = $1 AND status = 'draft'
                """, ct, priceListId, now, Mode).ConfigureAwait(false);
            if (updated == 1)
            {
                await queue.EnqueueAsync(BillingJobs.ActivatePriceList(priceListId, list.ValidFrom), transaction, ct).ConfigureAwait(false);
                await BillingSql.AuditAsync(transaction, null, null, BillingSql.System, "price_list.published", "price_list", priceListId.ToString("D"),
                    new JsonObject { ["mode"] = Mode, ["valid_from"] = list.ValidFrom }, now, ct).ConfigureAwait(false);
            }

            await transaction.CommitAsync(ct).ConfigureAwait(false);
        }
        logger.LogInformation("price_list.published {PriceListId} {Mode}", priceListId, Mode);
        return new CatalogSyncResult(true, null, false);
    }

    /// <summary>
    /// Activates a published price list at its <c>valid_from</c>: the lookup keys move to its prices
    /// (<c>transfer_lookup_key</c>), the market points to it, the previous lists of the market and currency are retired and the
    /// transfer of the running subscriptions is enqueued. A second activation changes nothing.
    /// </summary>
    public async Task<CatalogSyncResult> ActivateAsync(Guid priceListId, CancellationToken ct)
    {
        await using var connection = await dataSource.Source.OpenConnectionAsync(ct).ConfigureAwait(false);
        var list = await ReadAsync(connection, priceListId, ct).ConfigureAwait(false);
        if (list is null || list.Status != "published" || list.ActivatedAt is not null)
        {
            return new CatalogSyncResult(list?.ActivatedAt is not null, null, false);
        }

        try
        {
            foreach (var tier in list.Tiers)
            {
                foreach (var (price, key, kind) in new[]
                {
                    (tier.StripePriceAnalysis, tier.LookupKeyAnalysis, PriceKinds.Analysis),
                    (tier.StripePriceMonthly, tier.LookupKeyMonthly, PriceKinds.Monthly),
                    (tier.StripePriceYearly, tier.LookupKeyYearly, PriceKinds.Yearly),
                })
                {
                    if (price is not null && key is not null)
                    {
                        await stripe.TransferLookupKeyAsync(price, key, $"lookup:{list.Id:N}:{tier.Code}:{kind}", ct).ConfigureAwait(false);
                    }
                }
            }
        }
        catch (StripeGatewayException e)
        {
            logger.LogWarning("price_list.activation_failed {PriceListId} {Code} {Status}", priceListId, e.Code, e.HttpStatus);
            return new CatalogSyncResult(false, e.Code, e.Transient);
        }

        var now = time.GetUtcNow();
        await using (var transaction = await BillingSql.BeginGlobalAsync(connection, null, ct).ConfigureAwait(false))
        {
            var updated = await BillingSql.ExecuteAsync(transaction,
                "UPDATE billing.price_lists SET activated_at = $2, updated_at = $2 WHERE id = $1 AND activated_at IS NULL", ct, priceListId, now).ConfigureAwait(false);
            if (updated == 0)
            {
                return new CatalogSyncResult(true, null, false);
            }

            await BillingSql.ExecuteAsync(transaction,
                """
                UPDATE billing.price_lists SET status = 'retired', updated_at = $4
                WHERE market_code = $1 AND currency = $2 AND status = 'published' AND id <> $3 AND valid_from <= (SELECT valid_from FROM billing.price_lists WHERE id = $3)
                """, ct, list.Market, list.Currency, priceListId, now).ConfigureAwait(false);
            await BillingSql.ExecuteAsync(transaction,
                "UPDATE ref.markets SET price_list_id = $2, updated_at = $3 WHERE code = $1 AND currency = $4", ct, list.Market, priceListId, now, list.Currency).ConfigureAwait(false);
            await queue.EnqueueAsync(BillingJobs.SchedulePriceListTransfer(priceListId), transaction, ct).ConfigureAwait(false);
            await BillingSql.AuditAsync(transaction, null, null, BillingSql.System, "price_list.activated", "price_list", priceListId.ToString("D"),
                new JsonObject { ["market"] = list.Market, ["currency"] = list.Currency }, now, ct).ConfigureAwait(false);
            await transaction.CommitAsync(ct).ConfigureAwait(false);
        }
        logger.LogInformation("price_list.activated {PriceListId}", priceListId);
        return new CatalogSyncResult(true, null, false);
    }

    /// <summary>The products of the mode, created once (analysis and monitoring) and kept in <c>ops.system_settings</c>.</summary>
    private async Task<(string Analysis, string Monitoring)> ProductsAsync(NpgsqlConnection connection, CancellationToken ct)
    {
        var key = $"billing:stripe_products:{Mode}";
        JsonObject stored;
        await using (var read = await connection.BeginTransactionAsync(ct).ConfigureAwait(false))
        {
            var value = await BillingSql.ScalarAsync<string>(read, "SELECT value::text FROM ops.system_settings WHERE key = $1", ct, key).ConfigureAwait(false);
            stored = value is null ? [] : JsonNode.Parse(value)!.AsObject();
            await read.CommitAsync(ct).ConfigureAwait(false);
        }

        var analysis = stored["analysis"]?.GetValue<string>();
        var monitoring = stored["monitoring"]?.GetValue<string>();
        analysis ??= await stripe.CreateProductAsync(new StripeProductRequest("EshopGuard – úvodná analýza", new Dictionary<string, string> { ["eshopguard_kind"] = "analysis" }),
            $"product:{Mode}:analysis", ct).ConfigureAwait(false);
        monitoring ??= await stripe.CreateProductAsync(new StripeProductRequest("EshopGuard – sledovanie zmien", new Dictionary<string, string> { ["eshopguard_kind"] = "monitoring" }),
            $"product:{Mode}:monitoring", ct).ConfigureAwait(false);
        if (stored["analysis"] is null || stored["monitoring"] is null)
        {
            await SaveAsync(connection, "UPDATE ops.system_settings SET value = $2, updated_at = now() WHERE key = $1", ct, key,
                BillingSql.Json(new JsonObject { ["analysis"] = analysis, ["monitoring"] = monitoring })).ConfigureAwait(false);
        }

        return (analysis, monitoring);
    }

    private async Task PriceAsync(
        NpgsqlConnection connection, ListRow list, TierRow tier, string kind, string product, decimal amount, string? interval, string? existing, string column, CancellationToken ct)
    {
        if (existing is not null)
        {
            return;
        }

        var id = await stripe.CreatePriceAsync(
            new StripePriceRequest(product, list.Currency, Money.ToMinor(amount), interval, null, Metadata(list.Id, ("tier", tier.Code), ("kind", kind))),
            $"price:{list.Id:N}:{tier.Code}:{kind}", ct).ConfigureAwait(false);
        await SaveAsync(connection, $"UPDATE billing.price_tiers SET {column} = $2, updated_at = now() WHERE id = $1", ct, tier.Id, id).ConfigureAwait(false);
    }

    private static Dictionary<string, string> Metadata(Guid priceListId, params (string Key, string Value)[] more)
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal) { ["price_list_id"] = priceListId.ToString("D") };
        foreach (var (key, value) in more)
        {
            metadata[key] = value;
        }

        return metadata;
    }

    private static async Task SaveAsync(NpgsqlConnection connection, string sql, CancellationToken ct, params object?[] parameters)
    {
        await using var transaction = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        await BillingSql.ExecuteAsync(transaction, sql, ct, parameters).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
    }

    private static Task MarkFailedAsync(NpgsqlConnection connection, Guid id, string code, CancellationToken ct) =>
        SaveAsync(connection, "UPDATE billing.price_lists SET sync_status = 'failed', sync_error = $2, updated_at = now() WHERE id = $1 AND status = 'draft'", ct, id, code);

    private static async Task<ListRow?> ReadAsync(NpgsqlConnection connection, Guid id, CancellationToken ct)
    {
        await using var transaction = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        var lists = await BillingSql.ListAsync(transaction,
            "SELECT id, market_code, currency, status, stripe_mode, valid_from, activated_at FROM billing.price_lists WHERE id = $1",
            r => new ListRow(r.GetGuid(0), r.GetString(1), r.GetString(2).Trim(), r.GetString(3), r.Get<string>(4), r.GetFieldValue<DateTimeOffset>(5), r.Get<DateTimeOffset?>(6), [], []),
            ct, id).ConfigureAwait(false);
        if (lists.Count == 0)
        {
            return null;
        }

        var tiers = await BillingSql.ListAsync(transaction,
            """
            SELECT id, code, analysis_price, monitoring_monthly, monitoring_yearly, stripe_price_analysis, stripe_price_monthly, stripe_price_yearly,
                   lookup_key_analysis, lookup_key_monthly, lookup_key_yearly
            FROM billing.price_tiers WHERE price_list_id = $1 ORDER BY min_products
            """,
            r => new TierRow(r.GetGuid(0), r.GetString(1), r.Get<decimal?>(2), r.Get<decimal?>(3), r.Get<decimal?>(4), r.Get<string>(5), r.Get<string>(6), r.Get<string>(7),
                r.Get<string>(8), r.Get<string>(9), r.Get<string>(10)), ct, id).ConfigureAwait(false);
        var discounts = await BillingSql.ListAsync(transaction,
            "SELECT id, from_shop_number, percent, stripe_coupon_id FROM billing.volume_discounts WHERE price_list_id = $1 ORDER BY from_shop_number",
            r => new DiscountRow(r.GetGuid(0), r.GetInt32(1), r.GetDecimal(2), r.Get<string>(3)), ct, id).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
        return lists[0] with { Tiers = tiers, Discounts = discounts };
    }

    private sealed record ListRow(
        Guid Id, string Market, string Currency, string Status, string? StripeMode, DateTimeOffset ValidFrom, DateTimeOffset? ActivatedAt,
        IReadOnlyList<TierRow> Tiers, IReadOnlyList<DiscountRow> Discounts);

    private sealed record TierRow(
        Guid Id, string Code, decimal? AnalysisPrice, decimal? MonitoringMonthly, decimal? MonitoringYearly, string? StripePriceAnalysis, string? StripePriceMonthly,
        string? StripePriceYearly, string? LookupKeyAnalysis, string? LookupKeyMonthly, string? LookupKeyYearly);

    private sealed record DiscountRow(Guid Id, int FromShopNumber, decimal Percent, string? StripeCouponId);
}

/// <summary>Amounts between the database (decimal, two places) and Stripe (minor units).</summary>
public static class Money
{
    public static long ToMinor(decimal amount) => (long)Math.Round(amount * 100m, 0, MidpointRounding.AwayFromZero);

    public static decimal FromMinor(long amount) => amount / 100m;
}
