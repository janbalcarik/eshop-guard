using System.Text.Json;
using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Shops;

/// <summary>Connection to the e-shop platform; credentials are encrypted and never logged. Table <c>shop.connectors</c>.</summary>
public sealed class Connector : TenantEntity
{
    public Guid ShopId { get; set; }

    public ShopPlatform Platform { get; set; }

    public ConnectorStatus Status { get; set; }

    public string? ExternalShopId { get; set; }

    public ConnectorAccess Access { get; set; }

    public byte[]? CredentialsEnc { get; set; }

    public string? CredentialsKeyId { get; set; }

    public DateTimeOffset? TokenExpiresAt { get; set; }

    public string[] Scopes { get; set; } = [];

    public string? SyncCursor { get; set; }

    public DateTimeOffset? LastReconcileAt { get; set; }

    public DateTimeOffset? LastWebhookAt { get; set; }

    public byte[]? WebhookSecretHash { get; set; }

    public JsonDocument? Health { get; set; }
}
