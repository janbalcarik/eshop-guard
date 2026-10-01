using System.Text.Json;
using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Fixes;

/// <summary>Bulk fix of a repeated text, template or site obligation. Table <c>fixes.fix_groups</c>.</summary>
public sealed class FixGroup : TenantEntity
{
    public Guid ShopId { get; set; }

    public FixGroupKind Kind { get; set; }

    public long? SegmentHash { get; set; }

    public string? OriginalText { get; set; }

    public string? ReplacementTemplate { get; set; }

    public JsonDocument? Placeholders { get; set; }

    public JsonDocument? FilledValues { get; set; }

    public FixGroupStatus Status { get; set; }

    public int PageCount { get; set; }

    public Guid[] ExcludedPageIds { get; set; } = [];

    public Guid? ApprovedBy { get; set; }

    public DateTimeOffset? ApprovedAt { get; set; }

    public DateTimeOffset? LockedAt { get; set; }

    /// <summary>Concurrency token (PostgreSQL <c>xmin</c>).</summary>
    public uint Version { get; set; }
}
