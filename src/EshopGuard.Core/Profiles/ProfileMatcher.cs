using System.Text.RegularExpressions;
using AngleSharp.Dom;
using EshopGuard.Core.Extract;

namespace EshopGuard.Core.Profiles;

/// <summary>
/// Matches pages and profiles without any model: how alike two pages are in structure, how much of a page a profile
/// leaves outside its regions, and which text a profile leaves out of the check.
/// </summary>
internal static partial class ProfileMatcher
{
    /// <summary>Element names, element and class names and element and id names (numbers replaced) in the body.</summary>
    public static HashSet<string> StructureTokens(IElement body)
    {
        var tokens = new HashSet<string>(StringComparer.Ordinal);
        Collect(body, tokens);
        return tokens;
    }

    private static void Collect(IElement element, HashSet<string> tokens)
    {
        foreach (var child in element.Children)
        {
            if (HtmlText.IsSkipped(child.LocalName))
            {
                continue;
            }

            tokens.Add(child.LocalName);
            foreach (var name in child.ClassList)
            {
                tokens.Add(child.LocalName + "." + Digits().Replace(name, "#"));
            }

            if (child.Id is { Length: > 0 } id)
            {
                tokens.Add(child.LocalName + "#" + Digits().Replace(id, "#"));
            }

            Collect(child, tokens);
        }
    }

    /// <summary>Jaccard index of the structure tokens: shared tokens divided by all tokens.</summary>
    public static double Similarity(IReadOnlySet<string> a, IReadOnlySet<string> b)
    {
        if (a.Count == 0 && b.Count == 0)
        {
            return 1;
        }

        var shared = a.Count(b.Contains);
        return (double)shared / (a.Count + b.Count - shared);
    }

    /// <summary>
    /// Elements the extractor recognizes by structure whatever the profile: navigation and tiles of other products. A page
    /// with an extra "related products" row that the sample pages did not have still has the template of the profile
    /// (naturfyt.sk on 1. 10. 2026: such a row left 13.8 % of a product page outside the profile).
    /// </summary>
    public static List<IElement> KnownByStructure(IElement body, Uri page)
    {
        var known = new List<IElement>();
        ContentExtractor.CollectNavigation(body, known);
        ContentExtractor.CollectTiles(body, page, known);
        return known;
    }

    /// <summary>
    /// Share of the visible text of the page that lies outside every region of the profile, checked or skipped, and outside
    /// <paramref name="knownByStructure"/>. Near zero when the page has the template of the profile; near one for a page of
    /// another template. It decides only whether the profile applies; text outside its regions is checked anyway.
    /// </summary>
    public static double UnknownShare(IElement body, PageProfile profile, IReadOnlyList<IElement>? knownByStructure = null)
    {
        var total = RenderCheck.CountVisible(body);
        if (total == 0)
        {
            return 1;
        }

        var matched = profile.ActiveRegions.SelectMany(r => Select(body, r.Selector)).Concat(knownByStructure ?? []);
        var known = Outermost(matched).Sum(RenderCheck.CountVisible);
        return Math.Max(0, total - known) / (double)total;
    }

    /// <summary>Elements matching the selector; none when the selector is invalid or not supported.</summary>
    public static IEnumerable<IElement> Select(IElement root, string selector)
    {
        try
        {
            return root.QuerySelectorAll(selector).ToList();
        }
        catch (DomException)
        {
            return [];
        }
    }

    /// <summary>True when the selector can be used at all.</summary>
    public static bool IsValidSelector(IElement root, string selector)
    {
        try
        {
            _ = root.QuerySelector(selector);
            return true;
        }
        catch (DomException)
        {
            return false;
        }
    }

    private static List<IElement> Outermost(IEnumerable<IElement> elements)
    {
        var set = elements.ToHashSet();
        return set.Where(e => !HasAncestorIn(e, set)).ToList();
    }

    private static bool HasAncestorIn(IElement element, HashSet<IElement> set)
    {
        for (var parent = element.ParentElement; parent is not null; parent = parent.ParentElement)
        {
            if (set.Contains(parent))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Why a skip region was not used on a page.</summary>
    internal enum BlockReason
    {
        /// <summary>It contains a region that is checked.</summary>
        ContainsCheckedRegion,

        /// <summary>It contains a longer block of the main text.</summary>
        ContainsMainText,
    }

    /// <summary>A skip region not used on a page.</summary>
    internal sealed record Blocked(string Selector, BlockReason Reason)
    {
        public override string ToString() => Selector + (Reason == BlockReason.ContainsCheckedRegion ? ": obsahuje oblast ke kontrole" : ": obsahuje hlavní text stránky");
    }

    /// <param name="Content">The page with the skipped blocks moved from the frame and the other text to <see cref="ExtractedPage.ProfileSkippedBlocks"/>.</param>
    /// <param name="SkippedCharsByRole">Characters left out, by role of the skip region.</param>
    /// <param name="Blocked">Skip regions not used on this page and why.</param>
    internal sealed record Application(ExtractedPage Content, Dictionary<string, int> SkippedCharsByRole, List<Blocked> Blocked);

    /// <summary>
    /// Leaves out of the check the blocks of the frame and the other text that lie in skip regions of the profile. The main
    /// text is never touched. A skip region is not used on the page when it contains a check region or a longer block of the
    /// main text (a selector that matches something else on this page), and a block also shown outside the skip regions
    /// (a badge on the product and on a related tile) stays.
    /// </summary>
    /// <param name="document">A fresh parse of the page; it is changed.</param>
    /// <param name="content">What the extractor took from the page.</param>
    /// <param name="profile">The profile the page fits.</param>
    public static Application Apply(IDocument document, ExtractedPage content, PageProfile profile)
    {
        var body = document.Body;
        if (body is null)
        {
            return new Application(content, [], []);
        }

        var regions = profile.ActiveRegions.ToList();
        var checkElements = regions.Where(r => r.Action == ProfileRegion.Check).SelectMany(r => Select(body, r.Selector)).ToHashSet();
        var mainSet = content.MainBlocks.Select(b => TextTools.NormalizeForHash(b.Text)).ToHashSet();
        var mainJoined = string.Join("\n", mainSet);
        var skipped = new Dictionary<string, string>(StringComparer.Ordinal);
        var blocked = new List<Blocked>();
        var remove = new List<IElement>();
        foreach (var region in regions.Where(r => r.Action == ProfileRegion.Skip))
        {
            foreach (var element in Select(body, region.Selector))
            {
                if (checkElements.Any(c => c == element || element.Contains(c)))
                {
                    blocked.Add(new Blocked(region.Selector, BlockReason.ContainsCheckedRegion));
                    continue;
                }

                var texts = HtmlText.ExtractBlocks(element, ContentExtractor.IsNavigation).Select(b => TextTools.NormalizeForHash(b.Text)).ToList();
                if (texts.Any(t => t.Length >= ContentExtractor.MinContainedChars && (mainSet.Contains(t) || mainJoined.Contains(t, StringComparison.Ordinal))))
                {
                    blocked.Add(new Blocked(region.Selector, BlockReason.ContainsMainText));
                    continue;
                }

                foreach (var text in texts)
                {
                    skipped.TryAdd(text, region.Role);
                }

                remove.Add(element);
            }
        }

        if (remove.Count == 0)
        {
            return new Application(content, [], blocked);
        }

        foreach (var element in remove)
        {
            element.Remove();
        }

        var kept = HtmlText.ExtractBlocks(body, ContentExtractor.IsNavigation).Select(b => TextTools.NormalizeForHash(b.Text)).ToHashSet();
        var byRole = new Dictionary<string, int>(StringComparer.Ordinal);
        var skippedBlocks = new List<TextBlock>();
        bool Keep(TextBlock block)
        {
            var text = TextTools.NormalizeForHash(block.Text);
            if (!skipped.TryGetValue(text, out var role) || kept.Contains(text))
            {
                return true;
            }

            skippedBlocks.Add(block);
            byRole[role] = byRole.GetValueOrDefault(role) + block.Text.Length;
            return false;
        }

        var chrome = content.ChromeRegions
            .Select(r => (IReadOnlyList<TextBlock>)r.Where(Keep).ToList())
            .Where(r => r.Count > 0)
            .ToList();
        var rest = content.RestBlocks.Where(Keep).ToList();
        return new Application(content.WithProfile(chrome, rest, skippedBlocks), byRole, blocked);
    }

    [GeneratedRegex(@"\d+")]
    private static partial Regex Digits();
}
