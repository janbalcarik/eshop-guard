using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Fixes;

/// <summary>Evidence for a claim (certificate, licence, …); valid for all e-shops of the tenant. Table <c>fixes.evidence_items</c>.</summary>
public sealed class EvidenceItem : TenantEntity, ISoftDeletable
{
    public required string ClaimText { get; set; }

    public EvidenceSubjectKind SubjectKind { get; set; }

    public string? SubjectLabel { get; set; }

    public EvidenceKind Kind { get; set; }

    public string? Title { get; set; }

    public string? FileBlobKey { get; set; }

    public string? FileName { get; set; }

    public EvidenceSource Source { get; set; }

    public string? RegistryRef { get; set; }

    public DateTimeOffset? ValidFrom { get; set; }

    public DateTimeOffset? ValidUntil { get; set; }

    public EvidenceStatus Status { get; set; }

    public DateTimeOffset? ReminderSentAt { get; set; }

    public Guid? CreatedBy { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>Concurrency token (PostgreSQL <c>xmin</c>).</summary>
    public uint Version { get; set; }
}
