using System.Text.Json;
using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Billing;

/// <summary>Scheduled change of a subscription from the next period. Table <c>billing.subscription_changes</c>.</summary>
public sealed class SubscriptionChange : TenantEntity
{
    public Guid SubscriptionId { get; set; }

    public SubscriptionChangeKind Kind { get; set; }

    public JsonDocument? From { get; set; }

    public JsonDocument? To { get; set; }

    public DateTimeOffset EffectiveAt { get; set; }

    public DateTimeOffset? NotifiedAt { get; set; }

    public DateTimeOffset? AppliedAt { get; set; }

    public SubscriptionChangeStatus Status { get; set; }

    /// <summary>Why the change exists or was canceled (<c>tier_up</c>, <c>back_in_tier</c> …).</summary>
    public string? ReasonCode { get; set; }

    /// <summary>Fingerprint of the phases of the schedule the change was written with.</summary>
    public string? StripeSchedulePhaseHash { get; set; }
}
