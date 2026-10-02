namespace EshopGuard.Billing.Contracts;

/// <summary>A price list for the admin API (no ids of Stripe; <c>Synced</c> says whether its prices exist in Stripe).</summary>
public sealed record PriceListDto(
    Guid Id,
    string Name,
    string MarketCode,
    string Currency,
    DateTimeOffset ValidFrom,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? ActivatedAt,
    string Status,
    string SyncStatus,
    string? SyncError,
    string? StripeMode,
    int NoticeDays,
    decimal FairUseOtherPagesFactor,
    IReadOnlyList<PriceTierDto> Tiers,
    IReadOnlyList<VolumeDiscountDto> VolumeDiscounts);

public sealed record PriceTierDto(
    string Code, int MinProducts, int? MaxProducts, decimal? AnalysisPrice, decimal? MonitoringMonthly, decimal? MonitoringYearly, bool IsCustom, bool Synced, bool Archived);

public sealed record VolumeDiscountDto(int FromShopNumber, decimal Percent);

/// <summary>The impact of a price list on the running subscriptions of its market and currency (requirement „Náhled dopadu“).</summary>
public sealed record PriceListImpactDto(
    DateTimeOffset ComputedAt, int Increase, int Decrease, int Unchanged, DateTimeOffset? FirstIncreaseAt, DateTimeOffset? FirstDecreaseAt, int NoticeDays);

/// <summary>A command still running in the worker: its job and state (<c>queued</c>, <c>running</c>).</summary>
public sealed record AdminJobDto(long JobId, string State);

/// <summary>The active price list of a market for the public web (changes 13 and 14), without anything of Stripe.</summary>
public sealed record PublicPriceListDto(string Market, string Currency, DateTimeOffset ValidFrom, int NoticeDays, IReadOnlyList<PublicPriceTierDto> Tiers, IReadOnlyList<VolumeDiscountDto> VolumeDiscounts);

public sealed record PublicPriceTierDto(string Code, int MinProducts, int? MaxProducts, decimal? AnalysisPrice, decimal? MonitoringMonthly, decimal? MonitoringYearly, bool IsCustom);

/// <summary>
/// An order of the analysis with monitoring (the snapshot of the confirmed quote). <c>AwaitingConfirmation</c>: the customer came
/// back from Stripe and the payment waits for the webhook (only the webhook confirms a payment).
/// </summary>
public sealed record OrderDto(
    Guid Id,
    Guid ShopId,
    string Status,
    bool AwaitingConfirmation,
    Guid? QuoteId,
    string? TierCode,
    string Currency,
    decimal AnalysisNet,
    decimal VatRate,
    decimal VatAmount,
    decimal AnalysisGross,
    decimal? MonitoringMonthlyNet,
    decimal? MonitoringDiscountPercent,
    string? TaxTreatment,
    DateTimeOffset? TrialEndPlanned,
    DateTimeOffset? CheckoutExpiresAt,
    DateTimeOffset? PaidAt,
    Guid? RunId,
    DateTimeOffset CreatedAt);

/// <summary>The page of Stripe Checkout to send the customer to.</summary>
public sealed record CheckoutDto(Guid OrderId, string Url, DateTimeOffset ExpiresAt);
