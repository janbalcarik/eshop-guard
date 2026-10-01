using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Ops;

/// <summary>Planned recurring work of an e-shop (PK shop and kind). Table <c>ops.schedules</c>.</summary>
public sealed class Schedule : ITenantOwned, IHasTimestamps
{
    public Guid TenantId { get; set; }

    public Guid ShopId { get; set; }

    public ScheduleKind Kind { get; set; }

    public DateTimeOffset NextRunAt { get; set; }

    public DateTimeOffset? LastRunAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset UpdatedAt { get; set; }
}
