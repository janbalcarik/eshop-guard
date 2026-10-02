using EshopGuard.Data.Entities.Shops;

namespace EshopGuard.Application.Shops.Scope;

/// <summary>A language version for the scope: the stored row of <c>shop.shop_languages</c> with the numbers of the sample.</summary>
/// <param name="DecidedByUser">The client decided its state (an <c>excluded</c> of the client, not of the analysis).</param>
/// <param name="ProductCount">Products of the version; null when not known.</param>
/// <param name="OtherPageCount">Other pages of the version from the basis of the sample; null when not known.</param>
/// <param name="SitemapPageCount">All pages of the sitemap of the version (products and other pages); null in an older basis.</param>
public sealed record ScopeVersionInput(
    string Language, string BaseUrl, ShopLanguageStatus Status, bool DecidedByUser, bool IsMain, int? ProductCount, int? OtherPageCount,
    int? SitemapPageCount = null)
{
    /// <summary>The pages to check of the version: its sitemap, else its products and other pages; null when not known.</summary>
    public int? PageCount => SitemapPageCount ?? (OtherPageCount is { } other ? (ProductCount ?? 0) + other : null);
}

/// <summary>What the scope is computed from: the ticked markets, the versions, the excluded versions and the sample of the basis.</summary>
public sealed record ShopScopeInput(
    IReadOnlyList<string> ActiveMarkets, IReadOnlyList<ScopeVersionInput> Versions, IReadOnlyCollection<string> Excluded, Guid? SampleRunId);

/// <summary>A checked version: for which markets it is checked (its products count for each), by which jurisdictions it is judged.</summary>
/// <param name="Reason"><c>market_language</c> (the language of a ticked market) or <c>main_fallback</c> (no version in it).</param>
public sealed record ScopeCheckedVersion(
    string Language, string BaseUrl, bool IsMain, int? ProductCount, int? OtherPageCount, IReadOnlyList<string> Jurisdictions,
    IReadOnlyList<string> Markets, string Reason);

/// <summary>A version that is not checked, with its reason (<c>not_needed_by_markets</c>, <c>excluded</c>, <c>awaiting_confirmation</c> …).</summary>
public sealed record ScopeNotCheckedVersion(string Language, string BaseUrl, string Reason);

/// <summary>The products of one ticked market: those of the version checked for it, and its pages to check.</summary>
public sealed record ScopeMarket(string MarketCode, string Language, int? ProductCount, int? PageCount = null);

/// <summary>Units of the price of a scope (decision of 2. 10. 2026).</summary>
public static class PriceUnits
{
    /// <summary>The products of the ticked markets (the products of every checked version are known).</summary>
    public const string Products = "products";

    /// <summary>The pages to check of the ticked markets: the products of a version are not known, its sitemap pages are.</summary>
    public const string Pages = "pages";
}

/// <summary>
/// The scope of the check and the basis of the price (change 10, AD 7): the same rule for the check and the price.
/// <see cref="Issues"/> holds what prevents a price (<c>scope.no_checkable_version</c>, <c>scope.product_count_unknown</c>).
/// The price counts <see cref="PriceCount"/> of <see cref="PriceUnit"/>: products when every checked version knows them, else
/// the pages to check of the sitemaps (decision of 2. 10. 2026); null when neither is known.
/// </summary>
public sealed record ShopScope(
    IReadOnlyList<string> Markets,
    IReadOnlyList<ScopeCheckedVersion> CheckedVersions,
    IReadOnlyList<ScopeNotCheckedVersion> NotCheckedVersions,
    IReadOnlyList<ScopeMarket> ByMarket,
    IReadOnlyList<string> Jurisdictions,
    int? ProductTotal,
    int? OtherPagesTotal,
    Guid? SampleRunId,
    IReadOnlyList<string> Issues,
    string ScopeHash,
    string? PriceUnit = null,
    int? PriceCount = null);
