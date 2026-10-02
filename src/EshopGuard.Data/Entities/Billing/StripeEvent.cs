using System.Text.Json;
using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Billing;

/// <summary>Received Stripe webhook (deduplication, kept 90 days). Table <c>billing.stripe_events</c>.</summary>
public sealed class StripeEvent : IHasTimestamps
{
    public required string Id { get; set; }

    public required string Type { get; set; }

    public Guid? TenantId { get; set; }

    public DateTimeOffset ReceivedAt { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }

    public StripeEventStatus Status { get; set; }

    public bool Livemode { get; set; }

    public int Attempts { get; set; }

    /// <summary>The id of the object of the event (the processing loads its current state from Stripe).</summary>
    public string? ObjectId { get; set; }

    public required JsonDocument Payload { get; set; }

    public string? Error { get; set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset UpdatedAt { get; set; }
}
