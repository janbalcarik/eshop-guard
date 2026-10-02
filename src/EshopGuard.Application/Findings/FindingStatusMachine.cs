using EshopGuard.Application.Problems;
using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Checks;

namespace EshopGuard.Application.Findings;

/// <summary>
/// The states of a finding and the allowed transitions (change 11, AD 1), as a pure function. A finding of the whole site
/// follows the same table. Any other transition is <c>409 finding.transition_not_allowed</c> with <c>params.from</c> and
/// <c>params.to</c>; the services write the audit <c>finding.status_changed</c> for every transition they make.
/// </summary>
public static class FindingStatusMachine
{
    private static readonly Dictionary<FindingStatus, FindingStatus[]> Allowed = new()
    {
        // a question arose; a proposal passed its recheck; „Ponechať“; „Nejde o problém“
        [FindingStatus.Open] = [FindingStatus.NeedsAnswer, FindingStatus.Proposed, FindingStatus.Kept, FindingStatus.Dismissed],
        // „Áno“; „Nie“ with a ready variant; „Nie“ while a proposal is generated; „Ponechať“; „Nejde o problém“
        [FindingStatus.NeedsAnswer] = [FindingStatus.KeptWithEvidence, FindingStatus.Proposed, FindingStatus.Open, FindingStatus.Kept, FindingStatus.Dismissed],
        // all proposals accepted; the only proposal rejected; „Ponechať“; „Nejde o problém“
        [FindingStatus.Proposed] = [FindingStatus.Approved, FindingStatus.Open, FindingStatus.Kept, FindingStatus.Dismissed],
        // acceptance taken back; published (change 15); the next check did not find it (change 16)
        [FindingStatus.Approved] = [FindingStatus.Proposed, FindingStatus.Published, FindingStatus.Resolved],
        // publication rolled back (change 15); the next check did not find it (change 16)
        [FindingStatus.Published] = [FindingStatus.Approved, FindingStatus.Resolved],
        // „Znovu otvoriť“; the evidence expired or was deleted
        [FindingStatus.Kept] = [FindingStatus.Open],
        [FindingStatus.KeptWithEvidence] = [FindingStatus.Open],
        [FindingStatus.Dismissed] = [FindingStatus.Open],
        // the text came back (change 16)
        [FindingStatus.Resolved] = [FindingStatus.Open],
    };

    public static bool IsAllowed(FindingStatus from, FindingStatus to) => Allowed.TryGetValue(from, out var targets) && targets.Contains(to);

    /// <summary>Throws <c>409 finding.transition_not_allowed</c> unless the transition is in the table.</summary>
    public static void Ensure(FindingStatus from, FindingStatus to)
    {
        if (!IsAllowed(from, to))
        {
            throw NotAllowed(from, to);
        }
    }

    public static DomainException NotAllowed(FindingStatus from, FindingStatus to) => new(ProblemCodes.FindingTransitionNotAllowed, 409,
        new Dictionary<string, object?> { ["from"] = Text(from), ["to"] = Text(to) });

    /// <summary>Snake_case text of a state, as stored and returned.</summary>
    public static string Text(FindingStatus status) => SnakeCaseEnumConverter<FindingStatus>.ToText(status);

    /// <summary>States in which a finding still waits for the merchant (<c>to_resolve</c>).</summary>
    public static readonly FindingStatus[] Unresolved = [FindingStatus.Open, FindingStatus.NeedsAnswer, FindingStatus.Proposed];

    /// <summary>States of a fixed finding (the tab „Opravené“).</summary>
    public static readonly FindingStatus[] Fixed = [FindingStatus.Published, FindingStatus.Resolved];
}
