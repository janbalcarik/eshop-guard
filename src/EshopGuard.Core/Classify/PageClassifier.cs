using EshopGuard.Core.Extract;
using EshopGuard.Core.Models;
using EshopGuard.Core.Options;
using Microsoft.Extensions.Options;

namespace EshopGuard.Core.Classify;

/// <summary>
/// Decides the page type by heuristics: legal pages by URL and title, product pages by JSON-LD or og:type.
/// </summary>
internal sealed class PageClassifier
{
    private readonly IReadOnlyList<string> _legalSlugs;

    public PageClassifier(IOptions<EshopGuardOptions> options)
    {
        _legalSlugs = options.Value.Crawl.LegalPageSlugs
            .Select(TextTools.Slugify)
            .Where(s => s.Length > 0)
            .ToList();
    }

    public bool IsLegalUrl(Uri url) => ContainsLegalSlug(Uri.UnescapeDataString(url.AbsolutePath));

    public bool IsLegalText(string? text) => ContainsLegalSlug(text);

    public PageType Classify(Uri url, ExtractedPage page, bool isHome)
    {
        if (isHome)
        {
            return PageType.Home;
        }

        if (page.HasProductJsonLd || (page.OgType?.StartsWith("product", StringComparison.OrdinalIgnoreCase) ?? false))
        {
            return PageType.Product;
        }

        return IsLegalUrl(url) || IsLegalText(page.Title) ? PageType.Legal : PageType.Content;
    }

    private bool ContainsLegalSlug(string? text)
    {
        var slug = TextTools.Slugify(text);
        return slug.Length > 0 && _legalSlugs.Any(s => slug.Contains(s, StringComparison.Ordinal));
    }
}
