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

    /// <summary>How the group is fixed (change 11): replace with the template, remove the sentence, or own wording.</summary>
    public FixGroupMode Mode { get; set; }

    /// <summary>Own wording of the mode <c>custom</c>.</summary>
    public string? CustomText { get; set; }

    /// <summary>
    /// Where the fix fits, written by whoever builds the group (K rozhodnutí 1 of change 11): <c>{ "individual": [{ "page_id",
    /// "reason_code", "params" }] }</c>; those pages are left to be fixed one by one.
    /// </summary>
    public JsonDocument? Fit { get; set; }

    /// <summary>Recheck of the filled template or of the own wording; null when nothing waits for it.</summary>
    public RecheckStatus? RecheckStatus { get; set; }

    /// <summary>What the recheck found, as <see cref="FixProposal.RecheckResult"/>.</summary>
    public JsonDocument? RecheckResult { get; set; }

    public Guid? ApprovedBy { get; set; }

    public DateTimeOffset? ApprovedAt { get; set; }

    public DateTimeOffset? LockedAt { get; set; }

    /// <summary>Concurrency token (PostgreSQL <c>xmin</c>).</summary>
    public uint Version { get; set; }
}
