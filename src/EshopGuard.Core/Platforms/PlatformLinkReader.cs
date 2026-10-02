using EshopGuard.Core.Crawl;
using EshopGuard.Core.Markets;

namespace EshopGuard.Core.Platforms;

/// <summary>What the links of a platform on a page say: the platform, the currencies it offers and its language versions.</summary>
/// <param name="Platform">The platform whose links were read, or null when none is certain on the page.</param>
/// <param name="Currencies">ISO 4217 codes the currency switch of the platform offers.</param>
/// <param name="Languages">Languages the language switch of the platform offers, with the address of the switch.</param>
public sealed record PlatformLinkSignals(string? Platform, IReadOnlyList<string> Currencies, IReadOnlyList<(string Language, string Url)> Languages)
{
    /// <summary>Nothing read (no certain platform, or no switch of it).</summary>
    public static readonly PlatformLinkSignals None = new(null, [], []);
}

/// <summary>
/// Reads the currency and language switches of a known platform (<c>config/platforms.yaml</c>, kinds of
/// <see cref="PlatformLinkKinds"/>) from the links of a page. Only for a platform that is certain on the page: one of its
/// <c>host</c> signatures among the scripts the page loads and one of its links on the site itself (two independent technical
/// signs). The values come from the query parameter of the link, never from its text.
/// </summary>
public static class PlatformLinkReader
{
    /// <summary>The switches of the first platform that is certain on the page; <see cref="PlatformLinkSignals.None"/> otherwise.</summary>
    /// <param name="signatures">The data of <c>config/platforms.yaml</c>; null reads nothing.</param>
    /// <param name="links">Absolute addresses of the links of the page.</param>
    /// <param name="scriptSources">Absolute addresses of the scripts the page loads.</param>
    /// <param name="site">The e-shop; only its own links count.</param>
    public static PlatformLinkSignals Read(PlatformSignatures? signatures, IEnumerable<Uri> links, IEnumerable<string> scriptSources, Uri site)
    {
        ArgumentNullException.ThrowIfNull(links);
        ArgumentNullException.ThrowIfNull(scriptSources);
        ArgumentNullException.ThrowIfNull(site);
        if (signatures is null)
        {
            return PlatformLinkSignals.None;
        }

        var scriptHosts = scriptSources.Select(s => Uri.TryCreate(s, UriKind.Absolute, out var u) ? u.IdnHost.ToLowerInvariant() : null).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var own = links.Where(l => UrlTools.IsSameSite(l, site)).ToList();
        foreach (var (platform, patterns) in signatures.LinksByPlatform)
        {
            if (patterns.Count == 0)
            {
                continue;
            }

            var hosts = signatures.ByPlatform[platform].Where(s => s.Kind == SignatureKinds.Host).Select(s => s.Value).ToList();
            if (!scriptHosts.Any(h => hosts.Any(v => h == v || h.EndsWith("." + v, StringComparison.Ordinal))))
            {
                continue;
            }

            var currencies = new List<string>();
            var languages = new List<(string Language, string Url)>();
            foreach (var link in own)
            {
                foreach (var pattern in patterns.Where(p => SamePath(link.AbsolutePath, p.Path)))
                {
                    var value = Parameter(link, pattern.Parameter);
                    if (pattern.Kind == PlatformLinkKinds.CurrencySwitch && value is { Length: 3 } code && code.All(char.IsAsciiLetter))
                    {
                        currencies.Add(code.ToUpperInvariant());
                    }
                    else if (pattern.Kind == PlatformLinkKinds.LanguageSwitch && LanguageTags.IsLanguageTag(value))
                    {
                        languages.Add((LanguageTags.Normalize(value), link.AbsoluteUri));
                    }
                }
            }

            if (currencies.Count > 0 || languages.Count > 0)
            {
                return new PlatformLinkSignals(platform, currencies.Distinct(StringComparer.Ordinal).ToList(), languages.DistinctBy(l => l.Language).ToList());
            }
        }

        return PlatformLinkSignals.None;
    }

    private static bool SamePath(string path, string pattern) =>
        string.Equals(path.TrimEnd('/'), pattern.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);

    private static string? Parameter(Uri url, string name)
    {
        foreach (var pair in url.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Split('=', 2)))
        {
            if (pair.Length == 2 && string.Equals(Uri.UnescapeDataString(pair[0]), name, StringComparison.OrdinalIgnoreCase))
            {
                return Uri.UnescapeDataString(pair[1]).Trim();
            }
        }

        return null;
    }
}
