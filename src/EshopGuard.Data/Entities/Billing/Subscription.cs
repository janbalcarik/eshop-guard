using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Billing;

/// <summary>Monitoring subscription of an e-shop. Table <c>billing.subscriptions</c>.</summary>
public sealed class Subscription : TenantEntity
{
    public Guid ShopId { get; set; }

    public string? StripeSubscriptionId { get; set; }

    public SubscriptionStatus Status { get; set; }

    public BillingInterval Interval { get; set; }

    public Guid PriceListId { get; set; }

    public required string TierCode { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal? DiscountPercent { get; set; }

    public DateTimeOffset? TrialEnd { get; set; }

    public DateTimeOffset? CurrentPeriodStart { get; set; }

    public DateTimeOffset? CurrentPeriodEnd { get; set; }

    public bool CancelAtPeriodEnd { get; set; }

    public DateTimeOffset? CanceledAt { get; set; }

    /// <summary>Concurrency token (PostgreSQL <c>xmin</c>).</summary>
    public uint Version { get; set; }
}
