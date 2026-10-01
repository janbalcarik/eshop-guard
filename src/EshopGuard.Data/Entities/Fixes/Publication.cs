using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Fixes;

/// <summary>Publication of approved text into the e-shop through a connector (can be rolled back). Table <c>fixes.publications</c>.</summary>
public sealed class Publication : TenantEntity
{
    public Guid ShopId { get; set; }

    public Guid ConnectorId { get; set; }

    public Guid PageId { get; set; }

    public string? ExternalId { get; set; }

    public required string Field { get; set; }

    public string? Language { get; set; }

    public string? OldValue { get; set; }

    public byte[]? OldValueHash { get; set; }

    public required string NewValue { get; set; }

    public required string IdempotencyKey { get; set; }

    public PublicationStatus Status { get; set; }

    public int Attempts { get; set; }

    public string? Error { get; set; }

    public Guid? RequestedBy { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    public DateTimeOffset? RolledBackAt { get; set; }

    public Guid[] FixProposalIds { get; set; } = [];
}
