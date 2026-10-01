using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Shops;

/// <summary>Webhook subscription of a connector. Table <c>shop.connector_webhooks</c>.</summary>
public sealed class ConnectorWebhook : TenantEntity
{
    public Guid ConnectorId { get; set; }

    public required string Event { get; set; }

    public string? ExternalId { get; set; }

    public required string Status { get; set; }

    public DateTimeOffset? LastVerifiedAt { get; set; }
}
