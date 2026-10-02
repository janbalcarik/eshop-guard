using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using EshopGuard.Application.Problems;
using EshopGuard.Core.Languages;
using EshopGuard.Core.Markets;
using EshopGuard.Data.Entities.Shops;

namespace EshopGuard.Application.Shops.Scope;

/// <summary>
/// The scope of the check (change 10, AD 7; a pure function, the same for 3c, 3d, the quote and the order):
/// <list type="number">
/// <item>the ticked markets with checks (<c>ref.markets</c> and an enabled rule set);</item>
/// <item>for every market the active version in its language, otherwise the main version, otherwise <c>scope.no_checkable_version</c>;</item>
/// <item>every checked version judged by every ticked market whose customers read it (<c>readable_languages</c> of
///       <c>config/jurisdictions.yaml</c>);</item>
/// <item>the products for the band: for every ticked market those of its version (decision of 2. 10. 2026), an unknown number
///       gives <c>scope.product_count_unknown</c> and no price;</item>
/// <item><see cref="ShopScope.ScopeHash"/>: SHA-256 of the canonical JSON of the markets, the checked versions with their
///       numbers, the excluded versions and the sample of the basis.</item>
/// </list>
/// The choice of the versions is the library's <see cref="VersionMarketPlanner"/>, the same one the sample used.
/// </summary>
public static class ShopScopeCalculator
{
    public const string MarketLanguage = "market_language";
    public const string MainFallback = "main_fallback";
    public const string NotNeeded = "not_needed_by_markets";
    public const string Excluded = "excluded";
    public const string AwaitingConfirmation = "awaiting_confirmation";

    public static ShopScope Calculate(ShopScopeInput input, MarketCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(catalog);
        var markets = catalog.Supported.Where(m => input.ActiveMarkets.Contains(m.Code, StringComparer.Ordinal)).ToList();
        var excluded = input.Excluded.Select(LanguageTags.Normalize).ToHashSet(StringComparer.Ordinal);
        var candidates = input.Versions.Select(v => new LanguageVersionCandidate
        {
            Language = v.Language,
            BaseUrl = v.BaseUrl,
            SwitchMethod = SwitchMethods.Unknown,
            Source = v.IsMain ? VersionSources.Main : "stored",
            Status = Status(v, excluded),
            IsMain = v.IsMain,
        }).ToList();

        var issues = new List<string>();
        if (markets.Count == 0)
        {
            issues.Add(ProblemCodes.MarketsNoneSelected);
        }

        var main = candidates.FirstOrDefault(c => c.IsMain);
        var uncovered = markets.Where(m => !candidates.Any(c => c.Status == VersionStatus.Active && LanguageTags.SamePrimary(c.Language, m.Language))
            && main?.Status != VersionStatus.Active).ToList();
        if (main is null || uncovered.Count > 0)
        {
            issues.Add(ProblemCodes.ScopeNoCheckableVersion);
        }

        if (issues.Count > 0)
        {
            var none = input.Versions.Where(v => v.Status != ShopLanguageStatus.Unsupported)
                .Select(v => new ScopeNotCheckedVersion(v.Language, v.BaseUrl, Reason(Status(v, excluded)))).ToList();
            return Finish(markets.Select(m => m.Code).ToList(), [], none, [], null, null, input, excluded, issues);
        }

        var counts = input.Versions.ToDictionary(v => VersionMarketPlanner.Key(v.Language, v.BaseUrl), v => v.ProductCount, StringComparer.Ordinal);
        var plan = VersionMarketPlanner.Plan(candidates, markets.Select(m => m.Code).ToList(), catalog, counts);
        var byKey = input.Versions.ToDictionary(v => VersionMarketPlanner.Key(v.Language, v.BaseUrl), StringComparer.Ordinal);
        var checkedVersions = plan.Checked.Select(v =>
        {
            var stored = byKey[VersionMarketPlanner.Key(v.Language, v.BaseUrl)];
            var own = v.Markets.Any(code => LanguageTags.SamePrimary(catalog.ByCode(code)!.Language, v.Language));
            return new ScopeCheckedVersion(v.Language, v.BaseUrl, v.IsMain, stored.ProductCount, stored.OtherPageCount,
                [.. v.Jurisdictions.Order(StringComparer.Ordinal)], v.Markets, own ? MarketLanguage : MainFallback);
        }).ToList();
        var notChecked = plan.NotChecked
            .Where(v => byKey.TryGetValue(VersionMarketPlanner.Key(v.Language, v.BaseUrl), out var stored) && stored.Status != ShopLanguageStatus.Unsupported)
            .Select(v => new ScopeNotCheckedVersion(v.Language ?? "und", v.BaseUrl, Reason(v.Code))).ToList();
        var byMarket = plan.ByMarket.Select(m => new ScopeMarket(m.Market, m.Language, m.ProductCount)).ToList();
        if (plan.ProductCountUnknown)
        {
            issues.Add(ProblemCodes.ScopeProductCountUnknown);
        }

        int? productTotal = plan.ProductCountUnknown ? null : plan.CountedProducts;
        int? otherPages = checkedVersions.All(v => v.OtherPageCount is not null) ? checkedVersions.Sum(v => v.OtherPageCount!.Value) : null;
        return Finish(plan.ActiveMarkets, checkedVersions, notChecked, byMarket, productTotal, otherPages, input, excluded, issues);
    }

    /// <summary>
    /// The state of a stored version for the planner: an exclusion of the analysis (a version no ticked market needed) is
    /// available again when a market needs it; an exclusion of the client, or one asked for now, stays.
    /// </summary>
    private static string Status(ScopeVersionInput version, HashSet<string> excluded) => version.Status switch
    {
        _ when excluded.Contains(LanguageTags.Normalize(version.Language)) => VersionStatus.Excluded,
        ShopLanguageStatus.Active => VersionStatus.Active,
        ShopLanguageStatus.Excluded => version.DecidedByUser ? VersionStatus.Excluded : VersionStatus.Active,
        ShopLanguageStatus.NeedsConfirmation => VersionStatus.NeedsConfirmation,
        ShopLanguageStatus.NeedsBrowser => VersionStatus.NeedsBrowser,
        ShopLanguageStatus.Mismatch => VersionStatus.Mismatch,
        _ => VersionStatus.Unsupported,
    };

    private static string Reason(string code) => code switch
    {
        VersionCodes.NotNeeded or VersionStatus.Active => NotNeeded,
        "version_excluded" or VersionStatus.Excluded => Excluded,
        VersionCodes.OtherDomainNeedsConfirmation or VersionStatus.NeedsConfirmation => AwaitingConfirmation,
        VersionStatus.NeedsBrowser => VersionCodes.NeedsBrowser,
        VersionStatus.Mismatch => VersionCodes.LanguageMismatch,
        _ => code,
    };

    private static ShopScope Finish(
        IReadOnlyList<string> markets, IReadOnlyList<ScopeCheckedVersion> checkedVersions, IReadOnlyList<ScopeNotCheckedVersion> notChecked,
        IReadOnlyList<ScopeMarket> byMarket, int? productTotal, int? otherPages, ShopScopeInput input, HashSet<string> excluded, List<string> issues)
    {
        var sortedMarkets = markets.Order(StringComparer.Ordinal).ToList();
        var canonical = new JsonObject
        {
            ["markets"] = new JsonArray([.. sortedMarkets.Select(m => (JsonNode)m)]),
            ["checked"] = new JsonArray([.. checkedVersions.OrderBy(v => v.Language, StringComparer.Ordinal).Select(v => (JsonNode)new JsonObject
            {
                ["language"] = v.Language,
                ["base_url"] = v.BaseUrl,
                ["product_count"] = v.ProductCount,
                ["other_page_count"] = v.OtherPageCount,
                ["jurisdictions"] = new JsonArray([.. v.Jurisdictions.Select(j => (JsonNode)j)]),
                ["markets"] = new JsonArray([.. v.Markets.Order(StringComparer.Ordinal).Select(m => (JsonNode)m)]),
            })]),
            ["excluded"] = new JsonArray([.. excluded.Order(StringComparer.Ordinal).Select(l => (JsonNode)l)]),
            ["basis"] = input.SampleRunId?.ToString("D"),
        };
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToJsonString())));
        return new ShopScope(sortedMarkets, checkedVersions, notChecked, byMarket, sortedMarkets, productTotal, otherPages, input.SampleRunId, issues, hash);
    }
}
