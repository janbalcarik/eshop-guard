using System.Text.Json;
using System.Text.Json.Nodes;
using EshopGuard.Application.Contracts;
using EshopGuard.Application.Shops.Pricing;
using EshopGuard.Application.Shops.Scope;
using EshopGuard.Billing.Tax;
using EshopGuard.Data;
using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Billing;
using EshopGuard.Data.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using Npgsql;

namespace EshopGuard.Billing.Pricing;

/// <summary>
/// The price of a scope of change 10 (requirement „Ocenění rozsahu e-shopu“, tasks 4.1–4.3):
/// <list type="number">
/// <item>the active price list of the market and currency of the tenant (the currency of the market while the tenant has none);
/// without it <c>unavailable</c> / <c>billing.price_list_missing</c>, from another mode of Stripe <c>billing.stripe_mode_mismatch</c>;</item>
/// <item>the tier by <c>ShopScope.PriceCount</c> (products of every ticked market, or pages to check) from <c>billing.price_tiers</c>;</item>
/// <item>fair use: other pages above the factor × products, or the tier <c>custom</c>, is an individual offer that cannot be paid;</item>
/// <item>the discount by the order of the e-shop in the account, the preview of VAT by the tax treatment;</item>
/// <item>the price the customer saw is stored once per e-shop, <c>scopeHash</c> and price list (a repeated quote returns the same
/// <c>quoteId</c> and amounts); the state of the e-shop does not change.</item>
/// </list>
/// The reasons why versions are not checked stay in the scope as change 10 gave them.
/// </summary>
public sealed class PriceQuoteService(EshopGuardDb db, PriceListReader prices, IOptions<BillingOptions> options, TimeProvider time) : IPriceQuoteService
{
    public static readonly TimeSpan Validity = TimeSpan.FromDays(1);

    public Task<PriceQuoteDto> QuoteAsync(PriceQuoteRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        return db.ExecuteInTenantTransactionAsync(async () =>
        {
            var quote = await StoreAsync(request, await ComputeAsync(request, ct).ConfigureAwait(false), ct).ConfigureAwait(false);
            var list = quote.PriceListId is { } id ? await prices.GetAsync(id, ct).ConfigureAwait(false) : null;
            return ToDto(quote, quote.TierCode is { } code ? list?.Tier(code)?.MaxProducts : null);
        }, ct);
    }

    /// <summary>The quote of a scope without storing it (the order compares it with the confirmed one); in the open transaction of the tenant.</summary>
    public async Task<PriceQuote> ComputeAsync(PriceQuoteRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var now = time.GetUtcNow();
        var scope = request.Scope;
        var tenant = await db.Tenants.AsNoTracking().FirstAsync(t => t.Id == request.TenantId, ct).ConfigureAwait(false);
        var currency = tenant.Currency ?? await prices.MarketCurrencyAsync(tenant.MarketCode, ct).ConfigureAwait(false) ?? string.Empty;
        var count = scope.PriceCount ?? 0;
        var quote = new PriceQuote
        {
            TenantId = request.TenantId,
            ShopId = request.ShopId,
            BasisRunId = scope.SampleRunId,
            ScopeHash = scope.ScopeHash,
            PriceUnit = scope.PriceUnit,
            CountedProducts = scope.PriceCount,
            OtherPages = scope.OtherPagesTotal,
            Versions = JsonDocument.Parse(Versions(scope).ToJsonString()),
            Markets = scope.Markets.ToArray(),
            Currency = currency,
            CreatedBy = request.RequestedBy,
            CreatedAt = now,
            UpdatedAt = now,
        };

        var list = await prices.ActiveAsync(tenant.MarketCode, currency, ct).ConfigureAwait(false);
        if (list is null)
        {
            return Unavailable(quote, BillingCodes.PriceListMissing);
        }

        quote.PriceListId = list.List.Id;
        if (options.Value.StripeEnabled && list.List.StripeMode is { } mode && SnakeCaseEnumConverter<StripeMode>.ToText(mode) != options.Value.Stripe.Mode)
        {
            return Unavailable(quote, BillingCodes.StripeModeMismatch);
        }

        var tier = TierResolver.Resolve(list.Tiers, count);
        var fairUse = FairUsePolicy.Evaluate(scope.PriceUnit, count, scope.OtherPagesTotal, list.List.FairUseOtherPagesFactor);
        quote.TierCode = tier.Code;
        quote.FairUse = JsonDocument.Parse(new JsonObject { ["limit"] = fairUse.Limit, ["exceeded"] = fairUse.Exceeded, ["factor"] = list.List.FairUseOtherPagesFactor }.ToJsonString());
        if (fairUse.Exceeded || tier.IsCustom)
        {
            quote.Status = PriceQuoteStatus.IndividualOffer;
            quote.ReasonCode = fairUse.Exceeded ? BillingCodes.FairUseExceeded : BillingCodes.IndividualOffer;
            return quote;
        }

        var ordinal = 1 + await OtherRunningShopsAsync(request.ShopId, ct).ConfigureAwait(false);
        var discount = VolumeDiscountResolver.Resolve(list.Discounts, ordinal);
        quote.Status = PriceQuoteStatus.Offer;
        quote.AnalysisPrice = tier.Tier!.AnalysisPrice;
        quote.MonitoringMonthly = tier.Tier.MonitoringMonthly;
        quote.DiscountPercent = discount?.Percent;
        var treatment = TaxTreatmentResolver.Resolve(TaxBuyer.Of(tenant), options.Value.Tax);
        var preview = TaxTreatmentResolver.Preview(treatment, quote.AnalysisPrice, DiscountedMonthly(quote.MonitoringMonthly, quote.DiscountPercent), options.Value.Tax);
        quote.VatPreview = JsonDocument.Parse(preview.ToJsonString());
        return quote;
    }

    /// <summary>The monthly monitoring after the volume discount (rounded to cents).</summary>
    public static decimal? DiscountedMonthly(decimal? monthly, decimal? discountPercent) =>
        monthly is { } price && discountPercent is { } percent
            ? Math.Round(price * (100m - percent) / 100m, 2, MidpointRounding.AwayFromZero)
            : monthly;

    /// <summary>
    /// Stores the quote once per e-shop, scope and price list (<c>ON CONFLICT DO NOTHING</c>); a repeated quote returns the stored
    /// row, so the same <c>quoteId</c> and the same amounts.
    /// </summary>
    public async Task<PriceQuote> StoreAsync(PriceQuoteRequest request, PriceQuote quote, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(quote);
        var transaction = (NpgsqlTransaction)db.Database.CurrentTransaction!.GetDbTransaction();
        await BillingSql.ExecuteAsync(transaction,
            """
            INSERT INTO billing.price_quotes (id, tenant_id, shop_id, basis_run_id, scope_hash, price_list_id, tier_code, price_unit, counted_products, other_pages,
                                              versions, markets, analysis_price, monitoring_monthly, discount_percent, currency, vat_preview, fair_use, status, reason_code,
                                              created_by, created_at, updated_at)
            VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18, $19, $20, $21, $22, $22)
            ON CONFLICT (shop_id, scope_hash, price_list_id) DO NOTHING
            """, ct, quote.Id, request.TenantId, request.ShopId, quote.BasisRunId, quote.ScopeHash, quote.PriceListId, quote.TierCode, quote.PriceUnit,
            quote.CountedProducts, quote.OtherPages, BillingSql.Json(JsonNode.Parse(quote.Versions.RootElement.GetRawText())), quote.Markets, quote.AnalysisPrice,
            quote.MonitoringMonthly, quote.DiscountPercent, quote.Currency, BillingSql.Json(Node(quote.VatPreview)), BillingSql.Json(Node(quote.FairUse)),
            SnakeCaseEnumConverter<PriceQuoteStatus>.ToText(quote.Status), quote.ReasonCode, quote.CreatedBy, quote.CreatedAt).ConfigureAwait(false);
        return await db.PriceQuotes.AsNoTracking()
            .FirstAsync(q => q.ShopId == request.ShopId && q.ScopeHash == quote.ScopeHash && q.PriceListId == quote.PriceListId, ct).ConfigureAwait(false);
    }

    /// <summary>The answer of <c>POST …/quote</c> from a stored quote.</summary>
    public PriceQuoteDto ToDto(PriceQuote quote, int? tierMaxProducts)
    {
        ArgumentNullException.ThrowIfNull(quote);
        var now = time.GetUtcNow();
        var payable = quote.Status == PriceQuoteStatus.Offer;
        int? limit = quote.FairUse?.RootElement.TryGetProperty("limit", out var l) == true && l.ValueKind == JsonValueKind.Number ? l.GetInt32() : null;
        var exceeded = quote.FairUse?.RootElement.TryGetProperty("exceeded", out var e) == true && e.ValueKind == JsonValueKind.True;
        return new PriceQuoteDto(
            quote.Id,
            quote.PriceListId,
            quote.Currency,
            quote.TierCode,
            tierMaxProducts,
            quote.TierCode == TierResolver.CustomCode,
            new FairUseDto(limit, exceeded),
            quote.AnalysisPrice,
            quote.MonitoringMonthly,
            quote.DiscountPercent,
            payable ? quote.AnalysisPrice : null,
            payable ? BillingPeriods.TrialEnd(now, options.Value.TrialMode) : null,
            quote.VatPreview?.RootElement.Clone(),
            now + Validity,
            SnakeCaseEnumConverter<PriceQuoteStatus>.ToText(quote.Status),
            quote.ReasonCode,
            quote.PriceUnit,
            quote.CountedProducts);
    }

    private static PriceQuote Unavailable(PriceQuote quote, string code)
    {
        quote.Status = PriceQuoteStatus.Unavailable;
        quote.ReasonCode = code;
        return quote;
    }

    private async Task<int> OtherRunningShopsAsync(Guid shopId, CancellationToken ct) =>
        await db.Subscriptions.AsNoTracking()
            .Where(s => s.ShopId != shopId && (s.Status == SubscriptionStatus.Trialing || s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.PastDue))
            .Select(s => s.ShopId).Distinct().CountAsync(ct).ConfigureAwait(false);

    /// <summary>The versions of the scope as stored with the quote (codes and numbers, no text).</summary>
    private static JsonObject Versions(ShopScope scope) => new()
    {
        ["checked"] = new JsonArray(scope.CheckedVersions.Select(v => (JsonNode)new JsonObject
        {
            ["language"] = v.Language,
            ["products"] = v.ProductCount,
            ["other_pages"] = v.OtherPageCount,
            ["markets"] = new JsonArray(v.Markets.Select(m => (JsonNode)JsonValue.Create(m)!).ToArray()),
            ["reason"] = v.Reason,
        }).ToArray()),
        ["not_checked"] = new JsonArray(scope.NotCheckedVersions.Select(v => (JsonNode)new JsonObject { ["language"] = v.Language, ["reason"] = v.Reason }).ToArray()),
        ["by_market"] = new JsonArray(scope.ByMarket.Select(m => (JsonNode)new JsonObject
        {
            ["market"] = m.MarketCode, ["language"] = m.Language, ["products"] = m.ProductCount, ["pages"] = m.PageCount,
        }).ToArray()),
    };

    private static JsonNode? Node(JsonDocument? document) => document is null ? null : JsonNode.Parse(document.RootElement.GetRawText());
}
