using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Checks;

/// <summary>Detected change of a page (monitoring, "Posledné zmeny"). Table <c>checks.page_changes</c>.</summary>
public sealed class PageChange : ITenantOwned, IHasCreatedAt
{
    public long Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid ShopId { get; set; }

    public Guid PageId { get; set; }

    public DateTimeOffset DetectedAt { get; set; }

    public PageChangeSource Source { get; set; }

    public PageChangeKind ChangeKind { get; set; }

    public Guid? RunId { get; set; }

    public PageChangeResult? Result { get; set; }

    public Guid[] FindingIds { get; set; } = [];

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }
}
