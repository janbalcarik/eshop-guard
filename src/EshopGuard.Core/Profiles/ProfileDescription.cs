using AngleSharp.Dom;
using EshopGuard.Core.Extract;
using EshopGuard.Core.Languages;

namespace EshopGuard.Core.Profiles;

/// <summary>
/// The product description of a page by its profile: the text of the regions the profile model marked
/// <c>main_description</c> or <c>short_description</c>, without the regions of other roles inside them (reviews, related
/// products). The comparison of language versions compares descriptions; reviews and texts of the template that both versions
/// show would make a translated version look like a copy (goodie.sk, 2. 10. 2026).
/// </summary>
internal static class ProfileDescription
{
    /// <summary>Roles of the product description.</summary>
    public static readonly IReadOnlySet<string> Roles = new HashSet<string>(StringComparer.Ordinal) { "main_description", "short_description" };

    /// <summary>Sentences of the description, or null when the profile has no description region or it is empty on the page.</summary>
    public static IReadOnlyList<string>? Sentences(IDocument document, PageProfile profile)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(profile);
        if (document.Body is not { } body)
        {
            return null;
        }

        var regions = profile.ActiveRegions.ToList();
        var elements = regions.Where(r => Roles.Contains(r.Role)).SelectMany(r => ProfileMatcher.Select(body, r.Selector)).Distinct().ToList();
        var outermost = elements.Where(e => !elements.Any(o => !ReferenceEquals(o, e) && o.Contains(e))).ToList();
        if (outermost.Count == 0)
        {
            return null;
        }

        var removed = regions.Where(r => !Roles.Contains(r.Role)).SelectMany(r => ProfileMatcher.Select(body, r.Selector))
            .Where(e => outermost.Any(o => !ReferenceEquals(o, e) && o.Contains(e)))
            .ToHashSet();
        var text = string.Join('\n', outermost.SelectMany(e => HtmlText.ExtractBlocks(e, removed: removed)).Select(b => b.Text));
        var sentences = VersionComparer.Sentences(text);
        return sentences.Count == 0 ? null : sentences;
    }
}
