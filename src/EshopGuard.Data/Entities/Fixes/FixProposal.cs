using System.Text.Json;
using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Fixes;

/// <summary>Proposed rewrite of a passage ("Zmena 1 z 5"). Table <c>fixes.fix_proposals</c>.</summary>
public sealed class FixProposal : TenantEntity
{
    public Guid ShopId { get; set; }

    public Guid PageId { get; set; }

    public Guid PageVersionId { get; set; }

    public Guid? GroupId { get; set; }

    public Guid[] FindingIds { get; set; } = [];

    public FixField Field { get; set; }

    public int? BlockIndex { get; set; }

    public required string OriginalText { get; set; }

    public required string ProposedText { get; set; }

    public JsonDocument? Alternatives { get; set; }

    public string? SelectedAlternative { get; set; }

    public string? EditedText { get; set; }

    public string? Reason { get; set; }

    public JsonDocument? Placeholders { get; set; }

    public RecheckStatus RecheckStatus { get; set; }

    /// <summary>
    /// What the last recheck found (change 11): <c>{ "text_hash", "checked_at", "jurisdictions": { "sk": "ok", "cz": "still_finding" },
    /// "rule_ids": [] }</c>; written only for the text it was computed for.
    /// </summary>
    public JsonDocument? RecheckResult { get; set; }

    public string? Model { get; set; }

    public string? PromptVersion { get; set; }

    public FixProposalStatus Status { get; set; }

    public Guid? DecidedBy { get; set; }

    public DateTimeOffset? DecidedAt { get; set; }

    public Guid? CreatedRunId { get; set; }

    /// <summary>Concurrency token (PostgreSQL <c>xmin</c>).</summary>
    public uint Version { get; set; }
}
