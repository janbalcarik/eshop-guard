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

/// <summary>
/// The payment of an order with the card of the account (task 7.3): <c>processing</c> (the webhook confirms the payment) or
/// <c>requires_action</c> with the <c>ClientSecret</c> for 3-D Secure in the Payment Element; unconfirmed, it ends at <c>ExpiresAt</c>.
/// </summary>
public sealed record SavedCardPaymentDto(Guid OrderId, string Status, string? ClientSecret, DateTimeOffset ExpiresAt);

/// <summary>The monitoring of an e-shop after canceling or resuming (the webhook of Stripe brings the rest).</summary>
public sealed record SubscriptionStateDto(Guid ShopId, string Status, bool CancelAtPeriodEnd, DateTimeOffset? TrialEnd, DateTimeOffset? CurrentPeriodEnd);

/// <summary>
/// Monitoring started again (task 7.5): first <c>confirm_required</c> with the amount and the date of the first payment (today,
/// without a trial), after the confirmation <c>processing</c> or <c>requires_action</c> with the secret for 3-D Secure.
/// </summary>
public sealed record StartSubscriptionDto(
    string Status, string TierCode, decimal Amount, string Currency, decimal? DiscountPercent, DateTimeOffset Date, string? ClientSecret);

/// <summary>The customer portal of Stripe for the change of the card; only its URL, never an id of Stripe.</summary>
public sealed record CardPortalDto(string Url);

/// <summary>The SetupIntent of the Payment Element (the fallback of the portal); only its client secret.</summary>
public sealed record CardSetupIntentDto(string ClientSecret);

/// <summary>
/// The page „Predplatné a platby“ (task 7.8, design Billing): the e-shops with their monitoring, the card of the account, the tiers
/// of the active price list and the monthly sum of the monitoring after discounts. Amounts are net, texts come from the frontend.
/// </summary>
public sealed record BillingOverviewDto(
    string? Currency,
    IReadOnlyList<BillingShopDto> Shops,
    BillingCardDto? Card,
    IReadOnlyList<PublicPriceTierDto> Tiers,
    IReadOnlyList<VolumeDiscountDto> VolumeDiscounts,
    decimal MonthlyTotal);

/// <summary>
/// One e-shop. <c>Status</c>: <c>active</c>, <c>trial</c> (until <c>TrialEnd</c>), <c>ending</c> (canceled to <c>PeriodEnd</c>),
/// <c>past_due</c>, <c>incomplete</c> or <c>ended</c>. <c>NextPayment*</c> is empty when nothing more is charged.
/// </summary>
public sealed record BillingShopDto(
    Guid ShopId,
    string Domain,
    string? Name,
    IReadOnlyList<string> Markets,
    int? ProductCount,
    string? TierCode,
    int? TierMaxProducts,
    decimal? AnalysisNet,
    DateTimeOffset? AnalysisPaidAt,
    string Status,
    DateTimeOffset? TrialEnd,
    DateTimeOffset? PeriodEnd,
    decimal? MonthlyNet,
    decimal? DiscountPercent,
    int? ShopOrdinal,
    DateTimeOffset? NextPaymentAt,
    decimal? NextPaymentNet,
    BillingPendingChangeDto? PendingChange);

/// <summary>„Nová cena od …“: the earliest scheduled change of the price (tier, price list or discount).</summary>
public sealed record BillingPendingChangeDto(IReadOnlyList<string> Kinds, DateTimeOffset EffectiveAt, string? TierCode, decimal? MonthlyNet, decimal? DiscountPercent);

/// <summary>The card of the account („•••• 1881 · pre všetky e-shopy v účte“).</summary>
public sealed record BillingCardDto(string? Brand, string? Last4, int? ExpMonth, int? ExpYear);
