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

/// <summary>An alternate of the page from <c>link rel="alternate" hreflang</c>: language (normalized, or <c>x-default</c>) and URL.</summary>
internal sealed record PageAlternate(string Language, Uri Url);

/// <summary>
/// An element that switches the language only by script: no link with an address, but an attribute naming a language
/// (<c>data-lang</c>, <c>data-language</c>, <c>data-locale</c>) or an <c>onclick</c> handler that sets one.
/// </summary>
/// <param name="Tag">Element name.</param>
/// <param name="Language">Language from the attribute (normalized), or empty when only the handler shows it.</param>
internal sealed record ScriptSwitchElement(string Tag, string Language);

/// <summary>
/// Everything taken from one HTML page.
/// </summary>
internal sealed class ExtractedPage
{
    public string? Title { get; init; }

    public string? MetaDescription { get; init; }

    public string? OgType { get; init; }

    public bool HasProductJsonLd { get; init; }

    /// <summary>Exactly one schema.org Product in microdata (<c>itemtype</c>), not a listing of several.</summary>
    public bool HasProductMicrodata { get; init; }

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

    /// <summary><c>lang</c> of the <c>html</c> element, normalized (<c>sk-sk</c>); null when missing.</summary>
    public string? HtmlLang { get; init; }

    /// <summary>Alternates of the page (<c>link rel="alternate" hreflang</c>).</summary>
    public IReadOnlyList<PageAlternate> Alternates { get; init; } = [];

    /// <summary>Currencies of prices in the structured data (JSON-LD, microdata, meta of the product), ISO 4217.</summary>
    public IReadOnlyList<string> Currencies { get; init; } = [];

    /// <summary>Identifiers of the product from JSON-LD (EAN/GTIN, SKU, MPN, productID).</summary>
    public IReadOnlyList<ProductIdentifier> ProductIds { get; init; } = [];

    /// <summary>Phone numbers with an international prefix (<c>tel:</c> links and numbers in the text), as <c>+</c> and digits.</summary>
    public IReadOnlyList<string> PhoneNumbers { get; init; } = [];

    /// <summary>Links inside the footer (<c>footer</c>, <c>[role=contentinfo]</c>).</summary>
    public IReadOnlyList<PageLink> FooterLinks { get; init; } = [];

    /// <summary>Absolute addresses of the scripts of the page (<c>script src</c>).</summary>
    public IReadOnlyList<string> ScriptSources { get; init; } = [];

    /// <summary>Elements that switch the language only by script.</summary>
    public IReadOnlyList<ScriptSwitchElement> ScriptSwitchElements { get; init; } = [];

    /// <summary>A copy with the frame and the other text left after a profile, and the blocks it left out.</summary>
    public ExtractedPage WithProfile(IReadOnlyList<IReadOnlyList<TextBlock>> chromeRegions, IReadOnlyList<TextBlock> restBlocks, IReadOnlyList<TextBlock> skipped) => new()
    {
        Title = Title,
        MetaDescription = MetaDescription,
        OgType = OgType,
        HasProductJsonLd = HasProductJsonLd,
        HasProductMicrodata = HasProductMicrodata,
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
        HtmlLang = HtmlLang,
        Alternates = Alternates,
        Currencies = Currencies,
        ProductIds = ProductIds,
        PhoneNumbers = PhoneNumbers,
        FooterLinks = FooterLinks,
        ScriptSources = ScriptSources,
        ScriptSwitchElements = ScriptSwitchElements,
    };
}
