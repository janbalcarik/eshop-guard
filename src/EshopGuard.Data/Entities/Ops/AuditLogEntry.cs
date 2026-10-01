using System.Net;
using System.Text.Json;
using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Ops;

/// <summary>Who approved, published, rolled back or changed a price (monthly partitions; tenant_id null = system action). Table <c>ops.audit_log</c>.</summary>
public sealed class AuditLogEntry : ITenantOwned, IHasCreatedAt
{
    public long Id { get; set; }

    public Guid? TenantId { get; set; }

    public DateTimeOffset At { get; set; }

    public Guid? ActorUserId { get; set; }

    public AuditActorKind ActorKind { get; set; }

    public required string Action { get; set; }

    public string? EntityType { get; set; }

    public string? EntityId { get; set; }

    public JsonDocument? Data { get; set; }

    public IPAddress? Ip { get; set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }
}
