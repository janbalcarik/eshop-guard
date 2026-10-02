namespace EshopGuard.Application.Contracts;

/// <summary>An e-shop in the list of the tenant (design of change 10, „DTO“).</summary>
public sealed record ShopListItemDto(
    Guid Id,
    string Domain,
    string? Name,
    string Platform,
    string SourceMode,
    string? ConnectorStatus,
    string Status,
    DateTimeOffset? LastRunAt,
    IReadOnlyList<string> LanguagesChecked);

public sealed record FeedDto(string Url, string Format);

/// <summary>An e-shop; <see cref="Version"/> is the ETag of the row (<c>xmin</c>) for a change.</summary>
public sealed record ShopDto(
    Guid Id,
    string Domain,
    string BaseUrl,
    string BasePath,
    string? Name,
    string HomeCountry,
    string? Language,
    string Platform,
    string PlatformSource,
    string SourceMode,
    string Status,
    int? ProductCount,
    int? PageCount,
    string? TierCode,
    IReadOnlyList<string> Modules,
    bool CheckHiddenOnSave,
    DateTimeOffset? OwnershipVerifiedAt,
    string? VerificationMethod,
    FeedDto? Feed,
    uint Version,
    DetectionDto Detection);

public sealed record RedirectedDomainDto(string Domain);

public sealed record ConnectorAvailabilityDto(string Platform, bool Available);

/// <summary>
/// The recognition of the platform: <c>pending</c> while its job runs, <c>done</c> or <c>failed</c> with a code
/// (<c>robots_blocked</c>, <c>fetch_failed</c>, <c>timeout</c>, <c>not_html</c>, <c>not_started</c>).
/// </summary>
public sealed record DetectionDto(
    string Status,
    string Platform,
    string Confidence,
    IReadOnlyList<string> Signals,
    string? FinalUrl,
    RedirectedDomainDto? RedirectedToOtherDomain,
    string? FailureCode,
    ConnectorAvailabilityDto Connector,
    string RecommendedSource);

/// <summary>
/// The free sample of an e-shop: the state of its run, the code why it failed (<c>target_not_allowed</c>,
/// <c>sample_budget_exceeded</c> …), progress, the place in the queue and, when it ended, the result.
/// </summary>
public sealed record SampleDto(
    Guid RunId,
    string Status,
    string? Error,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    SampleProgressDto Progress,
    SampleQueueDto Queue,
    SampleResultDto? Result);

public sealed record SampleProgressDto(long? PagesPlanned, long PagesFetched, long PagesProcessed);

public sealed record SampleQueueDto(int? Position, DateTimeOffset? EstimatedFinishAt);

/// <summary>
/// The result of a sample as codes and numbers: what was checked and what not (by reason, nothing left out), the counts of
/// the findings, at most 5 findings by the strictest verdict and at most 1 example of a fix that passed the recheck. The
/// explanations are composed by the frontend from the texts of the rules (<c>ruleId</c>, <c>params</c>).
/// </summary>
public sealed record SampleResultDto(
    long? PagesPlanned,
    int PagesChecked,
    NotCheckedDto NotChecked,
    IReadOnlyList<string> Versions,
    IReadOnlyList<string> Jurisdictions,
    FindingCountsDto FindingCounts,
    IReadOnlyList<SampleFindingDto> TopFindings,
    ExampleFixDto? ExampleFix,
    string? ExampleFixMissingReason);

/// <summary>Pages not checked: the groups of 3c and every reason by its code (<c>byReason</c>).</summary>
public sealed record NotCheckedDto(int RobotsBlocked, int TextNotLoaded, int TooLarge, int FetchError, int Other, IReadOnlyDictionary<string, int> ByReason);

public sealed record FindingCountsDto(int Total, IReadOnlyDictionary<string, int> ByCheckability, IReadOnlyDictionary<string, int> BySeverity);

public sealed record SampleFindingDto(
    Guid FindingId,
    Guid RuleSetId,
    string RuleId,
    string Module,
    string Scope,
    VerdictDto Strictest,
    IReadOnlyList<VerdictDto> Verdicts,
    string? Text,
    SamplePageDto? Page,
    System.Text.Json.JsonElement? Params);

/// <summary>
/// A verdict of a finding in one jurisdiction (change 6): the group, severity, band and the legal references in the language of
/// the law; <c>status</c> is <c>upcoming</c> before the rule takes effect there (<c>effectiveFrom</c>), otherwise <c>finding</c>.
/// </summary>
public sealed record VerdictDto(
    string Jurisdiction, string Checkability, string Severity, string Band, System.Text.Json.JsonElement? LegalRefs,
    string Status = "finding", DateOnly? EffectiveFrom = null);

public sealed record SamplePageDto(Guid Id, string? Title, string Url, string? Language);

public sealed record ExampleFixDto(Guid ProposalId, Guid? FindingId, string OriginalText, string ProposedText, string RecheckStatus);

/// <summary>The places of sale of an e-shop (3c): only markets with checks, each with its reason.</summary>
public sealed record ShopMarketsDto(IReadOnlyList<ShopMarketDto> Markets, DateTimeOffset? ConfirmedAt, Guid? ConfirmedBy);

/// <summary>
/// A market: its state, whether 3c ticks it in advance (strong evidence or delivery), the level of the evidence, the source,
/// the reasons as codes with parameters and verified quotes, and the support of checks (<c>limited</c>, <c>full</c>).
/// </summary>
public sealed record ShopMarketDto(
    string CountryCode,
    string MarketCode,
    bool IsHome,
    string Status,
    bool Preselected,
    string? EvidenceLevel,
    string Source,
    IReadOnlyList<MarketEvidenceDto> Evidence,
    string ChecksStatus);

/// <summary>A reason of a market: a technical sign (<c>signal</c>: <c>seat</c>, <c>tld</c>, <c>currency</c> …) or a verified quote (<c>citation</c>).</summary>
public sealed record MarketEvidenceDto(string Kind, string Code, IReadOnlyDictionary<string, string> Params, string? Quote, string? PageUrl);

/// <summary>The language versions of an e-shop (3c, 3d), computed by the same rule as the price.</summary>
public sealed record LanguageVersionsDto(LanguageSummaryDto Summary, IReadOnlyList<LanguageVersionDto> Versions);

/// <summary>
/// The summary sentence of 3c as codes: <c>single_version</c>, <c>all_checked</c>, <c>some_not_checked</c> or
/// <c>needs_confirmation</c>; the version checked for every ticked market; whether a version is judged by more markets.
/// </summary>
public sealed record LanguageSummaryDto(
    string Kind, int VersionsFound, IReadOnlyList<string> CheckedLanguages, bool MutualJurisdictions, IReadOnlyList<ScopeMarketDto> ByMarket);

/// <summary>
/// A version: address, how it is reached, its state, products, the share of the products of the sample with a description in
/// its language (<c>translatedShare</c>) and those with a description in another language, whether it is checked and why.
/// </summary>
public sealed record LanguageVersionDto(
    string Language,
    string BaseUrl,
    bool IsMain,
    string? SwitchMethod,
    string Source,
    string Status,
    int? ProductCount,
    double? TranslatedShare,
    int? SampleProducts,
    int? UntranslatedProducts,
    string? ForeignTextLanguage,
    System.Text.Json.JsonElement? LanguageShare,
    bool Checked,
    string CheckedReason,
    IReadOnlyList<string> Jurisdictions);

public sealed record ScopeMarketDto(string MarketCode, string Language, int? ProductCount, int? PageCount = null);

/// <summary>What the price counts: <c>products</c>, or <c>pages</c> to check when the products are not known (2. 10. 2026).</summary>
public sealed record ScopePriceBasisDto(string Unit, int Count);

public sealed record ScopeCheckedVersionDto(
    string Language, string BaseUrl, bool IsMain, int? ProductCount, int? OtherPageCount, IReadOnlyList<string> Jurisdictions, IReadOnlyList<string> Markets,
    string Reason);

public sealed record ScopeNotCheckedVersionDto(string Language, string BaseUrl, string Reason);

public sealed record ScopeBasisDto(Guid? SampleRunId, DateTimeOffset? FinishedAt);

/// <summary>
/// The scope of the check: the products of every ticked market, the checked and not checked versions, the jurisdictions,
/// the products for the band (<c>productTotal</c>, null when not known), other pages, the basis and <c>scopeHash</c>;
/// <c>priceBasis</c> says what the price counts (products, or pages to check when the products are not known).
/// <c>issues</c> holds what prevents a price.
/// </summary>
public sealed record ScopeDto(
    IReadOnlyList<ScopeMarketDto> Markets,
    IReadOnlyList<ScopeCheckedVersionDto> CheckedVersions,
    IReadOnlyList<ScopeNotCheckedVersionDto> NotCheckedVersions,
    IReadOnlyList<string> Jurisdictions,
    int? ProductTotal,
    int? OtherPagesTotal,
    ScopeBasisDto Basis,
    IReadOnlyList<string> Issues,
    string ScopeHash,
    ScopePriceBasisDto? PriceBasis = null);

public sealed record QuoteDto(ScopeDto Scope, PriceQuoteDto Price);

/// <summary>
/// The price for a scope (change 12): amounts without VAT, the discount of the monitoring, the preview of VAT and the state
/// (<c>offer</c> can be paid; <c>individual_offer</c> with <c>billing.fair_use_exceeded</c> or <c>billing.individual_offer</c>;
/// <c>unavailable</c> with <c>billing.price_list_missing</c> or <c>billing.stripe_mode_mismatch</c>). <c>CountedProducts</c>
/// in <c>PriceUnit</c> is what the tier was chosen by. Codes and numbers only; the frontend composes the sentences.
/// </summary>
public sealed record PriceQuoteDto(
    Guid QuoteId,
    Guid? PriceListId,
    string Currency,
    string? TierCode,
    int? TierMaxProducts,
    bool IsCustom,
    FairUseDto FairUse,
    decimal? AnalysisNet,
    decimal? MonitoringMonthlyNet,
    decimal? MonitoringDiscountPercent,
    decimal? TodayNet,
    DateTimeOffset? FirstMonitoringChargeAt,
    System.Text.Json.JsonElement? VatPreview,
    DateTimeOffset ValidUntil,
    string Status = "offer",
    string? ReasonCode = null,
    string? PriceUnit = null,
    int? CountedProducts = null);

public sealed record FairUseDto(int? OtherPagesLimit, bool Exceeded);

/// <summary>Where the onboarding of an e-shop is (<c>connect</c>, <c>sample</c>, <c>scope</c>, <c>payment</c>, <c>analysis</c>, <c>done</c>) and what blocks the order.</summary>
public sealed record OnboardingStateDto(
    string Step,
    IReadOnlyList<string> Blocking,
    OnboardingSampleDto Sample,
    bool MarketsConfirmed,
    IReadOnlyList<string> LanguagesAwaitingConfirmation,
    OnboardingOwnershipDto Ownership,
    string? ScopeHash);

public sealed record OnboardingSampleDto(string? Status, Guid? RunId);

public sealed record OnboardingOwnershipDto(bool Required, bool Verified);

/// <summary>The ownership of an e-shop: verified or not, by which method, before which steps it is required, the verifications.</summary>
public sealed record OwnershipDto(bool Verified, DateTimeOffset? VerifiedAt, string? Method, IReadOnlyList<string> RequiredBefore, IReadOnlyList<VerificationDto> Verifications);

/// <summary>
/// A verification: its method, state, the token and what to put where (<c>metaTag</c> for the <c>&lt;head&gt;</c>, or the
/// name and the value of the TXT record), and the code of a failure (<c>meta_not_found</c>, <c>dns_record_not_found</c>,
/// <c>token_mismatch</c>, <c>fetch_failed</c>).
/// </summary>
public sealed record VerificationDto(
    Guid Id, string Method, string Status, string Token, VerificationInstructionsDto Instructions, DateTimeOffset? CheckedAt, string? FailureCode);

public sealed record VerificationInstructionsDto(string? MetaTag, string? DnsName, string? DnsValue);

/// <summary>The settings of an e-shop: name, modules (available for its markets), the check on save and the excluded versions.</summary>
public sealed record ShopSettingsDto(
    string? Name,
    IReadOnlyList<ShopModuleDto> Modules,
    bool CheckHiddenOnSave,
    bool CheckHiddenOnSaveAvailable,
    IReadOnlyList<string> ExcludedLanguages,
    uint Version);

public sealed record ShopModuleDto(string Module, bool Enabled, bool Available, IReadOnlyList<string> Jurisdictions);
