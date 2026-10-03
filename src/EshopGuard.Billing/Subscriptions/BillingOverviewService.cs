using System.Text.Json;
using EshopGuard.Billing.Contracts;
using EshopGuard.Billing.Pricing;
using EshopGuard.Data;
using EshopGuard.Data.Entities.Billing;
using EshopGuard.Data.Entities.Shops;
using EshopGuard.Data.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace EshopGuard.Billing.Subscriptions;

/// <summary>
/// The page „Predplatné a platby“ (task 7.8, design Billing): every e-shop with monitoring (its running subscription, else the last
/// one that started), the card of the account, the tiers and volume discounts of the active price list and the monthly sum after
/// discounts. Only the database is read, so the page works without Stripe; a subscription that never started
/// (<see cref="PendingSubscriptions.NotStarted"/>) is not shown.
/// </summary>
public sealed class BillingOverviewService(EshopGuardDb db, PriceListReader prices)
{
    public const string Trial = "trial";
    public const string Ending = "ending";
    public const string Active = "active";
    public const string PastDue = "past_due";
    public const string Incomplete = "incomplete";
    public const string Ended = "ended";

    public async Task<BillingOverviewDto> GetAsync(CancellationToken ct)
    {
        var tenantId = db.TenantContext.RequireTenantId();
        var tenant = await db.Tenants.AsNoTracking().FirstAsync(t => t.Id == tenantId, ct).ConfigureAwait(false);
        var data = await db.ExecuteInTenantTransactionAsync(async () =>
        {
            var subscriptions = await db.Subscriptions.AsNoTracking()
                .Where(s => s.PauseReason == null || s.PauseReason != PendingSubscriptions.NotStarted)
                .ToListAsync(ct).ConfigureAwait(false);
            var shopIds = subscriptions.Select(s => s.ShopId).Distinct().ToList();
            var subscriptionIds = subscriptions.Select(s => s.Id).ToList();
            return new OverviewData(
                subscriptions,
                await db.Shops.AsNoTracking().Where(s => shopIds.Contains(s.Id)).ToListAsync(ct).ConfigureAwait(false),
                await db.ShopMarkets.AsNoTracking().Where(m => shopIds.Contains(m.ShopId) && m.Status == ShopMarketStatus.Active).ToListAsync(ct).ConfigureAwait(false),
                await db.Orders.AsNoTracking().Where(o => shopIds.Contains(o.ShopId) && o.PaidAt != null).ToListAsync(ct).ConfigureAwait(false),
                await db.PriceQuotes.AsNoTracking().Where(q => shopIds.Contains(q.ShopId)).Select(q => new QuoteCount(q.Id, q.CountedProducts)).ToListAsync(ct).ConfigureAwait(false),
                await db.SubscriptionChanges.AsNoTracking()
                    .Where(c => subscriptionIds.Contains(c.SubscriptionId)
                        && (c.Status == SubscriptionChangeStatus.Scheduled || c.Status == SubscriptionChangeStatus.Notified)
                        && (c.Kind == SubscriptionChangeKind.PriceList || c.Kind == SubscriptionChangeKind.Tier || c.Kind == SubscriptionChangeKind.Discount))
                    .ToListAsync(ct).ConfigureAwait(false),
                await db.PaymentMethods.AsNoTracking().FirstOrDefaultAsync(p => p.IsDefault && p.DetachedAt == null, ct).ConfigureAwait(false));
        }, ct).ConfigureAwait(false);

        var currency = tenant.Currency ?? await prices.MarketCurrencyAsync(tenant.MarketCode, ct).ConfigureAwait(false);
        var active = currency is null ? null : await prices.ActiveAsync(tenant.MarketCode, currency, ct).ConfigureAwait(false);
        var lists = new Dictionary<Guid, PriceListSnapshot?>();
        var shops = new List<BillingShopDto>();
        foreach (var shop in data.Shops)
        {
            var history = data.Subscriptions.Where(s => s.ShopId == shop.Id).OrderByDescending(s => s.CreatedAt).ToList();
            var subscription = history.FirstOrDefault(s => SubscriptionSync.Running.Contains(Text(s.Status))) ?? history[0];
            if (!lists.TryGetValue(subscription.PriceListId, out var list))
            {
                list = await prices.GetAsync(subscription.PriceListId, ct).ConfigureAwait(false);
                lists[subscription.PriceListId] = list;
            }

            shops.Add(Row(shop, subscription, list, data));
        }

        var ordered = shops
            .OrderBy(s => s.ShopOrdinal ?? int.MaxValue)
            .ThenBy(s => s.Domain, StringComparer.Ordinal)
            .ToList();
        var card = data.Card is null ? null : new BillingCardDto(data.Card.Brand, data.Card.Last4, data.Card.ExpMonth, data.Card.ExpYear);
        return new BillingOverviewDto(
            active?.List.Currency ?? currency,
            ordered,
            card,
            active?.Tiers.Select(t => new PublicPriceTierDto(t.Code, t.MinProducts, t.MaxProducts, t.AnalysisPrice, t.MonitoringMonthly, t.MonitoringYearly, TierResolver.IsCustom(t))).ToList() ?? [],
            active?.Discounts.Select(d => new VolumeDiscountDto(d.FromShopNumber, d.Percent)).ToList() ?? [],
            ordered.Where(s => s.Status is Trial or Active or PastDue).Sum(s => s.MonthlyNet ?? 0m));
    }

    private static BillingShopDto Row(Shop shop, Subscription subscription, PriceListSnapshot? list, OverviewData data)
    {
        var order = data.Orders.Where(o => o.ShopId == shop.Id)
            .OrderByDescending(o => o.Id == subscription.OrderId)
            .ThenByDescending(o => o.PaidAt)
            .FirstOrDefault();
        var counted = order?.PriceQuoteId is { } quoteId ? data.Quotes.FirstOrDefault(q => q.Id == quoteId)?.CountedProducts : null;
        var status = Status(subscription);
        var price = PriceQuoteService.DiscountedMonthly(subscription.UnitPrice, subscription.DiscountPercent) ?? subscription.UnitPrice;
        DateTimeOffset? next = status switch
        {
            Trial => subscription.TrialEnd,
            Active or PastDue => subscription.CurrentPeriodEnd,
            _ => null,
        };
        var change = PendingChange(subscription, data.Changes.Where(c => c.SubscriptionId == subscription.Id).ToList());
        var markets = data.Markets.Where(m => m.ShopId == shop.Id)
            .OrderByDescending(m => m.IsHome).ThenBy(m => m.CountryCode, StringComparer.Ordinal)
            .Select(m => m.CountryCode).ToList();
        return new BillingShopDto(
            shop.Id,
            shop.Domain,
            shop.Name,
            markets,
            counted ?? shop.ProductCount,
            subscription.TierCode,
            list?.Tier(subscription.TierCode)?.MaxProducts,
            order?.AmountNet,
            order?.PaidAt,
            status,
            subscription.TrialEnd,
            status switch
            {
                Ending => SubscriptionService.End(subscription),
                Ended => subscription.CanceledAt ?? subscription.CurrentPeriodEnd,
                _ => null,
            },
            subscription.Interval == BillingInterval.Month ? price : null,
            subscription.DiscountPercent,
            subscription.ShopOrdinal,
            next,
            next is null ? null : change is not null && change.EffectiveAt <= next ? change.MonthlyNet : price,
            change);
    }

    private static string Status(Subscription subscription) => subscription switch
    {
        { Status: SubscriptionStatus.Canceled or SubscriptionStatus.Paused } => Ended,
        { CancelAtPeriodEnd: true, Status: SubscriptionStatus.Trialing or SubscriptionStatus.Active or SubscriptionStatus.PastDue } => Ending,
        { Status: SubscriptionStatus.Trialing } => Trial,
        { Status: SubscriptionStatus.PastDue } => PastDue,
        { Status: SubscriptionStatus.Incomplete } => Incomplete,
        _ => Active,
    };

    /// <summary>
    /// The earliest scheduled change (all kinds taking effect that day together): the tier and the unit price of its target, the
    /// discount of a target that states it (null = the discount is lost), else the current one.
    /// </summary>
    private static BillingPendingChangeDto? PendingChange(Subscription subscription, IReadOnlyList<SubscriptionChange> changes)
    {
        if (changes.Count == 0)
        {
            return null;
        }

        var effectiveAt = changes.Min(c => c.EffectiveAt);
        var first = changes.Where(c => c.EffectiveAt == effectiveAt).OrderBy(c => c.CreatedAt).ToList();
        string? tier = null;
        decimal? unit = null;
        var discount = subscription.DiscountPercent;
        foreach (var change in first)
        {
            var to = change.To?.RootElement;
            tier = Read(to, "tier_code", e => e.GetString()) ?? tier;
            unit = Read(to, "unit_price", e => (decimal?)e.GetDecimal()) ?? unit;
            if (to is { ValueKind: JsonValueKind.Object } target && target.TryGetProperty("discount_percent", out _))
            {
                discount = Read(to, "discount_percent", e => (decimal?)e.GetDecimal());
            }
        }

        var kinds = first.Select(c => Kind(c.Kind)).Distinct(StringComparer.Ordinal).ToList();
        return new BillingPendingChangeDto(kinds, effectiveAt, tier ?? subscription.TierCode,
            PriceQuoteService.DiscountedMonthly(unit ?? subscription.UnitPrice, discount) ?? unit ?? subscription.UnitPrice, discount);
    }

    private static T? Read<T>(JsonElement? element, string name, Func<JsonElement, T?> read) =>
        element is { ValueKind: JsonValueKind.Object } e && e.TryGetProperty(name, out var value) && value.ValueKind is not JsonValueKind.Null ? read(value) : default;

    private static string Kind(SubscriptionChangeKind kind) => kind switch
    {
        SubscriptionChangeKind.PriceList => "price_list",
        SubscriptionChangeKind.Tier => "tier",
        _ => "discount",
    };

    private static string Text(SubscriptionStatus status) => status switch
    {
        SubscriptionStatus.Trialing => "trialing",
        SubscriptionStatus.Active => "active",
        SubscriptionStatus.PastDue => "past_due",
        SubscriptionStatus.Incomplete => "incomplete",
        _ => "ended",
    };

    private sealed record QuoteCount(Guid Id, int? CountedProducts);

    private sealed record OverviewData(
        IReadOnlyList<Subscription> Subscriptions,
        IReadOnlyList<Shop> Shops,
        IReadOnlyList<ShopMarket> Markets,
        IReadOnlyList<Order> Orders,
        IReadOnlyList<QuoteCount> Quotes,
        IReadOnlyList<SubscriptionChange> Changes,
        PaymentMethod? Card);
}
