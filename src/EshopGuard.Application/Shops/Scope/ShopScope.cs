using EshopGuard.Data.Entities.Shops;

namespace EshopGuard.Application.Shops.Scope;

/// <summary>A language version for the scope: the stored row of <c>shop.shop_languages</c> with the numbers of the sample.</summary>
/// <param name="DecidedByUser">The client decided its state (an <c>excluded</c> of the client, not of the analysis).</param>
/// <param name="ProductCount">Products of the version; null when not known.</param>
/// <param name="OtherPageCount">Other pages of the version from the basis of the sample; null when not known.</param>
public sealed record ScopeVersionInput(
    string Language, string BaseUrl, ShopLanguageStatus Status, bool DecidedByUser, bool IsMain, int? ProductCount, int? OtherPageCount);

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

/// <summary>The products of one ticked market: those of the version checked for it.</summary>
public sealed record ScopeMarket(string MarketCode, string Language, int? ProductCount);

/// <summary>
/// The scope of the check and the basis of the price (change 10, AD 7): the same rule for the check and the price.
/// <see cref="Issues"/> holds what prevents a price (<c>scope.no_checkable_version</c>, <c>scope.product_count_unknown</c>).
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
    string ScopeHash);
