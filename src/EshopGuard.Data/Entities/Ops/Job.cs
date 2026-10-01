using System.Text.Json;
using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Ops;

/// <summary>Job in the queue (SKIP LOCKED with leases; change 4). Table <c>ops.jobs</c>.</summary>
public sealed class Job : IHasTimestamps
{
    public long Id { get; set; }

    public Guid? TenantId { get; set; }

    public Guid? ShopId { get; set; }

    public Guid? RunId { get; set; }

    public required string Kind { get; set; }

    public JobResourceClass ResourceClass { get; set; }

    public short Priority { get; set; }

    public required JsonDocument Payload { get; set; }

    public JobState State { get; set; }

    public string? DedupeKey { get; set; }

    public string? ConcurrencyKey { get; set; }

    public int Attempts { get; set; }

    public int MaxAttempts { get; set; }

    public DateTimeOffset NotBefore { get; set; }

    public string? LeaseOwner { get; set; }

    public DateTimeOffset? LeaseUntil { get; set; }

    public DateTimeOffset? HeartbeatAt { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    public string? LastError { get; set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset UpdatedAt { get; set; }
}
