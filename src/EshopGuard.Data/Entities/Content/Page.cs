using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Content;

/// <summary>Page of an e-shop (HASH partitions by shop). Table <c>content.pages</c>.</summary>
public sealed class Page : ITenantOwned, IHasTimestamps
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid TenantId { get; set; }

    public Guid ShopId { get; set; }

    public required string Url { get; set; }

    public long UrlHash { get; set; }

    public string? Path { get; set; }

    public string? Language { get; set; }

    public string? HreflangGroup { get; set; }

    public PageType? PageType { get; set; }

    public PageSource Source { get; set; }

    public string? ExternalId { get; set; }

    public string? Title { get; set; }

    public PageStatus Status { get; set; }

    public bool IsHiddenInShop { get; set; }

    public DateTimeOffset FirstSeenAt { get; set; }

    public DateTimeOffset? LastSeenAt { get; set; }

    public DateTimeOffset? LastFetchedAt { get; set; }

    public string? HttpEtag { get; set; }

    public string? HttpLastModified { get; set; }

    public Guid? CurrentVersionId { get; set; }

    public Guid? ProfileId { get; set; }

    public float? ProfileUnknownShare { get; set; }

    public short RotationBucket { get; set; }

    public DateTimeOffset? NextCheckAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset UpdatedAt { get; set; }
}
