using System.Text.RegularExpressions;
using EshopGuard.Core.Rules;

namespace EshopGuard.Core.Markets;

/// <summary>A market the tool knows: a jurisdiction of <c>config/jurisdictions.yaml</c> that has a country.</summary>
/// <param name="Code">Code of the jurisdiction in the rules (<c>sk</c>).</param>
/// <param name="Country">ISO 3166-1 alpha-2 (<c>SK</c>).</param>
/// <param name="Language">Language of the shop version for this market (<c>sk</c>).</param>
/// <param name="ReadableLanguages">Languages of versions that customers of this market read (<c>sk</c>, <c>cs</c>).</param>
/// <param name="Tlds">Top-level domains of the market (<c>sk</c>).</param>
public sealed record MarketDefinition(string Code, string Country, string Language, IReadOnlyList<string> ReadableLanguages, IReadOnlyList<string> Tlds)
{
    /// <summary>Customers of this market read a version in the language (by its primary subtag).</summary>
    public bool Reads(string? language) => ReadableLanguages.Any(l => LanguageTags.SamePrimary(l, language));
}

/// <summary>
/// The markets from <c>config/jurisdictions.yaml</c> (in the web application the same values come from <c>ref.markets</c>).
/// A market is supported when an enabled rule set covers its jurisdiction and the host allows it; other countries found
/// on a shop are kept as unsupported. Another market is a line of data, never code.
/// </summary>
public sealed partial class MarketCatalog
{
    private MarketCatalog(IReadOnlyList<MarketDefinition> all, IReadOnlyList<MarketDefinition> supported)
    {
        All = all;
        Supported = supported;
    }

    /// <summary>No markets.</summary>
    public static MarketCatalog Empty { get; } = new([], []);

    /// <summary>Every market of the registry, in the order of its codes.</summary>
    public IReadOnlyList<MarketDefinition> All { get; }

    /// <summary>Markets the tool can check: enabled rule sets for the jurisdiction and allowed by the host.</summary>
    public IReadOnlyList<MarketDefinition> Supported { get; }

    /// <summary>
    /// The markets of the loaded rules. <paramref name="hostAllows"/> is the host's switch per market code
    /// (<c>ref.markets.checks_status != none</c> in the web application); null allows every market.
    /// </summary>
    public static MarketCatalog From(RuleCatalog rules, Func<string, bool>? hostAllows = null)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var all = rules.Jurisdictions.Codes
            .Select(code => (code, info: rules.Jurisdictions.Items[code]))
            .Where(j => j.info.Country.Length > 0)
            .Select(j => new MarketDefinition(
                j.code,
                j.info.Country,
                LanguageTags.Normalize(j.info.Language),
                j.info.ReadableLanguages.Select(LanguageTags.Normalize).ToList(),
                j.info.Tlds.Select(t => t.Trim().TrimStart('.').ToLowerInvariant()).ToList()))
            .ToList();
        var covered = rules.RuleSets.Where(s => s.Enabled).SelectMany(s => s.Jurisdictions).ToHashSet(StringComparer.Ordinal);
        var supported = all.Where(m => covered.Contains(m.Code) && (hostAllows?.Invoke(m.Code) ?? true)).ToList();
        return new MarketCatalog(all, supported);
    }

    /// <summary>The market of the country (ISO alpha-2, any case), supported or not; null when the tool does not know it.</summary>
    public MarketDefinition? ByCountry(string? country) =>
        All.FirstOrDefault(m => string.Equals(m.Country, country?.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>The market of a jurisdiction code.</summary>
    public MarketDefinition? ByCode(string? code) => All.FirstOrDefault(m => m.Code == code);

    /// <summary>The market whose top-level domain the host has (<c>shop.sk</c> gives SK); null for <c>.com</c> and unknown domains.</summary>
    public MarketDefinition? ByHost(string host)
    {
        var dot = host.TrimEnd('.').LastIndexOf('.');
        var tld = dot >= 0 ? host[(dot + 1)..].ToLowerInvariant() : "";
        return tld.Length == 0 ? null : All.FirstOrDefault(m => m.Tlds.Contains(tld));
    }

    /// <summary>The country is a supported market.</summary>
    public bool IsSupportedCountry(string? country) =>
        Supported.Any(m => string.Equals(m.Country, country?.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>Customers of some supported market read a version in the language.</summary>
    public bool IsSupportedLanguage(string? language) => Supported.Any(m => m.Reads(language));

    /// <summary>Supported markets whose own language is the language (the market for which a version in it is checked).</summary>
    public IEnumerable<MarketDefinition> SupportedWithLanguage(string? language) => Supported.Where(m => LanguageTags.SamePrimary(m.Language, language));

    /// <summary>Errors of the market fields of one jurisdiction (none when it is not a market).</summary>
    internal static IEnumerable<string> Validate(string code, JurisdictionInfo info)
    {
        var hasMarketFields = info.Language.Length > 0 || info.ReadableLanguages.Count > 0 || info.Tlds.Count > 0;
        if (info.Country.Length == 0)
        {
            if (hasMarketFields)
            {
                yield return $"jurisdikce „{code}“ má language, readable_languages nebo tlds, ale chybí jí country.";
            }

            yield break;
        }

        if (!CountryCode().IsMatch(info.Country))
        {
            yield return $"jurisdikce „{code}“: country musí být kód ISO 3166-1 alpha-2 velkými písmeny (např. SK).";
        }

        if (!LanguageTags.IsLanguageTag(info.Language))
        {
            yield return $"jurisdikce „{code}“: language musí být jazykový kód (např. sk).";
        }
        else if (!info.ReadableLanguages.Any(l => LanguageTags.SamePrimary(l, info.Language)))
        {
            yield return $"jurisdikce „{code}“: readable_languages musí obsahovat i language ({info.Language}).";
        }

        foreach (var language in info.ReadableLanguages.Where(l => !LanguageTags.IsLanguageTag(l)))
        {
            yield return $"jurisdikce „{code}“: „{language}“ v readable_languages není jazykový kód.";
        }

        foreach (var tld in info.Tlds.Where(t => !TopLevelDomain().IsMatch(t)))
        {
            yield return $"jurisdikce „{code}“: „{tld}“ v tlds musí být doména nejvyššího řádu malými písmeny bez tečky.";
        }
    }

    [GeneratedRegex("^[A-Z]{2}$")]
    private static partial Regex CountryCode();

    [GeneratedRegex("^[a-z0-9-]{2,63}$")]
    private static partial Regex TopLevelDomain();
}
