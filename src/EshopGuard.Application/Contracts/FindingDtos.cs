using System.Text.Json;

namespace EshopGuard.Application.Contracts;

/// <summary>A page of a list read by a cursor; <see cref="NextCursor"/> is null on the last page.</summary>
public sealed record CursorPage<T>(IReadOnlyList<T> Items, string? NextCursor, int Total);

/// <summary>A page of an e-shop in a list (title, path, address, language of its version).</summary>
public sealed record PageRefDto(Guid Id, string? Title, string? Path, string Url, string? Language);

/// <summary>Explanation of a rule: the general one and the own one of a jurisdiction whose law differs.</summary>
public sealed record RuleExplanationDto(string Default, IReadOnlyDictionary<string, string> ByJurisdiction);

/// <summary>A code of „Čo pomôže“ with its text from the rule.</summary>
public sealed record RuleHelpDto(string Code, string Text);

/// <summary>Texts of one rule in the language of the catalog.</summary>
public sealed record RuleTextDto(
    string Title, RuleExplanationDto Explanation, string Recommendation, IReadOnlyList<RuleHelpDto> Helps, IReadOnlyDictionary<string, string> Questions);

/// <summary>Texts of the rules of one rule set; <see cref="Locale"/> is the language actually used (a reviewed translation or the original).</summary>
public sealed record RuleSetTextsDto(Guid RuleSetId, string Module, string Version, string Locale, IReadOnlyDictionary<string, RuleTextDto> Rules);

/// <summary><c>GET /api/catalog/rule-texts</c> (change 11, AD 2).</summary>
public sealed record RuleTextCatalogDto(string Locale, IReadOnlyList<RuleSetTextsDto> RuleSets);

/// <summary>The proposal of a finding in a list: its state and the state of its recheck.</summary>
public sealed record FindingProposalRefDto(Guid Id, string Status, string RecheckStatus);

/// <summary>A finding in a list: codes, verdicts by country (the strictest first) and where it is; the text is a copy from the customer's web.</summary>
public sealed record FindingListItemDto(
    Guid FindingId,
    Guid RuleSetId,
    string RuleId,
    string Module,
    string Scope,
    string Status,
    string? Text,
    PageRefDto? Page,
    int Occurrences,
    IReadOnlyList<VerdictDto> Verdicts,
    VerdictDto? Strictest,
    JsonElement? Params,
    FindingProposalRefDto? Proposal,
    Guid? QuestionId);

/// <summary>A change of the state of a finding from the audit (who and when).</summary>
public sealed record FindingHistoryDto(DateTimeOffset At, string Action, string? From, string? To, string? ReasonCode, FindingActorDto? Actor);

/// <summary>Who made a decision.</summary>
public sealed record FindingActorDto(Guid UserId, string? DisplayName);

/// <summary>A proposal of a finding in its detail.</summary>
public sealed record FindingProposalDto(Guid Id, Guid PageId, string Status, string RecheckStatus, string OriginalText, string Text);

/// <summary>A piece of evidence linked to a finding.</summary>
public sealed record FindingEvidenceDto(Guid Id, string Kind, string? Title, string Status, DateTimeOffset? ValidUntil);

/// <summary><c>GET …/findings/{findingId}</c>: the finding, its context on the page, its pages, questions, proposals, evidence and history.</summary>
public sealed record FindingDetailDto(
    FindingListItemDto Finding,
    string? ContextBefore,
    string? ContextAfter,
    IReadOnlyList<PageRefDto> OccurrencePages,
    IReadOnlyList<QuestionDto> Questions,
    IReadOnlyList<FindingProposalDto> Proposals,
    IReadOnlyList<FindingEvidenceDto> Evidence,
    IReadOnlyList<FindingHistoryDto> History);

/// <summary>The tabs of the findings (Porušenia, Na posúdenie, Na overenie by the strictest verdict; Opravené = published + resolved).</summary>
public sealed record FindingTabsDto(int Text, int Assess, int Verify, int Fixed);

/// <summary>The tabs of the pages (AD 3) and every item with a finding („4 z 28“).</summary>
public sealed record PageTabsDto(int ToResolve, int ToApprove, int NeedsAnswer, int Published, int WithFindings);

/// <summary>Findings of an item by the group of their strictest verdict.</summary>
public sealed record CheckCountsDto(int Text, int Assess, int Verify);

/// <summary>Work of an item: proposals ready to approve, accepted proposals, open questions.</summary>
public sealed record WorkCountsDto(int Proposals, int Accepted, int OpenQuestions);

/// <summary>What an item shows in the list: a change (before → after) or the code of a question.</summary>
public sealed record WorkPreviewDto(string Kind, string? Before, string? After, string? QuestionCode, JsonElement? QuestionParams);

/// <summary>
/// An item of „Opravy“ by pages: a page, or the whole e-shop (<c>site_template</c> „Celý e-shop: šablóna“,
/// <c>site_obligations</c> „Celý e-shop: košík a objednávka“); <see cref="Status"/> is <c>needs_answer</c>, <c>to_approve</c>,
/// <c>open</c>, <c>published</c> or <c>decided</c> (every finding kept or dismissed).
/// </summary>
public sealed record PageWorkItemDto(
    string Kind,
    Guid? PageId,
    string? Title,
    string? Path,
    string? Url,
    string? Language,
    string? PageType,
    CheckCountsDto Counts,
    WorkCountsDto Work,
    WorkPreviewDto? Preview,
    string Status,
    VerdictDto? Strictest);

/// <summary>The last finished check of the e-shop for the overview.</summary>
public sealed record OverviewRunDto(Guid RunId, string Kind, string Status, DateTimeOffset? FinishedAt, int PagesChecked, NotCheckedDto NotChecked, IReadOnlyList<string> Jurisdictions);

/// <summary>Counts of findings for the overview.</summary>
public sealed record FindingCountsByGroupDto(int Total, int Text, int Assess, int Verify);

/// <summary>Quick answers: the number of open questions and at most 3 of them.</summary>
public sealed record QuickQuestionsDto(int Total, IReadOnlyList<QuestionDto> Items);

/// <summary>Counts of the side menu: items to approve, evidence ending or waiting for an answer; monitoring comes with change 16.</summary>
public sealed record BadgesDto(int Fixes, int Evidence, int? Monitoring);

/// <summary><c>GET …/overview</c>: the last check, counts, tabs, at most 5 items to solve first and at most 3 quick answers.</summary>
public sealed record OverviewDto(
    OverviewShopDto Shop,
    OverviewRunDto? LastRun,
    FindingCountsByGroupDto FindingCounts,
    PageTabsDto PageTabs,
    IReadOnlyList<PageWorkItemDto> PriorityPages,
    QuickQuestionsDto QuickQuestions,
    BadgesDto Badges,
    object? Monitoring);

/// <summary>The e-shop of the overview.</summary>
public sealed record OverviewShopDto(Guid Id, string Domain);

/// <summary><c>GET …/search</c>: at most 10 pages and 10 findings.</summary>
public sealed record SearchResultDto(IReadOnlyList<PageRefDto> Pages, IReadOnlyList<FindingListItemDto> Findings);

/// <summary>How many questions, findings and pages an answer applies to.</summary>
public sealed record AppliesToDto(int Questions, int Findings, int Pages);

/// <summary>A question for the merchant („Áno / Nie“), by finding or for the whole e-shop.</summary>
public sealed record QuestionDto(
    Guid QuestionId,
    string Scope,
    string Code,
    JsonElement? Params,
    Guid? RuleSetId,
    string? RuleId,
    Guid? FindingId,
    PageRefDto? Page,
    string Status,
    string? Answer,
    string? AnsweredBy,
    DateTimeOffset? AnsweredAt,
    AppliesToDto AppliesTo);
