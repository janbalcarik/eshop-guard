using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Shops;

/// <summary>Product feed of an e-shop. Table <c>shop.feeds</c>.</summary>
public sealed class Feed : TenantEntity
{
    public Guid ShopId { get; set; }

    public required string Url { get; set; }

    public FeedFormat Format { get; set; }

    public string? Etag { get; set; }

    public DateTimeOffset? LastFetchedAt { get; set; }
}
