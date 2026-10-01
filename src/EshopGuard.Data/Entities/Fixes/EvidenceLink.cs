using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Fixes;

/// <summary>Where a piece of evidence applies ("19 produktov"). Table <c>fixes.evidence_links</c>.</summary>
public sealed class EvidenceLink : ITenantOwned, IHasCreatedAt
{
    /// <summary>Identifier (<c>uuid</c> v7).</summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Owning tenant.</summary>
    public Guid TenantId { get; set; }

    public Guid EvidenceId { get; set; }

    public Guid ShopId { get; set; }

    public Guid? PageId { get; set; }

    public string? ExternalId { get; set; }

    public Guid? FindingId { get; set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }
}
