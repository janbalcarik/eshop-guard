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

/// <summary>The free sample of an e-shop: the state of its run, progress, the place in the queue and, when it ended, the result.</summary>
public sealed record SampleDto(
    Guid RunId,
    string Status,
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

/// <summary>A verdict of one jurisdiction (the shape of change 6); the legal references in the language of the law.</summary>
public sealed record VerdictDto(string Jurisdiction, string Checkability, string Severity, string Band, System.Text.Json.JsonElement? LegalRefs);

public sealed record SamplePageDto(Guid Id, string? Title, string Url, string? Language);

public sealed record ExampleFixDto(Guid ProposalId, Guid? FindingId, string OriginalText, string ProposedText, string RecheckStatus);
