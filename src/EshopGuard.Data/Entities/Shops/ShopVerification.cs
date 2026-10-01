using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Shops;

/// <summary>Ownership verification of an e-shop. Table <c>shop.shop_verifications</c>.</summary>
public sealed class ShopVerification : TenantEntity
{
    public Guid ShopId { get; set; }

    public VerificationMethod Method { get; set; }

    public required string Token { get; set; }

    public required string Status { get; set; }

    public DateTimeOffset? CheckedAt { get; set; }
}
