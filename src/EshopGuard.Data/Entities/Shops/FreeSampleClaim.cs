using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Shops;

/// <summary>Free sample used for a domain (once per domain across tenants). Table <c>shop.free_sample_claims</c>.</summary>
public sealed class FreeSampleClaim : IHasCreatedAt
{
    public required string Domain { get; set; }

    public Guid TenantId { get; set; }

    public Guid ShopId { get; set; }

    public DateTimeOffset ClaimedAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }
}
