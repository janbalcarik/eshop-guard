using EshopGuard.Core.Crawl;
using EshopGuard.Core.Extract;
using EshopGuard.Core.Pipeline;

namespace EshopGuard.Core.Markets;

/// <summary>
/// Reads the technical signs of markets and language versions from the one extraction of the home page (change 5) and the
/// sitemap, without a model. A switch to another version is recognized by the structure of the address of a link, never by
/// its text; no list of words or letters decides a language or a country.
/// </summary>
internal static partial class MarketSignalReader
{
    private static readonly string[] LanguageParameters = ["lang", "language", "locale"];

    public static MarketSignals Read(
        ExtractedPage home, IReadOnlyList<SitemapEntry> sitemap, Uri site, IReadOnlyList<string> translationSignatures, int maxHomeLinks,
        bool homeTextNotLoaded = false)
    {
        ArgumentNullException.ThrowIfNull(home);
        ArgumentNullException.ThrowIfNull(site);
        var hreflang = home.Alternates.Where(a => a.Language != "x-default")
            .Select(a => new HreflangSignal(a.Language, a.Url.AbsoluteUri, "page"))
            .ToList();
        foreach (var alternate in sitemap.SelectMany(e => e.Alternates).Where(a => a.Language != "x-default"))
        {
            if (!hreflang.Any(h => h.Language == alternate.Language))
            {
                hreflang.Add(new HreflangSignal(alternate.Language, alternate.Url.AbsoluteUri, "sitemap"));
            }
        }

        var host = site.Host;
        var dot = host.TrimEnd('.').LastIndexOf('.');
        var links = home.Links.Concat(home.FooterLinks).ToList();
        return new MarketSignals
        {
            Site = site.AbsoluteUri,
            HtmlLang = home.HtmlLang,
            Hreflang = hreflang,
            Currencies = home.Currencies,
            PhonePrefixes = home.PhoneNumbers.Select(PhonePrefixes.CallingCode).OfType<string>().Distinct().ToList(),
            Tld = dot >= 0 ? host[(dot + 1)..].ToLowerInvariant() : "",
            SwitcherCandidates = SwitcherCandidates(links.Select(l => l.Url), site),
            ScriptOnlySwitchElements = home.ScriptSwitchElements
                .Select(e => new ScriptSwitchSignal(e.Tag, e.Language.Length > 0 ? e.Language : null))
                .Distinct()
                .ToList(),
            BrowserTranslationWidgets = translationSignatures
                .Where(signature => home.ScriptSources.Any(src => src.Contains(signature, StringComparison.OrdinalIgnoreCase)))
                .ToList(),
            HomeLinks = home.Links.DistinctBy(l => l.Url.AbsoluteUri).Take(maxHomeLinks).Select(l => new LinkInfo(l.Text, l.Url.AbsoluteUri)).ToList(),
            FooterLinks = home.FooterLinks.DistinctBy(l => l.Url.AbsoluteUri).Select(l => new LinkInfo(l.Text, l.Url.AbsoluteUri)).ToList(),
            HomeTextNotLoaded = homeTextNotLoaded,
        };
    }

    /// <summary>Links whose address differs from the site only by a language segment, parameter, subdomain or top-level part.</summary>
    internal static List<SwitcherCandidate> SwitcherCandidates(IEnumerable<Uri> targets, Uri site)
    {
        var found = new List<SwitcherCandidate>();
        var home = UrlTools.Normalize(site);
        foreach (var target in targets.Select(UrlTools.Normalize).DistinctBy(u => u.AbsoluteUri))
        {
            if (target.AbsoluteUri == home.AbsoluteUri)
            {
                continue;
            }

            var candidate = Candidate(target, home);
            if (candidate is not null && !found.Any(c => c.Url == candidate.Url))
            {
                found.Add(candidate);
            }
        }

        return found;
    }

    private static SwitcherCandidate? Candidate(Uri target, Uri site)
    {
        var segments = target.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var root = segments.Length == 0;
        if (UrlTools.IsSameSite(target, site))
        {
            if (segments.Length == 1 && SwitchLabel().IsMatch(segments[0]))
            {
                return new SwitcherCandidate(target.AbsoluteUri, "path", LanguageTags.Normalize(segments[0]));
            }

            var language = LanguageParameter(target);
            return language is not null && root ? new SwitcherCandidate(target.AbsoluteUri, "query", language) : null;
        }

        if (!root || target.Query.Length > 1)
        {
            return null;
        }

        if (UrlTools.IsSubdomainOf(target.Host, site.Host))
        {
            var label = target.Host[..target.Host.IndexOf('.')];
            return SwitchLabel().IsMatch(label) ? new SwitcherCandidate(target.AbsoluteUri, "subdomain", LanguageTags.Normalize(label)) : null;
        }

        var siteHost = UrlTools.StripWww(site.Host);
        var targetHost = UrlTools.StripWww(target.Host);
        return FirstLabel(siteHost) == FirstLabel(targetHost) && !string.Equals(siteHost, targetHost, StringComparison.OrdinalIgnoreCase)
            ? new SwitcherCandidate(target.AbsoluteUri, "domain", null)
            : null;
    }

    private static string? LanguageParameter(Uri url)
    {
        foreach (var pair in url.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Split('=', 2)))
        {
            if (pair.Length == 2 && LanguageParameters.Contains(pair[0], StringComparer.OrdinalIgnoreCase) && LanguageTags.IsLanguageTag(pair[1]))
            {
                return LanguageTags.Normalize(pair[1]);
            }
        }

        return null;
    }

    /// <summary>A language segment of a path or a subdomain: two letters and an optional region (<c>sk</c>, <c>en-gb</c>), as in the design.</summary>
    [System.Text.RegularExpressions.GeneratedRegex("^[a-z]{2}(-[a-z]{2})?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex SwitchLabel();

    private static string FirstLabel(string host)
    {
        var dot = host.IndexOf('.');
        return (dot > 0 ? host[..dot] : host).ToLowerInvariant();
    }
}
