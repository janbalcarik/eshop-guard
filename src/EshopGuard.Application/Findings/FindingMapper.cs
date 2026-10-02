using EshopGuard.Jobs.Fixes;
using EshopGuard.Application.Contracts;
using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Checks;
using EshopGuard.Data.Entities.Fixes;

namespace EshopGuard.Application.Findings;

/// <summary>Findings, pages and questions as the API returns them (codes, never sentences of the tool).</summary>
public static class FindingMapper
{
    public static PageRefDto? Page(WorkPage? page) => page is null ? null : new PageRefDto(page.Id, page.Title, page.Path, page.Url, page.Language);

    public static FindingListItemDto Item(WorkFinding finding, WorkPage? page, FixProposal? proposal, Guid? questionId)
    {
        ArgumentNullException.ThrowIfNull(finding);
        var verdicts = VerdictStrictness.Verdicts(finding.Verdicts);
        return new FindingListItemDto(
            finding.Id, finding.RuleSetId, finding.RuleId, finding.Module, Text(finding.Scope), FindingStatusMachine.Text(finding.Status), finding.Text,
            Page(page), finding.Occurrences, verdicts, verdicts.FirstOrDefault(), finding.Params?.RootElement.Clone(),
            proposal is null ? null : new FindingProposalRefDto(proposal.Id, Text(proposal.Status), Text(ProposalText.Recheck(proposal))),
            questionId);
    }

    /// <summary>A finding of a list from its row.</summary>
    public static WorkFinding Work(Finding finding)
    {
        ArgumentNullException.ThrowIfNull(finding);
        return new WorkFinding(finding.Id, finding.RuleSetId, finding.RuleId, finding.Module, finding.Scope, finding.Status, finding.SegmentHash, finding.PageId,
            finding.Text, finding.Verdicts, finding.Params, finding.Occurrences, VerdictStrictness.Rank(finding.Verdicts));
    }

    public static QuestionDto Question(Question question, WorkFinding? finding, WorkPage? page, AppliesToDto appliesTo, string? answeredBy)
    {
        ArgumentNullException.ThrowIfNull(question);
        return new QuestionDto(
            question.Id, Text(question.Scope), question.Code, question.Params?.RootElement.Clone(), finding?.RuleSetId, finding?.RuleId, question.FindingId,
            Page(page), Text(question.Status), question.Answer, answeredBy, question.AnsweredAt, appliesTo);
    }

    public static string Text<T>(T value)
        where T : struct, Enum => SnakeCaseEnumConverter<T>.ToText(value);
}
