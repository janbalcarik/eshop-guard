namespace EshopGuard.Core.Models;

/// <summary>
/// Type of a page, decided by URL, title and structured data.
/// </summary>
public enum PageType
{
    /// <summary>The home page of the site.</summary>
    Home,

    /// <summary>A product detail page (JSON-LD <c>Product</c>, one microdata <c>Product</c> or <c>og:type=product</c>).</summary>
    Product,

    /// <summary>A legal page such as terms and conditions or the complaints procedure.</summary>
    Legal,

    /// <summary>Any other page.</summary>
    Content,
}

/// <summary>
/// How the main text of a page was extracted.
/// </summary>
public enum ExtractionMethod
{
    /// <summary>SmartReader (port of Mozilla Readability).</summary>
    Readability,

    /// <summary>Own heuristic over the page body without script, style, nav, header, footer and aside.</summary>
    Fallback,
}

/// <summary>
/// A downloaded page.
/// </summary>
public sealed class PageInfo
{
    /// <summary>Final URL of the page after redirects.</summary>
    public required string Url { get; init; }

    /// <summary>Page type.</summary>
    public PageType Type { get; init; }

    /// <summary>Content of the <c>title</c> element.</summary>
    public string? Title { get; init; }

    /// <summary>Content of the meta description.</summary>
    public string? MetaDescription { get; init; }

    /// <summary>Product description from JSON-LD.</summary>
    public string? JsonLdDescription { get; init; }

    /// <summary>How the main text was extracted.</summary>
    public ExtractionMethod Extraction { get; init; }

    /// <summary>False when the page is a product page over the sample limit and was left out of the analysis.</summary>
    public bool IncludedInAnalysis { get; set; } = true;

    /// <summary>Images with their alt texts and file names.</summary>
    public IReadOnlyList<ImageInfo> Images { get; init; } = [];

    /// <summary>Number of sentence segments taken from the page (before deduplication).</summary>
    public int SentenceCount { get; set; }

    /// <summary>Number of legal paragraphs taken from the page (before deduplication).</summary>
    public int ParagraphCount { get; set; }

    /// <summary>Main text of the page as extracted, one block (paragraph, list item, heading) per line.</summary>
    public string MainText { get; init; } = "";

    /// <summary>
    /// Title, first h1, breadcrumb trail, microdata category and JSON-LD product name, category and breadcrumb, joined
    /// by " | ": where rules that depend on the product category (point 15 list) look for it.
    /// </summary>
    public string Category { get; init; } = "";

    /// <summary>Characters of readable text in the downloaded HTML (scripts, styles and templates left out).</summary>
    public int VisibleTextChars { get; init; }

    /// <summary>
    /// Sign that the site renders the page with JavaScript: a framework name (Next.js, Nuxt, Gatsby, Angular, React, Remix)
    /// or <c>noscript_message</c>, <c>app_state</c>, <c>empty_app_root</c>; null when there is none.
    /// </summary>
    public string? ScriptApp { get; init; }

    /// <summary>
    /// Other visible text outside the main text, the frame and the navigation, one block per line, for example a product
    /// box with badges, price and delivery that SmartReader left out of the main text. It is checked like the main text.
    /// </summary>
    public string RestText { get; set; } = "";

    /// <summary>Readable characters in navigation (menus, category lists, breadcrumbs, filters), left out on purpose.</summary>
    public int NavigationTextChars { get; init; }

    /// <summary>Readable characters in tiles of other products (related products, listings); checked on those products' own pages.</summary>
    public int ListingTextChars { get; init; }

    /// <summary>Readable characters that were checked: main text, page frame and other text.</summary>
    public int CheckedTextChars { get; set; }

    /// <summary>Profile of the page template the page used (<c>vegis.sk#1</c>), or null when no profile fit the page.</summary>
    public string? ProfileId { get; set; }

    /// <summary>
    /// Share of the visible text outside every region of the best stored profile; above <c>profiles.max_unknown_share</c>
    /// the page fits no profile. Null when there was no profile to compare with.
    /// </summary>
    public double? ProfileUnknownShare { get; set; }

    /// <summary>Readable characters the profile left out (cookie bar, forms, cart, filters, listings).</summary>
    public int ProfileSkippedTextChars { get; set; }

    /// <summary>Blocks the profile left out, one per line, so that what was not sent to Jev can be checked.</summary>
    public string ProfileSkippedText { get; set; } = "";

    /// <summary>Visible characters that were neither checked, navigation, tiles of other products nor left out by the profile; near zero unless the extraction lost text.</summary>
    public int UncheckedTextChars => Math.Max(0, VisibleTextChars - NavigationTextChars - ListingTextChars - CheckedTextChars - ProfileSkippedTextChars);

    /// <summary>Share of visible text above which unchecked text is reported.</summary>
    public const double UncheckedReportShare = 0.2;

    /// <summary>True when at least <see cref="UncheckedReportShare"/> of the visible text was neither checked, navigation nor tiles of other products.</summary>
    public bool HasUncheckedText => VisibleTextChars > 0 && UncheckedTextChars >= VisibleTextChars * UncheckedReportShare;

    /// <summary>
    /// True when the downloaded HTML had less readable text than the crawl minimum, typically because JavaScript renders it.
    /// Only the title, meta description and JSON-LD of such a page were checked; no findings do not mean the page is fine.
    /// </summary>
    public bool TextNotLoaded { get; init; }

    /// <summary>
    /// Language of the page: the language of the shop version it was crawled in (change 7), otherwise its <c>html lang</c>;
    /// null when neither is known. Goes to <c>content.pages.language</c>.
    /// </summary>
    public string? Language { get; init; }

    /// <summary>
    /// Key of the group of alternates (<c>hreflang</c>): the first 16 characters of SHA-256 of the sorted URLs of the
    /// alternates. Two pages of the same product in two language versions have the same key; null without alternates.
    /// </summary>
    public string? HreflangGroup { get; init; }

    /// <summary>Identifiers of the product from its structured data (EAN/GTIN, SKU, MPN, productID), for pairing versions.</summary>
    public IReadOnlyList<ProductIdentifier> ProductIds { get; init; } = [];
}

/// <summary>An identifier of a product from structured data: kind (<c>gtin13</c>, <c>sku</c>, <c>mpn</c>, <c>productID</c>) and value.</summary>
public sealed record ProductIdentifier(string Kind, string Value);

/// <summary>
/// An image on a page. Jev does not see images; alt texts and file names are only flagged for manual review.
/// </summary>
public sealed class ImageInfo
{
    /// <summary>Absolute image URL.</summary>
    public required string Src { get; init; }

    /// <summary>File name from the URL.</summary>
    public required string FileName { get; init; }

    /// <summary>Alt text, if any.</summary>
    public string? Alt { get; init; }
}

/// <summary>
/// A legal document the tool found but did not read, typically a PDF with terms and conditions.
/// </summary>
public sealed class UncheckedDocument
{
    /// <summary>Document URL.</summary>
    public required string Url { get; init; }

    /// <summary>Text of the link pointing to the document, if found in a link.</summary>
    public string? LinkText { get; init; }

    /// <summary>Page where the link was found, or the sitemap.</summary>
    public required string FoundOn { get; init; }
}
