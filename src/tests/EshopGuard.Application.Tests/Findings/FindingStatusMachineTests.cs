using EshopGuard.Application.Findings;
using EshopGuard.Application.Problems;
using EshopGuard.Data.Entities.Checks;

namespace EshopGuard.Application.Tests.Findings;

/// <summary>The states of a finding (change 11, task 2.2; AD 1): all 81 pairs of states against the table.</summary>
public sealed class FindingStatusMachineTests
{
    private static readonly HashSet<(FindingStatus, FindingStatus)> Table =
    [
        (FindingStatus.Open, FindingStatus.NeedsAnswer),
        (FindingStatus.Open, FindingStatus.Proposed),
        (FindingStatus.NeedsAnswer, FindingStatus.KeptWithEvidence),
        (FindingStatus.NeedsAnswer, FindingStatus.Proposed),
        (FindingStatus.NeedsAnswer, FindingStatus.Open),
        (FindingStatus.Proposed, FindingStatus.Approved),
        (FindingStatus.Proposed, FindingStatus.Open),
        (FindingStatus.Approved, FindingStatus.Proposed),
        (FindingStatus.Approved, FindingStatus.Published),
        (FindingStatus.Published, FindingStatus.Approved),
        (FindingStatus.Approved, FindingStatus.Resolved),
        (FindingStatus.Published, FindingStatus.Resolved),
        (FindingStatus.Open, FindingStatus.Kept),
        (FindingStatus.NeedsAnswer, FindingStatus.Kept),
        (FindingStatus.Proposed, FindingStatus.Kept),
        (FindingStatus.Open, FindingStatus.Dismissed),
        (FindingStatus.NeedsAnswer, FindingStatus.Dismissed),
        (FindingStatus.Proposed, FindingStatus.Dismissed),
        (FindingStatus.Kept, FindingStatus.Open),
        (FindingStatus.KeptWithEvidence, FindingStatus.Open),
        (FindingStatus.Dismissed, FindingStatus.Open),
        (FindingStatus.Resolved, FindingStatus.Open),
    ];

    public static TheoryData<FindingStatus, FindingStatus> AllPairs()
    {
        var data = new TheoryData<FindingStatus, FindingStatus>();
        foreach (var from in Enum.GetValues<FindingStatus>())
        {
            foreach (var to in Enum.GetValues<FindingStatus>())
            {
                data.Add(from, to);
            }
        }

        return data;
    }

    [Fact]
    public void ThereAreNineStates_And81Pairs() => Assert.Equal(81, AllPairs().Count);

    [Theory]
    [MemberData(nameof(AllPairs))]
    public void Transition_MatchesTheTable(FindingStatus from, FindingStatus to)
    {
        var expected = Table.Contains((from, to));
        Assert.Equal(expected, FindingStatusMachine.IsAllowed(from, to));
        if (expected)
        {
            FindingStatusMachine.Ensure(from, to);
            return;
        }

        var error = Assert.Throws<DomainException>(() => FindingStatusMachine.Ensure(from, to));
        Assert.Equal("finding.transition_not_allowed", error.Code);
        Assert.Equal(409, error.Status);
        Assert.Equal(FindingStatusMachine.Text(from), error.Parameters["from"]);
        Assert.Equal(FindingStatusMachine.Text(to), error.Parameters["to"]);
    }

    [Fact]
    public void PublishedCannotBeKept()
    {
        var error = Assert.Throws<DomainException>(() => FindingStatusMachine.Ensure(FindingStatus.Published, FindingStatus.Kept));
        Assert.Equal("published", error.Parameters["from"]);
        Assert.Equal("kept", error.Parameters["to"]);
    }
}
