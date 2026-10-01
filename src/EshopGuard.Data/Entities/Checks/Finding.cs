using System.Text.Json;
using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Checks;

/// <summary>Finding of a rule in an e-shop; verdicts per jurisdiction in verdicts. Table <c>checks.findings</c>.</summary>
public sealed class Finding : TenantEntity
{
    public Guid ShopId { get; set; }

    public required string RuleId { get; set; }

    public Guid RuleSetId { get; set; }

    public required string Module { get; set; }

    public Checkability Checkability { get; set; }

    public required string Severity { get; set; }

    public FindingBand Band { get; set; }

    public FindingScope Scope { get; set; }

    public long? SegmentHash { get; set; }

    public Guid? PageId { get; set; }

    public string? Text { get; set; }

    public float? Score { get; set; }

    public required JsonDocument Verdicts { get; set; }

    public JsonDocument? LegalRefs { get; set; }

    public JsonDocument? Params { get; set; }

    public FindingStatus Status { get; set; }

    public int Occurrences { get; set; }

    public Guid? FirstRunId { get; set; }

    public Guid? LastSeenRunId { get; set; }

    public Guid? ResolvedRunId { get; set; }

    public DateTimeOffset? ResolvedAt { get; set; }
}
