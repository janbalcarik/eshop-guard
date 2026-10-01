using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Shops;

/// <summary>Fact about the tenant's e-shops for fix proposals (shop_id null = all e-shops). Table <c>shop.shop_facts</c>.</summary>
public sealed class ShopFact : TenantEntity
{
    public Guid? ShopId { get; set; }

    public required string Topic { get; set; }

    public required string Text { get; set; }

    public Guid? CreatedBy { get; set; }
}
