using EshopGuard.Core.Crawl;
using EshopGuard.Core.Markets;

namespace EshopGuard.Core.Languages;

/// <summary>
/// Finds the language versions of a shop without a download (change 7, design section 4): <c>hreflang</c> of the home page and
/// the sitemap, links that switch by the structure of their address, the languages of the connector, and only when none of
/// them names another version, the versions the model named. A version on another domain than the shop or its subdomains waits
/// for a confirmation by the client; a version in the language of a market the tool does not check is unsupported.
/// </summary>
public static class LanguageVersionFinder
{
    public static IReadOnlyList<LanguageVersionCandidate> Find(
        MarketSignals signals, Uri site, MarketCatalog catalog, IReadOnlyList<VersionAnswer>? modelVersions = null,
        IReadOnlyList<ConnectorLanguage>? connector = null)
    {
        ArgumentNullException.ThrowIfNull(signals);
        ArgumentNullException.ThrowIfNull(catalog);
        var home = UrlTools.Normalize(site);
        var mainLanguage = signals.HtmlLang
            ?? signals.Hreflang.FirstOrDefault(h => SameAddress(h.Url, home))?.Language;
        var found = new List<LanguageVersionCandidate>
        {
            new()
            {
                Language = mainLanguage, BaseUrl = home.AbsoluteUri, SwitchMethod = SwitchMethods.Path, Source = VersionSources.Main, IsMain = true,
                Evidence = mainLanguage is null ? [] : [$"html_lang={mainLanguage}"],
            },
        };

        foreach (var alternate in signals.Hreflang)
        {
            if (Uri.TryCreate(alternate.Url, UriKind.Absolute, out var url) && Root(url, home, alternate.Source == "sitemap") is { } root)
            {
                Add(found, alternate.Language, root.Url, root.Method, VersionSources.Hreflang, $"hreflang={alternate.Language}");
            }
        }

        foreach (var switcher in signals.SwitcherCandidates)
        {
            var method = switcher.Kind switch
            {
                "path" => SwitchMethods.Path,
                "query" => SwitchMethods.Query,
                "subdomain" => SwitchMethods.Subdomain,
                "cookie" => SwitchMethods.Cookie,
                _ => SwitchMethods.Domain,
            };
            var language = switcher.Language ?? (method == SwitchMethods.Domain ? catalog.ByHost(new Uri(switcher.Url).Host)?.Language : null);

            // A switch of a platform (Shoptet) sets a cookie and keeps the address: the version lives on the home page.
            Add(found, language, method == SwitchMethods.Cookie ? home.AbsoluteUri : switcher.Url, method, VersionSources.Switcher, $"switcher={switcher.Url}");
        }

        foreach (var language in connector ?? [])
        {
            var url = language.BaseUrl is { } given && Uri.TryCreate(given, UriKind.Absolute, out var parsed) ? UrlTools.Normalize(parsed).AbsoluteUri : home.AbsoluteUri;
            Add(found, language.Language, url, url == home.AbsoluteUri ? SwitchMethods.Unknown : Method(new Uri(url), home), VersionSources.Connector, $"connector={language.Language}");
        }

        if (found.Count == 1)
        {
            foreach (var version in modelVersions ?? [])
            {
                var url = Uri.TryCreate(version.Url, UriKind.Absolute, out var parsed) ? UrlTools.Normalize(parsed) : home;
                var method = url.AbsoluteUri == home.AbsoluteUri ? SwitchMethods.Unknown : Method(url, home);
                Add(found, version.Language, url.AbsoluteUri, method, VersionSources.Llm, $"model={version.Evidence?.Quote}");
            }
        }

        foreach (var element in signals.ScriptOnlySwitchElements.Where(e => e.Language is not null))
        {
            if (!found.Any(v => LanguageTags.SamePrimary(v.Language, element.Language)))
            {
                Add(found, element.Language, home.AbsoluteUri, SwitchMethods.Unknown, VersionSources.Switcher, $"script_switch={element.Language}");
            }
        }

        return found.Select(v => v with { Status = Status(v, home, catalog) }).ToList();
    }

    /// <summary>
    /// The state after the probe: a version found without a language (a switcher to a domain of a market the catalog does not
    /// know, bonami.it) gets it from its page; the language of no supported market makes it unsupported, so it is not sampled,
    /// not offered for a confirmation and takes no part of the sample (bonami.sk, 2. 10. 2026: bg, et and it took 3 of 4 shares).
    /// </summary>
    public static IReadOnlyList<LanguageVersionCandidate> AfterProbe(IReadOnlyList<LanguageVersionCandidate> versions, MarketCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(versions);
        ArgumentNullException.ThrowIfNull(catalog);
        return versions.Select(v => !v.IsMain && v.Status != VersionStatus.Unsupported && v.Language is { } language && !catalog.IsSupportedLanguage(language)
                ? v with { Status = VersionStatus.Unsupported, Scope = null }
                : v)
            .ToList();
    }

    /// <summary>The state before the probe: unsupported language, another domain to confirm, otherwise active.</summary>
    private static string Status(LanguageVersionCandidate version, Uri home, MarketCatalog catalog)
    {
        if (version.IsMain)
        {
            return VersionStatus.Active;
        }

        if (version.Language is { } language && !catalog.IsSupportedLanguage(language))
        {
            return VersionStatus.Unsupported;
        }

        var host = new Uri(version.BaseUrl).Host;
        return UrlTools.IsSameSite(new Uri(version.BaseUrl), home) || UrlTools.IsSubdomainOf(host, home.Host)
            ? VersionStatus.Active
            : VersionStatus.NeedsConfirmation;
    }

    private static void Add(List<LanguageVersionCandidate> found, string? language, string baseUrl, string method, string source, string evidence)
    {
        var normalized = language is null ? null : LanguageTags.Normalize(language);
        var main = found[0];
        if (baseUrl == main.BaseUrl && (normalized is null || main.Language is null || LanguageTags.SamePrimary(normalized, main.Language)))
        {
            // The site itself: its language when the page does not declare one.
            found[0] = main with { Language = main.Language ?? normalized, Evidence = [.. main.Evidence, evidence] };
            return;
        }

        // One address with one language is one version: hreflang cs-CZ and a switcher to the same domain (cs by its TLD) are
        // the same version (goodie.cz, 2. 10. 2026); the language first found is kept.
        var index = found.FindIndex(v => !v.IsMain && v.BaseUrl == baseUrl
            && (normalized is null || v.Language is null || LanguageTags.SamePrimary(v.Language, normalized)));
        if (index >= 0)
        {
            found[index] = found[index] with { Language = found[index].Language ?? normalized, Evidence = [.. found[index].Evidence, evidence] };
            return;
        }

        found.Add(new LanguageVersionCandidate { Language = normalized, BaseUrl = baseUrl, SwitchMethod = method, Source = source, Evidence = [evidence] });
    }

    /// <summary>
    /// The root of the version an alternate belongs to: the root of another host, a language segment of the path, a language
    /// parameter, or the site itself (a version without an address of its own). An alternate of a deeper page (sitemap) gives
    /// a root only through its host or its first language segment.
    /// </summary>
    private static (string Url, string Method)? Root(Uri url, Uri home, bool deeperPage)
    {
        var normalized = UrlTools.Normalize(url);
        if (!UrlTools.IsSameSite(normalized, home))
        {
            var root = new UriBuilder(normalized) { Path = "/", Query = "" }.Uri;
            return (root.AbsoluteUri, UrlTools.IsSubdomainOf(normalized.Host, home.Host) ? SwitchMethods.Subdomain : SwitchMethods.Domain);
        }

        var segments = normalized.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length > 0 && segments[0].Length is 2 or 5 && LanguageTags.IsLanguageTag(segments[0]) && (deeperPage || segments.Length == 1))
        {
            return (new Uri(home, "/" + segments[0] + "/").AbsoluteUri, SwitchMethods.Path);
        }

        if (normalized.Query.Length > 1 && !deeperPage)
        {
            return (normalized.AbsoluteUri, SwitchMethods.Query);
        }

        return deeperPage ? null : (home.AbsoluteUri, SwitchMethods.Unknown);
    }

    private static string Method(Uri url, Uri home) => Root(url, home, deeperPage: false)?.Method ?? SwitchMethods.Unknown;

    private static bool SameAddress(string url, Uri home) =>
        Uri.TryCreate(url, UriKind.Absolute, out var parsed) && UrlTools.Normalize(parsed).AbsoluteUri == home.AbsoluteUri;
}
