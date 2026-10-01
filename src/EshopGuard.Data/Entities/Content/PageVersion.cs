using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Content;

/// <summary>Version of a page; a new one only when the text changes (HASH partitions by shop). Table <c>content.page_versions</c>.</summary>
public sealed class PageVersion : ITenantOwned, IHasTimestamps
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid TenantId { get; set; }

    public Guid ShopId { get; set; }

    public Guid PageId { get; set; }

    public Guid? RunId { get; set; }

    public DateTimeOffset FetchedAt { get; set; }

    public int? HttpStatus { get; set; }

    public string? HtmlBlobKey { get; set; }

    public string? ExtractBlobKey { get; set; }

    public byte[]? TextHash { get; set; }

    public int? VisibleChars { get; set; }

    public int? CheckedChars { get; set; }

    public int? NavigationChars { get; set; }

    public int? ListingChars { get; set; }

    public int? ProfileSkippedChars { get; set; }

    public string? ExtractionMethod { get; set; }

    public string? ScriptApp { get; set; }

    public bool TextNotLoaded { get; set; }

    public long[] SegmentHashes { get; set; } = [];

    public bool IsCurrent { get; set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset UpdatedAt { get; set; }
}
