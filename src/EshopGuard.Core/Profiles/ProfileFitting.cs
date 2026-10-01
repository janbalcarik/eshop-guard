using AngleSharp.Dom;
using EshopGuard.Core.Pipeline;

namespace EshopGuard.Core.Profiles;

/// <summary>
/// Applies the best of the given profiles to a page: the one that leaves the least of its visible text outside the known
/// regions, when that is at most <c>profiles.max_unknown_share</c>. The page then leaves out the text of the skip regions
/// and remembers what it left out.
/// </summary>
internal static class ProfileFitting
{
    /// <summary>
    /// Fits the page (it must not use a profile yet) with the parsed document of the page; returns the skip regions that
    /// were not used, or null when no profile fits.
    /// </summary>
    public static IReadOnlyList<ProfileMatcher.Blocked>? Fit(ExtractedPageRecord page, IDocument document, IReadOnlyList<PageProfile> profiles, double maxUnknownShare)
    {
        if (profiles.Count == 0 || page.Fit is not null || document.Body is not { } body)
        {
            return null;
        }

        var info = page.Info;
        var known = ProfileMatcher.KnownByStructure(body, new Uri(info.Url));
        var (best, share) = profiles
            .Select(p => (Profile: p, Share: ProfileMatcher.UnknownShare(body, p, known)))
            .MinBy(x => x.Share);
        info.ProfileUnknownShare = Math.Min(share, info.ProfileUnknownShare ?? 1);
        if (share > maxUnknownShare)
        {
            return null;
        }

        var application = ProfileMatcher.ApplyNonDestructive(document, page.Content, best);
        var content = application.Content;
        info.ProfileId = best.Id;
        info.ProfileSkippedTextChars = content.ProfileSkippedBlocks.Sum(b => b.Text.Length);
        info.ProfileSkippedText = string.Join('\n', content.ProfileSkippedBlocks.Select(b => b.Text));
        info.RestText = string.Join('\n', content.RestBlocks.Select(b => b.Text));
        info.CheckedTextChars = content.MainBlocks.Concat(content.ChromeRegions.SelectMany(r => r)).Concat(content.RestBlocks).Sum(b => b.Text.Length);
        page.Content = content;
        page.Fit = new ProfileFit(best.Id, application.SkippedCharsByRole);
        return application.Blocked;
    }

    /// <summary>
    /// How every profile was used, in the order the scan applied them: stored profiles by their first page, then the new
    /// ones in the order they were written.
    /// </summary>
    public static Dictionary<string, ProfileUse> Uses(IReadOnlyList<ExtractedPageRecord> pages, IReadOnlyList<PageProfile> stored, IReadOnlyList<PageProfile> created)
    {
        var uses = new Dictionary<string, ProfileUse>();
        var storedById = stored.GroupBy(p => p.Id).ToDictionary(g => g.Key, g => g.First());
        void Add(ExtractedPageRecord page, PageProfile profile, bool isNew)
        {
            var use = uses.TryGetValue(profile.Id, out var existing)
                ? existing
                : uses[profile.Id] = new ProfileUse { Profile = profile, CreatedInThisScan = isNew };
            use.Pages++;
            foreach (var (role, chars) in page.Fit!.SkippedCharsByRole)
            {
                use.SkippedCharsByRole[role] = use.SkippedCharsByRole.GetValueOrDefault(role) + chars;
            }
        }

        foreach (var page in pages)
        {
            if (page.Fit is { } fit && storedById.TryGetValue(fit.ProfileId, out var profile) && !created.Any(c => c.Id == fit.ProfileId))
            {
                Add(page, profile, isNew: false);
            }
        }

        foreach (var profile in created)
        {
            foreach (var page in pages.Where(p => p.Fit?.ProfileId == profile.Id))
            {
                Add(page, profile, isNew: true);
            }
        }

        return uses;
    }
}
