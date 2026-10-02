using EshopGuard.Core.Markets;

namespace EshopGuard.Core.Languages;

/// <summary>A checked version: its language, address, the markets it is judged for, and its products.</summary>
public sealed record PlannedVersion(string Language, string BaseUrl, IReadOnlyList<string> Jurisdictions, bool IsMain)
{
    /// <summary>Products of the version (sitemap or connector); null when not known.</summary>
    public int? ProductCount { get; init; }

    /// <summary>The ticked markets this version is checked for (their products count into the price), a subset of <see cref="Jurisdictions"/>.</summary>
    public IReadOnlyList<string> Markets { get; init; } = [];
}

/// <summary>A version that is not checked, with the reason as a code.</summary>
public sealed record UncheckedVersion(string? Language, string BaseUrl, string Code);

/// <summary>The products of one ticked market for the price: those of the version checked for it (null when not known).</summary>
public sealed record MarketProducts(string Market, string Language, string BaseUrl, int? ProductCount);

/// <summary>
/// Which versions are checked for the ticked markets and how many products count into the price (change 7, design section 5;
/// price for every country, decision of 2. 10. 2026). Built and recalculated without a download or a model.
/// </summary>
public sealed record VersionPlan
{
    public IReadOnlyList<string> ActiveMarkets { get; init; } = [];

    public IReadOnlyList<PlannedVersion> Checked { get; init; } = [];

    public IReadOnlyList<UncheckedVersion> NotChecked { get; init; } = [];

    /// <summary>The products of every ticked market: the version checked for it and its products.</summary>
    public IReadOnlyList<MarketProducts> ByMarket { get; init; } = [];

    /// <summary>Products for the price band: the sum over the ticked markets (a version checked for two markets counts twice).</summary>
    public int CountedProducts { get; init; }

    /// <summary>The products of a ticked market are not known: the sum is not known either (the price is not guaranteed).</summary>
    public bool ProductCountUnknown { get; init; }

    /// <summary>The versions the plan was made from (for <see cref="Recalculate"/>).</summary>
    public IReadOnlyList<LanguageVersionCandidate> Versions { get; init; } = [];

    /// <summary>Products by version (key <see cref="VersionMarketPlanner.Key"/>), null when not known.</summary>
    public IReadOnlyDictionary<string, int?> ProductCounts { get; init; } = new Dictionary<string, int?>();

    /// <summary>The plan for other ticked markets (a country unticked on 3c): nothing is downloaded and no model is called.</summary>
    public VersionPlan Recalculate(IReadOnlyList<string> activeMarkets, MarketCatalog catalog) =>
        VersionMarketPlanner.Plan(Versions, activeMarkets, catalog, ProductCounts);
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
        IReadOnlyDictionary<string, int?>? productCounts = null)
    {
        ArgumentNullException.ThrowIfNull(versions);
        ArgumentNullException.ThrowIfNull(catalog);
        productCounts ??= new Dictionary<string, int?>();
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
            return new PlannedVersion(v.Language ?? "und", v.BaseUrl, [.. chosenBy[v], .. readers], v.IsMain)
            {
                ProductCount = productCounts.GetValueOrDefault(Key(v.Language, v.BaseUrl)),
                Markets = chosenBy[v],
            };
        }).ToList();

        // The price for every country: the products of the version checked for it (one version for two countries twice).
        var byMarket = markets.Select(m => (Market: m, Version: checkedVersions.First(v => v.Markets.Contains(m.Code))))
            .Select(x => new MarketProducts(x.Market.Code, x.Version.Language, x.Version.BaseUrl, x.Version.ProductCount))
            .ToList();
        var notChecked = versions.Where(v => !chosenBy.ContainsKey(v)).Select(v => new UncheckedVersion(v.Language, v.BaseUrl, Reason(v))).ToList();
        return new VersionPlan
        {
            ActiveMarkets = markets.Select(m => m.Code).ToList(),
            Checked = checkedVersions,
            NotChecked = notChecked,
            ByMarket = byMarket,
            CountedProducts = byMarket.Sum(m => m.ProductCount ?? 0),
            ProductCountUnknown = byMarket.Any(m => m.ProductCount is null),
            Versions = versions,
            ProductCounts = productCounts,
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
