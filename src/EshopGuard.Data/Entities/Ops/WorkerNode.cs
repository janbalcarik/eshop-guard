using System.Text.Json;
using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Ops;

/// <summary>Running worker instance (id host:pid). Table <c>ops.workers</c>.</summary>
public sealed class WorkerNode : IHasTimestamps
{
    public required string Id { get; set; }

    public string? Version { get; set; }

    public JsonDocument? Slots { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset HeartbeatAt { get; set; }

    public bool Draining { get; set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset UpdatedAt { get; set; }
}
