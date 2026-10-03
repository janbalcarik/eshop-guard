using EshopGuard.Data.Entities.Billing;
using Npgsql;

namespace EshopGuard.Billing.Pricing;

/// <summary>
/// Price lists read in an open transaction (the jobs of change 12 plan in the transaction of the tenant; price lists are global
/// rows without RLS): the list, its tiers and its volume discounts, as <see cref="PriceListReader"/> reads them.
/// </summary>
internal static class PriceListSql
{
    public static async Task<PriceListSnapshot?> LoadAsync(NpgsqlTransaction transaction, Guid id, CancellationToken ct)
    {
        var lists = await BillingSql.ListAsync(transaction,
            "SELECT id, name, market_code, currency, valid_from, published_at, status, notice_days FROM billing.price_lists WHERE id = $1",
            r => new PriceList
            {
                Id = r.GetGuid(0),
                Name = r.GetString(1),
                MarketCode = r.GetString(2),
                Currency = r.GetString(3).Trim(),
                ValidFrom = r.GetFieldValue<DateTimeOffset>(4),
                PublishedAt = r.Get<DateTimeOffset?>(5),
                Status = r.GetString(6) switch
                {
                    "published" => PriceListStatus.Published,
                    "retired" => PriceListStatus.Retired,
                    _ => PriceListStatus.Draft,
                },
                NoticeDays = r.GetInt32(7),
            }, ct, id).ConfigureAwait(false);
        if (lists.Count == 0)
        {
            return null;
        }

        var tiers = await BillingSql.ListAsync(transaction,
            """
            SELECT code, min_products, max_products, analysis_price, monitoring_monthly, monitoring_yearly, stripe_price_monthly, stripe_price_yearly
            FROM billing.price_tiers WHERE price_list_id = $1 ORDER BY min_products
            """,
            r => new PriceTier
            {
                PriceListId = id,
                Code = r.GetString(0),
                MinProducts = r.GetInt32(1),
                MaxProducts = r.Get<int?>(2),
                AnalysisPrice = r.Get<decimal?>(3),
                MonitoringMonthly = r.Get<decimal?>(4),
                MonitoringYearly = r.Get<decimal?>(5),
                StripePriceMonthly = r.Get<string>(6),
                StripePriceYearly = r.Get<string>(7),
            }, ct, id).ConfigureAwait(false);
        var discounts = await BillingSql.ListAsync(transaction,
            "SELECT from_shop_number, percent, stripe_coupon_id FROM billing.volume_discounts WHERE price_list_id = $1 ORDER BY from_shop_number",
            r => new VolumeDiscount { PriceListId = id, FromShopNumber = r.GetInt32(0), Percent = r.GetDecimal(1), StripeCouponId = r.Get<string>(2) }, ct, id)
            .ConfigureAwait(false);
        return new PriceListSnapshot(lists[0], tiers, discounts);
    }

    /// <summary>
    /// The price list new orders of the market and currency get now: published, valid, the newest (as
    /// <see cref="PriceListReader.ActiveAsync"/>).
    /// </summary>
    public static Task<Guid?> ActiveIdAsync(NpgsqlTransaction transaction, string market, string currency, DateTimeOffset now, CancellationToken ct) =>
        BillingSql.ScalarAsync<Guid?>(transaction,
            """
            SELECT id FROM billing.price_lists
            WHERE market_code = $1 AND currency = $2 AND status = 'published' AND valid_from <= $3
            ORDER BY valid_from DESC, published_at DESC NULLS LAST LIMIT 1
            """, ct, market, currency, now);
}
