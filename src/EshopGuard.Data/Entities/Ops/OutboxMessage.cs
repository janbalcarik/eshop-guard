using System.Text.Json;
using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Ops;

/// <summary>E-mail or invoice call retried until sent. Table <c>ops.outbox</c>.</summary>
public sealed class OutboxMessage : ITenantOwned, IHasTimestamps
{
    public long Id { get; set; }

    public Guid TenantId { get; set; }

    public OutboxKind Kind { get; set; }

    public required JsonDocument Payload { get; set; }

    public DateTimeOffset? SentAt { get; set; }

    public int Attempts { get; set; }

    public string? Error { get; set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset UpdatedAt { get; set; }
}
