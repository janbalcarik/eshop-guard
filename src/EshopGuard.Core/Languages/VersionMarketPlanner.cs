using EshopGuard.Core.Markets;

namespace EshopGuard.Core.Languages;

/// <summary>A checked version: its language, address, the markets it is judged for, and its products for the price.</summary>
public sealed record PlannedVersion(string Language, string BaseUrl, IReadOnlyList<string> Jurisdictions, bool IsMain)
{
    /// <summary>Products of the version (sitemap or connector); null when not known.</summary>
    public int? ProductCount { get; init; }

    /// <summary>The version counts into the price (own texts, see <see cref="VersionComparer"/>).</summary>
    public bool Counted { get; init; }
}

/// <summary>A version that is not checked, with the reason as a code.</summary>
public sealed record UncheckedVersion(string? Language, string BaseUrl, string Code);

/// <summary>Products and counting of one version from the analysis of versions (key = <see cref="VersionMarketPlanner.Key"/>).</summary>
public sealed record VersionCount(int? ProductCount, bool Counted);

/// <summary>
/// Which versions are checked for the ticked markets and how many products count into the price (change 7, design section 5).
/// Built and recalculated without a download or a model.
/// </summary>
public sealed record VersionPlan
{
    public IReadOnlyList<string> ActiveMarkets { get; init; } = [];

    public IReadOnlyList<PlannedVersion> Checked { get; init; } = [];

    public IReadOnlyList<UncheckedVersion> NotChecked { get; init; } = [];

    /// <summary>Products for the price band: the first checked version and every other checked version that counts.</summary>
    public int CountedProducts { get; init; }

    /// <summary>A checked version that counts has an unknown number of products (the price is then not guaranteed).</summary>
    public bool ProductCountUnknown { get; init; }

    /// <summary>The versions the plan was made from (for <see cref="Recalculate"/>).</summary>
    public IReadOnlyList<LanguageVersionCandidate> Versions { get; init; } = [];

    /// <summary>Products and counting by version.</summary>
    public IReadOnlyDictionary<string, VersionCount> Counts { get; init; } = new Dictionary<string, VersionCount>();

    /// <summary>The plan for other ticked markets (a country unticked on 3c): nothing is downloaded and no model is called.</summary>
    public VersionPlan Recalculate(IReadOnlyList<string> activeMarkets, MarketCatalog catalog) =>
        VersionMarketPlanner.Plan(Versions, activeMarkets, catalog, Counts);
}

/// <summary>
/// For every ticked market the version in its language, otherwise the main version; every checked version is judged by all
/// ticked markets whose customers read it (<c>readable_languages</c>). Versions waiting for a confirmation, needing a browser,
/// answering in another language, excluded, unsupported or made by a translation in the browser are listed with a code.
/// </summary>
public static class VersionMarketPlanner
{
    /// <summary>Key of a version in <see cref="VersionPlan.Counts"/>.</summary>
    public static string Key(string? language, string baseUrl) => $"{language ?? "und"}|{baseUrl}";

    public static VersionPlan Plan(
        IReadOnlyList<LanguageVersionCandidate> versions, IReadOnlyList<string> activeMarkets, MarketCatalog catalog,
        IReadOnlyDictionary<string, VersionCount>? counts = null)
    {
        ArgumentNullException.ThrowIfNull(versions);
        ArgumentNullException.ThrowIfNull(catalog);
        counts ??= new Dictionary<string, VersionCount>();
        var markets = activeMarkets.Select(catalog.ByCode).OfType<MarketDefinition>().ToList();
        var main = versions.First(v => v.IsMain);
        var plannable = versions.Where(Plannable).ToList();

        // The version of each market: its own language, otherwise the main version.
        var chosenBy = new Dictionary<LanguageVersionCandidate, List<string>>();
        foreach (var market in markets)
        {
            var version = plannable.Where(v => LanguageTags.SamePrimary(v.Language, market.Language)).OrderByDescending(v => v.IsMain).FirstOrDefault() ?? main;
            if (!chosenBy.TryGetValue(version, out var list))
            {
                chosenBy[version] = list = [];
            }

            list.Add(market.Code);
        }

        var checkedVersions = versions.Where(chosenBy.ContainsKey).OrderByDescending(v => v.IsMain).Select(v =>
        {
            var readers = markets.Where(m => m.Reads(v.Language) && !chosenBy[v].Contains(m.Code)).Select(m => m.Code);
            var count = counts.GetValueOrDefault(Key(v.Language, v.BaseUrl));
            return new PlannedVersion(v.Language ?? "und", v.BaseUrl, [.. chosenBy[v], .. readers], v.IsMain)
            {
                ProductCount = count?.ProductCount,
                Counted = count?.Counted ?? v.IsMain,
            };
        }).ToList();

        var notChecked = versions.Where(v => !chosenBy.ContainsKey(v)).Select(v => new UncheckedVersion(v.Language, v.BaseUrl, Reason(v))).ToList();
        var counted = checkedVersions.Where((v, i) => i == 0 || v.Counted).ToList();
        return new VersionPlan
        {
            ActiveMarkets = markets.Select(m => m.Code).ToList(),
            Checked = checkedVersions,
            NotChecked = notChecked,
            CountedProducts = counted.Sum(v => v.ProductCount ?? 0),
            ProductCountUnknown = counted.Any(v => v.ProductCount is null),
            Versions = versions,
            Counts = counts,
        };
    }

    /// <summary>The version can be checked: active, with a text of its own and reachable.</summary>
    private static bool Plannable(LanguageVersionCandidate version) =>
        version.Status == VersionStatus.Active
        && version.SwitchMethod != SwitchMethods.BrowserTranslation
        && !version.Codes.Contains(VersionCodes.RobotsDisallowed)
        && !version.Codes.Contains(VersionCodes.Unreachable);

    private static string Reason(LanguageVersionCandidate version) => version.Status switch
    {
        VersionStatus.NeedsConfirmation => VersionCodes.OtherDomainNeedsConfirmation,
        VersionStatus.NeedsBrowser => VersionCodes.NeedsBrowser,
        VersionStatus.Mismatch => version.Codes.FirstOrDefault() ?? VersionCodes.LanguageMismatch,
        VersionStatus.Excluded => "version_excluded",
        VersionStatus.Unsupported => "version_unsupported",
        _ when version.SwitchMethod == SwitchMethods.BrowserTranslation => VersionCodes.BrowserTranslation,
        _ when version.Codes.FirstOrDefault(c => c is VersionCodes.RobotsDisallowed or VersionCodes.Unreachable) is { } code => code,
        _ => VersionCodes.NotNeeded,
    };
}
