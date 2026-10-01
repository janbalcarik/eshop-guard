using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Shops;

/// <summary>E-shop of a tenant. Table <c>shop.shops</c>.</summary>
public sealed class Shop : TenantEntity, ISoftDeletable
{
    public required string Domain { get; set; }

    public required string BaseUrl { get; set; }

    public required string BasePath { get; set; }

    public string? Name { get; set; }

    public required string HomeCountry { get; set; }

    public string? Language { get; set; }

    public ShopPlatform Platform { get; set; }

    public ShopSourceMode SourceMode { get; set; }

    public ShopStatus Status { get; set; }

    public int? ProductCount { get; set; }

    public int? PageCount { get; set; }

    public string? TierCode { get; set; }

    public string[] Modules { get; set; } = [];

    public bool CheckHiddenOnSave { get; set; }

    public DateTimeOffset? OwnershipVerifiedAt { get; set; }

    public VerificationMethod? VerificationMethod { get; set; }

    public int? MonitorSlotMinute { get; set; }

    public Guid? LastFullRunId { get; set; }

    public DateTimeOffset? LastRunAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? DeletedAt { get; set; }
}
