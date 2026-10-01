using System.Text.Json;
using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Shops;

/// <summary>Received connector event (monthly partitions, kept 30 days). Table <c>shop.connector_events</c>.</summary>
public sealed class ConnectorEvent : ITenantOwned, IHasCreatedAt
{
    public long Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid ConnectorId { get; set; }

    public Guid ShopId { get; set; }

    public required string DedupeKey { get; set; }

    public required string EventType { get; set; }

    public string? ResourceType { get; set; }

    public string? ResourceExternalId { get; set; }

    public required JsonDocument Payload { get; set; }

    public DateTimeOffset ReceivedAt { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }

    public required string Status { get; set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }
}
