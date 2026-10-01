using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Fixes;

/// <summary>Remembered decision about a text: the same text gets the same fix without an LLM. Table <c>fixes.decision_memory</c>.</summary>
public sealed class DecisionMemory : TenantEntity
{
    public Guid? ShopId { get; set; }

    public long SegmentHash { get; set; }

    public required string NormalizedText { get; set; }

    public Decision Decision { get; set; }

    public string? ReplacementText { get; set; }

    public Guid? EvidenceId { get; set; }

    public Guid? SourceProposalId { get; set; }

    public bool AutoPublish { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTimeOffset? SupersededAt { get; set; }
}
