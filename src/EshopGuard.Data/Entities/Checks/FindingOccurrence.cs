using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Checks;

/// <summary>Page on which a finding occurs. Table <c>checks.finding_occurrences</c>.</summary>
public sealed class FindingOccurrence : ITenantOwned, IHasCreatedAt
{
    public Guid TenantId { get; set; }

    public Guid FindingId { get; set; }

    public Guid PageId { get; set; }

    public Guid ShopId { get; set; }

    public int? BlockIndex { get; set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }
}
