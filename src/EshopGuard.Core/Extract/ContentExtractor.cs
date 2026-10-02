using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using EshopGuard.Core.Crawl;
using EshopGuard.Core.Models;
using Microsoft.Extensions.Logging;

namespace EshopGuard.Core.Extract;

/// <summary>Takes the text blocks and everything else of one parsed page (tests replace it).</summary>
internal interface IPageExtractor
{
    ExtractedPage Extract(Uri url, ParsedPage page);
}

/// <summary>
/// Extracts the main text (SmartReader, fallback heuristic), the page frame, title, meta description,
/// JSON-LD, images and links from an HTML page.
/// </summary>
internal sealed partial class ContentExtractor(ILogger<ContentExtractor> logger) : IPageExtractor
{
    private const string ChromeSelector =
        "header, footer, aside, [role=banner], [role=contentinfo], [role=complementary]";

    private const string FallbackRemovedSelector =
        "script, style, noscript, template, svg, nav, [role=navigation]";

    private static readonly string[] BreadcrumbClasses = ["breadcrumb", "breadcrumbs"];

    /// <summary>
    /// SmartReader removes class attributes, but badges and breadcrumb trails are recognized by them;
    /// "page" is SmartReader's own default.
    /// </summary>
    private static readonly string[] ClassesKeptBySmartReader = ["page", .. BreadcrumbClasses, .. HtmlText.BadgeClassNames];

    /// <summary>
    /// Navigation and interface: nav, ARIA navigation and menu roles, breadcrumb trails, lists whose every item is only a link
    /// (category trees, brand lists, footer links) and labels of checkboxes and radio buttons (product filters).
    /// The links themselves are read separately.
    /// </summary>
    internal static bool IsNavigation(IElement element) => IsNavigation(element, null);

    /// <summary>
    /// <see cref="IsNavigation(IElement)"/> as it would decide in a copy of the document without <paramref name="removed"/>
    /// (their text and their links do not count).
    /// </summary>
    internal static bool IsNavigation(IElement element, IReadOnlySet<IElement>? removed)
    {
        if (element.LocalName == "nav" || IsLinkList(element, removed) || IsOptionLabel(element, removed))
        {
            return true;
        }

        if (element.GetAttribute("role") is "navigation" or "menu" or "menubar" or "menuitem")
        {
            return true;
        }

        return IsBreadcrumb(element);
    }

    /// <summary><see cref="IsNavigation(IElement, IReadOnlySet{IElement}?)"/> as a predicate for <see cref="HtmlText.ExtractBlocks"/>.</summary>
    internal static Func<IElement, bool> Navigation(IReadOnlySet<IElement>? removed) =>
        removed is null || removed.Count == 0 ? IsNavigation : e => IsNavigation(e, removed);

    private static bool IsBreadcrumb(IElement element) =>
        (element.GetAttribute("itemtype") ?? "").Contains("BreadcrumbList", StringComparison.OrdinalIgnoreCase)
        || (element.GetAttribute("aria-label") ?? "").Contains("breadcrumb", StringComparison.OrdinalIgnoreCase)
        || element.ClassList.Any(c => BreadcrumbClasses.Contains(c, StringComparer.OrdinalIgnoreCase));

    /// <summary>
    /// Title, first h1, breadcrumb trail, microdata category and JSON-LD hints: the product category is read from these,
    /// never from the sentence itself. Read before the fallback heuristic removes parts of the document.
    /// </summary>
    private static string ReadCategory(IDocument document, string title, JsonLdReader.Result jsonLd)
    {
        var parts = new List<string> { title };
        if (document.QuerySelector("h1") is { } heading)
        {
            parts.Add(TextTools.Clean(heading.TextContent));
        }

        var breadcrumbs = document.All.Where(IsBreadcrumb).ToList();
        foreach (var breadcrumb in breadcrumbs.Where(b => !breadcrumbs.Any(other => other != b && other.Contains(b))))
        {
            parts.Add(string.Join(" / ", HtmlText.ExtractBlocks(breadcrumb).Select(b => b.Text)));
        }

        foreach (var category in document.QuerySelectorAll("[itemprop=category]"))
        {
            parts.Add(TextTools.Clean(category.GetAttribute("content") ?? category.TextContent));
        }

        parts.AddRange(jsonLd.CategoryHints);
        return string.Join(" | ", parts.Where(p => p.Length > 0).Distinct());
    }

    private static bool IsOptionLabel(IElement element, IReadOnlySet<IElement>? removed)
    {
        if (element.LocalName != "label")
        {
            return false;
        }

        var input = element.QuerySelectorAll("input").FirstOrDefault(i => !HtmlText.IsRemoved(i, removed, element))
            ?? (element.GetAttribute("for") is { Length: > 0 } id ? ElementById(element.Owner, id, removed) : null);
        return input?.GetAttribute("type")?.ToLowerInvariant() is "checkbox" or "radio";
    }

    /// <summary>The first element with the id that is not removed (what <c>GetElementById</c> finds in a copy without them).</summary>
    private static IElement? ElementById(IDocument? document, string id, IReadOnlySet<IElement>? removed)
    {
        var found = document?.GetElementById(id);
        if (found is null || !HtmlText.IsRemoved(found, removed))
        {
            return found;
        }

        return document!.All.FirstOrDefault(e => e.Id == id && !HtmlText.IsRemoved(e, removed));
    }

    private static bool IsLinkList(IElement element, IReadOnlySet<IElement>? removed)
    {
        if (element.LocalName is not ("ul" or "ol"))
        {
            return false;
        }

        var items = element.Children.Where(c => c.LocalName == "li" && removed?.Contains(c) != true).ToList();
        return items.Count >= 2 && items.All(i => IsOnlyLinks(i, removed));
    }

    /// <summary>Nearly all text of the item is inside links (nested link lists included).</summary>
    private static bool IsOnlyLinks(IElement item, IReadOnlySet<IElement>? removed)
    {
        var text = TextTools.Clean(HtmlText.TextContent(item, removed));
        if (text.Length == 0)
        {
            return true;
        }

        var links = item.QuerySelectorAll("a").Where(a => !HtmlText.IsRemoved(a, removed, item));
        var linkText = TextTools.Clean(string.Join(" ", links.Select(a => HtmlText.TextContent(a, removed))));
        return linkText.Length >= text.Length * 0.9;
    }

    /// <summary>Extraction of a page given as text (parsed once here).</summary>
    public ExtractedPage Extract(Uri url, string html) => Extract(url, ParsedPage.Parse(url, html));

    /// <summary>Extraction of a parsed page; the document is only read, never changed.</summary>
    public ExtractedPage Extract(Uri url, ParsedPage page)
    {
        var document = page.Document;
        var baseUri = GetBaseUri(document, url);
        var jsonLd = JsonLdReader.Read(document);
        var render = RenderCheck.Inspect(document);

        var chromeElements = document.QuerySelectorAll(ChromeSelector)
            .Where(e => !HasAncestor(e, ChromeSelector) && !HasAncestor(e, "main, article"))
            .ToList();
        // Menus and breadcrumbs are navigation, not claims; the links themselves are read separately below.
        var chromeRegions = chromeElements
            .Select(e => (IReadOnlyList<TextBlock>)HtmlText.ExtractBlocks(e, IsNavigation))
            .Where(blocks => blocks.Count > 0)
            .ToList();

        var images = ReadImages(document, baseUri);
        var links = ReadLinks(document, baseUri);
        var title = TextTools.Clean(document.Title);
        var metaDescription = TextTools.Clean(GetMeta(document, "name", "description"));
        var ogType = GetMeta(document, "property", "og:type");
        var category = ReadCategory(document, title, jsonLd);

        var (mainBlocks, method) = ExtractMain(url, document, chromeElements);

        // A block that is also in the frame belongs to the frame; otherwise the footer would be evaluated twice.
        var chromeTexts = chromeRegions.SelectMany(r => r).Select(b => TextTools.NormalizeForHash(b.Text)).ToHashSet();
        mainBlocks = mainBlocks.Where(b => !chromeTexts.Contains(TextTools.NormalizeForHash(b.Text))).ToList();
        var remainder = ReadRemainder(url, document, mainBlocks, chromeTexts);
        // SmartReader may keep a "related products" row in the main text; its longer texts belong to the other products.
        // Short blocks stay, so that a badge such as "Eco" shown on this product and on a related one is kept.
        mainBlocks = mainBlocks
            .Where(b => b.Text.Length < MinContainedChars || !remainder.ListingTexts.Contains(TextTools.NormalizeForHash(b.Text)))
            .ToList();

        var allBlocks = mainBlocks.Concat(chromeRegions.SelectMany(r => r)).Concat(remainder.Rest);
        return new ExtractedPage
        {
            HtmlLang = ReadHtmlLang(document),
            Alternates = ReadAlternates(document, baseUri),
            Currencies = jsonLd.Currencies.Concat(ReadMicrodataCurrencies(document)).Distinct().ToList(),
            ProductIds = jsonLd.ProductIds,
            PhoneNumbers = ReadPhoneNumbers(document, allBlocks),
            FooterLinks = ReadFooterLinks(document, baseUri),
            ScriptSources = ReadScriptSources(document, baseUri),
            ScriptSwitchElements = ReadScriptSwitchElements(document),
            Title = title.Length > 0 ? title : null,
            MetaDescription = metaDescription.Length > 0 ? metaDescription : null,
            OgType = ogType,
            HasProductJsonLd = jsonLd.HasProduct,
            HasProductMicrodata = HasSingleProductMicrodata(document),
            JsonLdDescription = jsonLd.ProductDescription,
            Category = category,
            MainBlocks = mainBlocks,
            ChromeRegions = chromeRegions,
            Method = method,
            Images = images,
            Links = links,
            Render = render,
            RestBlocks = remainder.Rest,
            NavigationChars = remainder.NavigationChars,
            ListingChars = remainder.ListingChars,
        };
    }

    internal sealed record Remainder(List<TextBlock> Rest, int NavigationChars, int ListingChars, HashSet<string> ListingTexts);

    /// <summary>
    /// Everything visible that is neither main text nor frame nor navigation nor a listing of other products, read the way
    /// the fallback heuristic reads the page. A block already in the main text is left out:
    /// exactly, or as a part of a longer main block when it has at least <see cref="MinContainedChars"/> characters
    /// (SmartReader may join blocks), so that a short badge such as "Eco" is never dropped because the word occurs
    /// somewhere in the main text.
    /// </summary>
    internal static Remainder ReadRemainder(Uri url, IDocument document, IReadOnlyList<TextBlock> mainBlocks, HashSet<string> chromeTexts)
    {
        var body = document.Body;
        if (body is null)
        {
            return new Remainder([], 0, 0, []);
        }

        var navigationChars = CountNavigation(body);
        var tiles = new List<IElement>();
        CollectTiles(body, url, tiles);
        var listingChars = tiles.Sum(RenderCheck.CountVisible);
        var listingTexts = tiles.SelectMany(t => HtmlText.ExtractBlocks(t, IsNavigation))
            .Select(b => TextTools.NormalizeForHash(b.Text))
            .ToHashSet();

        // Left out, not removed: the frame, scripts and navigation, and the tiles of other products.
        var frame = document.QuerySelectorAll(ChromeSelector)
            .Where(e => !HasAncestor(e, ChromeSelector) && !HasAncestor(e, "main, article"));
        var removed = frame.Concat(body.QuerySelectorAll(FallbackRemovedSelector)).Concat(tiles).ToHashSet();

        var main = mainBlocks.Select(b => TextTools.NormalizeForHash(b.Text)).ToList();
        var mainSet = main.ToHashSet();
        var mainJoined = string.Join("\n", main);
        var rest = HtmlText.ExtractBlocks(body, Navigation(removed), removed)
            .Where(b =>
            {
                var text = TextTools.NormalizeForHash(b.Text);
                return text.Length > 0
                    && !mainSet.Contains(text)
                    && !chromeTexts.Contains(text)
                    && !(text.Length >= MinContainedChars && mainJoined.Contains(text, StringComparison.Ordinal));
            })
            .ToList();
        return new Remainder(rest, navigationChars, listingChars, listingTexts);
    }

    internal const int MinContainedChars = 30;

    /// <summary>Fewest tiles that make a listing.</summary>
    private const int MinTiles = 3;

    /// <summary>A tile is short: name, price, a badge, maybe one line of description.</summary>
    private const int MaxTileChars = 600;

    /// <summary>A price: a number with a currency before or after it.</summary>
    [GeneratedRegex(@"(\d[\d\s.,]*\s?(€|eur\b|kč|czk|zł|pln|ft\b|huf|lei\b|ron\b))|((€|eur\b|kč|czk|zł|pln|huf)\s?\d)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PriceRegex();

    /// <summary>
    /// Tiles of other products ("related products", "you may also like", product listings): recognized by structure, never
    /// by the heading or wording, which differ from shop to shop. A parent with at least <see cref="MinTiles"/> children of
    /// the same element type, most of them short, each with a price and a link to another page of the same site. Their
    /// texts are the other products' texts and are checked on those products' own pages. Navigation is not searched.
    /// </summary>
    internal static void CollectTiles(IElement element, Uri page, List<IElement> tiles)
    {
        if (HtmlText.IsSkipped(element.LocalName) || IsNavigation(element))
        {
            return;
        }

        var found = new HashSet<IElement>();
        foreach (var group in element.Children.Where(c => !HtmlText.IsSkipped(c.LocalName)).GroupBy(c => c.LocalName))
        {
            var members = group.ToList();
            if (members.Count < MinTiles)
            {
                continue;
            }

            var productTiles = members.Where(m => IsProductTile(m, page)).ToList();
            if (productTiles.Count >= MinTiles && productTiles.Count * 10 >= members.Count * 6)
            {
                found.UnionWith(productTiles);
            }
        }

        tiles.AddRange(found);
        foreach (var child in element.Children.Where(c => !found.Contains(c)))
        {
            CollectTiles(child, page, tiles);
        }
    }

    private static bool IsProductTile(IElement tile, Uri page)
    {
        var text = TextTools.Clean(tile.TextContent);
        if (text.Length == 0 || RenderCheck.CountVisible(tile) > MaxTileChars || !PriceRegex().IsMatch(text))
        {
            return false;
        }

        // The tile itself may be the link (<a class="product" href="..."> with name and price inside).
        var links = tile.LocalName == "a" ? tile.QuerySelectorAll("a[href]").Prepend(tile) : tile.QuerySelectorAll("a[href]");
        return links.Any(a => LinksElsewhere(a.GetAttribute("href"), page));
    }

    /// <summary>A link to another page of the same site (host compared without "www.").</summary>
    private static bool LinksElsewhere(string? href, Uri page)
    {
        var target = UrlTools.TryResolve(href, page);
        if (target is null || target.Scheme is not ("http" or "https"))
        {
            return false;
        }

        static string Host(Uri u) => u.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? u.Host[4..] : u.Host;
        return string.Equals(Host(target), Host(page), StringComparison.OrdinalIgnoreCase)
            && !string.Equals(target.AbsolutePath.TrimEnd('/'), page.AbsolutePath.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The outermost navigation elements, in one pass from the top.</summary>
    internal static void CollectNavigation(IElement element, List<IElement> found)
    {
        foreach (var child in element.Children)
        {
            if (HtmlText.IsSkipped(child.LocalName))
            {
                continue;
            }

            if (IsNavigation(child))
            {
                found.Add(child);
            }
            else
            {
                CollectNavigation(child, found);
            }
        }
    }

    /// <summary>Readable characters of the outermost navigation elements, in one pass from the top.</summary>
    private static int CountNavigation(IElement element)
    {
        var count = 0;
        foreach (var child in element.Children)
        {
            if (HtmlText.IsSkipped(child.LocalName))
            {
                continue;
            }

            count += IsNavigation(child) ? RenderCheck.CountVisible(child) : CountNavigation(child);
        }

        return count;
    }

    private (List<TextBlock> Blocks, ExtractionMethod Method) ExtractMain(
        Uri url, IDocument document, List<IElement> chromeElements)
    {
        try
        {
            // Never Reader.ParseArticle(url, html): that overload downloads the page itself. SmartReader changes the document,
            // so it reads a copy of the one parse of the page.
            var reader = new SmartReader.Reader(url.AbsoluteUri, (AngleSharp.Html.Dom.IHtmlDocument)document.Clone(deep: true)) { ClassesToPreserve = ClassesKeptBySmartReader };
            var article = reader.GetArticle();
            if (article.IsReadable && !string.IsNullOrWhiteSpace(article.Content))
            {
                var articleDocument = new HtmlParser().ParseDocument(article.Content);
                var blocks = HtmlText.ExtractBlocks(articleDocument.Body, IsNavigation);
                if (blocks.Count > 0)
                {
                    return (blocks, ExtractionMethod.Readability);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SmartReader failed for {Url}, using fallback", url);
        }

        var body = document.Body;
        if (body is null)
        {
            return ([], ExtractionMethod.Fallback);
        }

        // Left out, not removed, so the document stays whole for the other readers.
        var removed = chromeElements.Concat(body.QuerySelectorAll(FallbackRemovedSelector)).ToHashSet();
        return (HtmlText.ExtractBlocks(body, Navigation(removed), removed), ExtractionMethod.Fallback);
    }

    private static bool HasAncestor(IElement element, string selector)
    {
        for (var parent = element.ParentElement; parent is not null; parent = parent.ParentElement)
        {
            if (parent.Matches(selector))
            {
                return true;
            }
        }

        return false;
    }

    private static Uri GetBaseUri(IDocument document, Uri url)
    {
        var href = document.QuerySelector("base[href]")?.GetAttribute("href");
        return UrlTools.TryResolve(href, url) ?? url;
    }

    private static string? GetMeta(IDocument document, string attribute, string value)
    {
        foreach (var meta in document.QuerySelectorAll("meta"))
        {
            if (string.Equals(meta.GetAttribute(attribute)?.Trim(), value, StringComparison.OrdinalIgnoreCase))
            {
                return meta.GetAttribute("content")?.Trim();
            }
        }

        return null;
    }

    private static List<ImageInfo> ReadImages(IDocument document, Uri baseUri)
    {
        var images = new List<ImageInfo>();
        foreach (var image in document.QuerySelectorAll("img"))
        {
            var src = UrlTools.TryResolve(image.GetAttribute("src") ?? image.GetAttribute("data-src"), baseUri);
            if (src is null)
            {
                continue;
            }

            var alt = TextTools.Clean(image.GetAttribute("alt"));
            images.Add(new ImageInfo { Src = src.AbsoluteUri, FileName = UrlTools.FileName(src), Alt = alt.Length > 0 ? alt : null });
        }

        return images;
    }

    /// <summary><c>lang</c> of the <c>html</c> element, normalized; null when missing or not a language tag.</summary>
    internal static string? ReadHtmlLang(IDocument document)
    {
        var lang = Markets.LanguageTags.Normalize(document.DocumentElement?.GetAttribute("lang") ?? document.DocumentElement?.GetAttribute("xml:lang"));
        return Markets.LanguageTags.IsLanguageTag(lang) ? lang : null;
    }

    /// <summary>Alternates from <c>link rel="alternate" hreflang</c>, language normalized (<c>x-default</c> kept).</summary>
    private static List<PageAlternate> ReadAlternates(IDocument document, Uri baseUri)
    {
        var alternates = new List<PageAlternate>();
        foreach (var link in document.QuerySelectorAll("link[hreflang][href]"))
        {
            var rel = (link.GetAttribute("rel") ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var language = Markets.LanguageTags.Normalize(link.GetAttribute("hreflang"));
            var url = UrlTools.TryResolve(link.GetAttribute("href"), baseUri);
            if (rel.Contains("alternate", StringComparer.OrdinalIgnoreCase) && url is not null
                && (language == "x-default" || Markets.LanguageTags.IsLanguageTag(language)))
            {
                alternates.Add(new PageAlternate(language, UrlTools.Normalize(url)));
            }
        }

        return alternates.Distinct().ToList();
    }

    /// <summary>
    /// Exactly one item of the schema.org type Product in microdata that is not inside another one: the product of a detail page.
    /// A listing marks every tile as a Product, so more than one is not a product page.
    /// </summary>
    internal static bool HasSingleProductMicrodata(IDocument document)
    {
        var products = document.QuerySelectorAll("[itemtype]").Where(e => IsProductType(e.GetAttribute("itemtype"))).ToList();
        return products.Count(p => !products.Any(o => !ReferenceEquals(o, p) && o.Contains(p))) == 1;
    }

    private static bool IsProductType(string? itemtype) =>
        (itemtype ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Any(t => t.TrimEnd('/').EndsWith("schema.org/Product", StringComparison.OrdinalIgnoreCase));

    /// <summary>Currencies of microdata (<c>itemprop=priceCurrency</c>) and of the product meta tags (Open Graph).</summary>
    private static IEnumerable<string> ReadMicrodataCurrencies(IDocument document)
    {
        var values = document.QuerySelectorAll("[itemprop=priceCurrency]").Select(e => e.GetAttribute("content") ?? e.TextContent)
            .Concat(document.QuerySelectorAll("meta[property]")
                .Where(m => (m.GetAttribute("property") ?? "").Trim().ToLowerInvariant() is "product:price:currency" or "og:price:currency")
                .Select(m => m.GetAttribute("content")));
        return values.Select(v => (v ?? "").Trim().ToUpperInvariant()).Where(v => v.Length == 3 && v.All(char.IsAsciiLetterUpper));
    }

    /// <summary>
    /// Phone numbers with an international prefix: <c>tel:</c> links and numbers in the text that start with <c>+</c> or
    /// <c>00</c> (the shape of the number, never words around it). Returned as <c>+</c> and 8 to 15 digits.
    /// </summary>
    private static List<string> ReadPhoneNumbers(IDocument document, IEnumerable<TextBlock> blocks)
    {
        var numbers = new List<string>();
        foreach (var link in document.QuerySelectorAll("a[href]"))
        {
            var href = (link.GetAttribute("href") ?? "").Trim();
            if (href.StartsWith("tel:", StringComparison.OrdinalIgnoreCase) && International(Uri.UnescapeDataString(href[4..])) is { } number)
            {
                numbers.Add(number);
            }
        }

        foreach (var block in blocks)
        {
            foreach (Match match in InternationalPhone().Matches(block.Text))
            {
                if (International(match.Value) is { } number)
                {
                    numbers.Add(number);
                }
            }
        }

        return numbers.Distinct().ToList();

        static string? International(string text)
        {
            var trimmed = text.Trim();
            var international = trimmed.StartsWith('+') || trimmed.StartsWith("00", StringComparison.Ordinal);
            var digits = new string(trimmed.Where(char.IsAsciiDigit).ToArray());
            if (trimmed.StartsWith("00", StringComparison.Ordinal))
            {
                digits = digits[2..];
            }

            return international && digits.Length is >= 8 and <= 15 ? "+" + digits : null;
        }
    }

    /// <summary>A number that starts with + or 00 and has digits separated by spaces, hyphens, slashes, dots or brackets.</summary>
    [GeneratedRegex(@"(?<![\w+])(?:\+|00)\d[\d \u00A0\-/.()]{6,20}\d")]
    private static partial Regex InternationalPhone();

    /// <summary>Links inside the outermost footers (<c>footer</c>, <c>[role=contentinfo]</c>).</summary>
    private static List<PageLink> ReadFooterLinks(IDocument document, Uri baseUri)
    {
        const string footer = "footer, [role=contentinfo]";
        var links = new List<PageLink>();
        foreach (var element in document.QuerySelectorAll(footer).Where(e => !HasAncestor(e, footer)))
        {
            foreach (var anchor in element.QuerySelectorAll("a[href]"))
            {
                if (UrlTools.TryResolve(anchor.GetAttribute("href"), baseUri) is { } target)
                {
                    links.Add(new PageLink(target, TextTools.Clean(anchor.TextContent)));
                }
            }
        }

        return links;
    }

    private static List<string> ReadScriptSources(IDocument document, Uri baseUri) =>
        document.QuerySelectorAll("script[src]")
            .Select(s => UrlTools.TryResolve(s.GetAttribute("src"), baseUri)?.AbsoluteUri)
            .OfType<string>()
            .Distinct()
            .ToList();

    /// <summary>
    /// Elements that switch the language only by script: an attribute naming a language (<c>data-lang</c>,
    /// <c>data-language</c>, <c>data-locale</c>), or an <c>onclick</c> handler that sets a language or locale; a link with an
    /// http address is a link, not a script switch.
    /// </summary>
    private static List<ScriptSwitchElement> ReadScriptSwitchElements(IDocument document)
    {
        var found = new List<ScriptSwitchElement>();
        foreach (var element in document.Body?.QuerySelectorAll("*") ?? Enumerable.Empty<IElement>())
        {
            if (element.LocalName == "a" && UrlTools.TryResolve(element.GetAttribute("href"), new Uri("http://localhost/")) is not null
                && !(element.GetAttribute("href") ?? "").TrimStart().StartsWith('#'))
            {
                continue;
            }

            var language = new[] { "data-lang", "data-language", "data-locale" }
                .Select(a => Markets.LanguageTags.Normalize(element.GetAttribute(a)))
                .FirstOrDefault(Markets.LanguageTags.IsLanguageTag);
            if (language is not null)
            {
                found.Add(new ScriptSwitchElement(element.LocalName, language));
            }
            else if (LanguageHandler().IsMatch(element.GetAttribute("onclick") ?? ""))
            {
                found.Add(new ScriptSwitchElement(element.LocalName, ""));
            }
        }

        return found;
    }

    /// <summary>A script handler that sets a language or locale (a technical sign of the switch, not a word of the text).</summary>
    [GeneratedRegex(@"\b(lang|language|locale)\b", RegexOptions.IgnoreCase)]
    private static partial Regex LanguageHandler();

    private static List<PageLink> ReadLinks(IDocument document, Uri baseUri)
    {
        var links = new List<PageLink>();
        foreach (var anchor in document.QuerySelectorAll("a[href]"))
        {
            var target = UrlTools.TryResolve(anchor.GetAttribute("href"), baseUri);
            if (target is not null)
            {
                links.Add(new PageLink(target, TextTools.Clean(anchor.TextContent)));
            }
        }

        return links;
    }
}
