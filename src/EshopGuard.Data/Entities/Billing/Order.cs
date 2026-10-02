using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Billing;

/// <summary>Order of an analysis ("Dnes zaplatíte"). Table <c>billing.orders</c>.</summary>
public sealed class Order : TenantEntity
{
    public Guid ShopId { get; set; }

    public OrderKind Kind { get; set; }

    public Guid PriceListId { get; set; }

    public string? TierCode { get; set; }

    public decimal AmountNet { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal VatRate { get; set; }

    public decimal VatAmount { get; set; }

    public decimal AmountGross { get; set; }

    public required string Currency { get; set; }

    public OrderStatus Status { get; set; }

    public string? StripeCheckoutSessionId { get; set; }

    public string? StripePaymentIntentId { get; set; }

    public Guid? RunId { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTimeOffset? PaidAt { get; set; }

    // The snapshot of the quote the customer confirmed (change 12, requirement „Garantovaná cena objednávky“).
    public Guid? PriceQuoteId { get; set; }

    public string? ScopeHash { get; set; }

    public string? StripePriceAnalysis { get; set; }

    public string? StripePriceMonitoring { get; set; }

    public string? StripeCouponId { get; set; }

    /// <summary>Monthly monitoring without VAT and before the discount, as quoted.</summary>
    public decimal? MonitoringMonthly { get; set; }

    public decimal? MonitoringDiscountPercent { get; set; }

    public string? TermsVersion { get; set; }

    public TaxTreatment? TaxTreatment { get; set; }

    /// <summary>End of the trial planned when the payment starts (<c>Billing:TrialMode</c>).</summary>
    public DateTimeOffset? TrialEndPlanned { get; set; }

    public string? StripeSubscriptionId { get; set; }

    public DateTimeOffset? CheckoutExpiresAt { get; set; }

    /// <summary>Number of Checkout Sessions created (part of the idempotency key <c>checkout:{orderId}:{attempt}</c>).</summary>
    public int CheckoutAttempt { get; set; }
}
