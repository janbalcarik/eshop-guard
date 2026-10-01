using EshopGuard.Core.Models;

namespace EshopGuard.Core.Extract;

/// <summary>
/// A block of text (paragraph, list item, table cell, heading) in reading order.
/// </summary>
/// <param name="Text">Cleaned text.</param>
/// <param name="HeadingLevel">1–6 for headings, 0 otherwise.</param>
internal sealed record TextBlock(string Text, int HeadingLevel = 0)
{
    public bool IsHeading => HeadingLevel > 0;
}

/// <summary>
/// A link found on a page.
/// </summary>
internal sealed record PageLink(Uri Url, string Text);

/// <summary>
/// Everything taken from one HTML page.
/// </summary>
internal sealed class ExtractedPage
{
    public string? Title { get; init; }

    public string? MetaDescription { get; init; }

    public string? OgType { get; init; }

    public bool HasProductJsonLd { get; init; }

    public string? JsonLdDescription { get; init; }

    /// <summary>
    /// Where the product category is read from: title, first h1, breadcrumb trail, microdata category and JSON-LD
    /// product name, category and breadcrumb, joined by " | ". Empty when the page has none of them.
    /// </summary>
    public string Category { get; init; } = "";

    /// <summary>Main text of the page.</summary>
    public IReadOnlyList<TextBlock> MainBlocks { get; init; } = [];

    /// <summary>Page frame (header, footer, aside outside the main content), one list per element.</summary>
    public IReadOnlyList<IReadOnlyList<TextBlock>> ChromeRegions { get; init; } = [];

    /// <summary>
    /// Other visible text: blocks that are neither in the main text nor in the frame nor navigation. SmartReader keeps what
    /// it takes for the article and may leave out product boxes, badges or parameter tables; they are checked from here.
    /// </summary>
    public IReadOnlyList<TextBlock> RestBlocks { get; init; } = [];

    /// <summary>Readable characters inside navigation (menus, category lists, breadcrumbs, filters), left out on purpose.</summary>
    public int NavigationChars { get; init; }

    /// <summary>Readable characters in tiles of other products (related products, listings), checked on their own pages.</summary>
    public int ListingChars { get; init; }

    public ExtractionMethod Method { get; init; }

    public IReadOnlyList<ImageInfo> Images { get; init; } = [];

    public IReadOnlyList<PageLink> Links { get; init; } = [];

    /// <summary>Readable text in the downloaded HTML and signs of a JavaScript application, see <see cref="RenderCheck"/>.</summary>
    public RenderCheck.Result Render { get; init; } = new(0, null);

    /// <summary>
    /// Blocks of the frame and the other text that the profile of the page template left out (cookie bar, forms, cart,
    /// filters, listings). They are not sent to Jev, but rules that look for information anywhere on the page still see them.
    /// </summary>
    public IReadOnlyList<TextBlock> ProfileSkippedBlocks { get; init; } = [];

    /// <summary>A copy with the frame and the other text left after a profile, and the blocks it left out.</summary>
    public ExtractedPage WithProfile(IReadOnlyList<IReadOnlyList<TextBlock>> chromeRegions, IReadOnlyList<TextBlock> restBlocks, IReadOnlyList<TextBlock> skipped) => new()
    {
        Title = Title,
        MetaDescription = MetaDescription,
        OgType = OgType,
        HasProductJsonLd = HasProductJsonLd,
        JsonLdDescription = JsonLdDescription,
        Category = Category,
        MainBlocks = MainBlocks,
        ChromeRegions = chromeRegions,
        RestBlocks = restBlocks,
        NavigationChars = NavigationChars,
        ListingChars = ListingChars,
        Method = Method,
        Images = Images,
        Links = Links,
        Render = Render,
        ProfileSkippedBlocks = skipped,
    };
}
