using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Shops;

/// <summary>Ownership verification of an e-shop. Table <c>shop.shop_verifications</c>.</summary>
public sealed class ShopVerification : TenantEntity
{
    public Guid ShopId { get; set; }

    public VerificationMethod Method { get; set; }

    public required string Token { get; set; }

    public ShopVerificationStatus Status { get; set; }

    /// <summary>Why the check failed (<c>meta_not_found</c>, <c>dns_record_not_found</c>, <c>token_mismatch</c>, <c>fetch_failed</c>).</summary>
    public string? FailureCode { get; set; }

    public DateTimeOffset? CheckedAt { get; set; }
}
