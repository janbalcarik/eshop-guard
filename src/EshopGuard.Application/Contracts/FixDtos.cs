using System.Text.Json;

namespace EshopGuard.Application.Contracts;

/// <summary>A variant of a proposal and the state of its recheck.</summary>
public sealed record AlternativeDto(string Key, string Text, string RecheckStatus);

/// <summary>A fact the merchant fills in; the value goes in exactly as written.</summary>
public sealed record PlaceholderDto(string Key, string? Value);

/// <summary>The recheck of the current text: <c>pending</c>, <c>ok</c> or <c>still_finding</c> with the countries and rules that still find something.</summary>
public sealed record RecheckDto(string Status, IReadOnlyList<string>? Jurisdictions, IReadOnlyList<string>? RuleIds);

/// <summary>The bulk fix a change belongs to: on how many pages, where it fits and where it must be fixed one by one.</summary>
public sealed record ChangeGroupDto(Guid Id, int PageCount, int FitsCount, int IndividualCount, string Status);

/// <summary>
/// A change on a page („Zmena 3 z 5“, change 11, AD 4): replace, remove, a question or a group, with the original and the new
/// text in its context, variants, facts to fill in, verdicts by country and the state of the recheck; <see cref="Version"/>
/// is the ETag of the proposal.
/// </summary>
public sealed record ChangeDto(
    int Index,
    string Kind,
    Guid? ProposalId,
    Guid? QuestionId,
    Guid? GroupId,
    IReadOnlyList<Guid> FindingIds,
    string RuleId,
    Guid RuleSetId,
    IReadOnlyList<VerdictDto> Verdicts,
    VerdictDto? Strictest,
    string OriginalText,
    string? ProposedText,
    string? Text,
    string? ContextBefore,
    string? ContextAfter,
    IReadOnlyList<AlternativeDto> Alternatives,
    string? SelectedAlternative,
    string? EditedText,
    IReadOnlyList<PlaceholderDto> Placeholders,
    ChangeGroupDto? Group,
    RecheckDto Recheck,
    string Status,
    uint? Version,
    string? QuestionCode,
    JsonElement? QuestionParams);

/// <summary>Where the text of a page comes from: a connector (with the number of the product), crawling or a feed.</summary>
public sealed record PageSourceDto(string Kind, string? Platform, string? ExternalId);

/// <summary>The page of a review.</summary>
public sealed record ReviewPageDto(Guid Id, string? Title, string Url, string? Language, PageSourceDto Source);

/// <summary>The place of the page in the queue of a tab („Stránka 1 z 24“, „Ďalšia: …“).</summary>
public sealed record ReviewPositionDto(string Tab, int Index, int Total, Guid? PrevPageId, Guid? NextPageId, string? NextTitle);

/// <summary>Accepted changes of the page, all changes, groups still waiting for a fact.</summary>
public sealed record ReviewSummaryDto(int Accepted, int Total, int GroupsAwaitingValue);

/// <summary>Whether the page can be published into the e-shop, and if not, why (<c>no_connector</c>, <c>connector_error</c>, <c>read_only</c>, <c>publisher_missing</c>, <c>nothing_accepted</c>).</summary>
public sealed record PublishAvailabilityDto(bool Available, string? ReasonCode, string? Platform, int AcceptedCount);

/// <summary><c>GET …/pages/{pageId}/review</c>: every change of the page in the order of its text.</summary>
public sealed record PageReviewDto(
    ReviewPageDto Page,
    ReviewPositionDto Position,
    IReadOnlyList<ChangeDto> Changes,
    int UnchangedBlocks,
    bool TemplateNote,
    ReviewSummaryDto Summary,
    PublishAvailabilityDto Publish);

/// <summary>A proposal of a fix (for polling its recheck and after a change); <see cref="Version"/> is its ETag.</summary>
public sealed record ProposalDto(
    Guid Id,
    Guid PageId,
    Guid? GroupId,
    IReadOnlyList<Guid> FindingIds,
    string Field,
    int? BlockIndex,
    string OriginalText,
    string ProposedText,
    string Text,
    IReadOnlyList<AlternativeDto> Alternatives,
    string? SelectedAlternative,
    string? EditedText,
    IReadOnlyList<PlaceholderDto> Placeholders,
    RecheckDto Recheck,
    string Status,
    uint Version);

/// <summary>
/// <c>POST S/questions/{questionId}/answer</c>: how many questions, findings and pages the answer changed, the evidence of
/// „Áno“ and whether a proposal is being generated after „Nie“.
/// </summary>
public sealed record AnswerResultDto(int AffectedQuestions, int AffectedFindings, int AffectedPages, Guid? EvidenceId, bool GenerationPending);
