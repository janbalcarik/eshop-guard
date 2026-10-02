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

    /// <summary>The order whose payment started the subscription (null for monitoring started again).</summary>
    public Guid? OrderId { get; set; }

    public string? StripePriceId { get; set; }

    public string? StripeCouponId { get; set; }

    public string? StripeScheduleId { get; set; }

    /// <summary>Fingerprint of the phases last written to the Subscription Schedule (no call when unchanged).</summary>
    public string? ScheduleHash { get; set; }

    /// <summary>Order of the e-shop in the account by the start of its subscription (volume discount).</summary>
    public int? ShopOrdinal { get; set; }

    public DateTimeOffset? TrialReminderSentAt { get; set; }

    /// <summary>Why monitoring stopped (<c>payment_failed</c>, <c>canceled</c>).</summary>
    public string? PauseReason { get; set; }

    /// <summary>Concurrency token (PostgreSQL <c>xmin</c>).</summary>
    public uint Version { get; set; }
}
