using EshopGuard.Core.Fix;
using EshopGuard.Core.Models;
using EshopGuard.Core.Rules;

namespace EshopGuard.Core.Tests;

/// <summary>
/// The strictest verdict of a finding (proposal of change 6, decision 3): a verdict that applies before an upcoming one,
/// then the group, the severity, the band and the jurisdiction.
/// </summary>
public sealed class VerdictOrderTests
{
    [Fact]
    public void ViolationByText_IsStricterThanAssessment()
    {
        var finding = Finding(Verdict("sk", "text", "high"), Verdict("cz", "assess", "high"));

        Assert.Equal("sk", finding.Strictest.Jurisdiction);
        Assert.Equal("text", finding.Checkability);
    }

    [Fact]
    public void SameGroup_SeverityDecides()
    {
        var high = Finding(Verdict("sk", "verify", "high"));
        var medium = Finding(Verdict("sk", "verify", "medium"));

        var ordered = new[] { medium, high }.OrderBy(f => f.Strictest, Comparer<JurisdictionVerdict>.Create(VerdictOrder.Compare)).ToList();

        Assert.Same(high, ordered[0]);
    }

    [Fact]
    public void UpcomingObligation_DoesNotMakeTheFindingStricter()
    {
        var finding = Finding(Verdict("cz", "text", "high", VerdictStatus.Upcoming), Verdict("sk", "verify", "medium"));

        Assert.Equal("sk", finding.Strictest.Jurisdiction);
        Assert.Equal("verify", finding.Checkability);
    }

    [Fact]
    public void EqualVerdicts_AreOrderedByJurisdiction()
    {
        var sorted = VerdictOrder.Sort([Verdict("sk", "text", "high"), Verdict("cz", "text", "high")]);

        Assert.Equal(["cz", "sk"], sorted.Select(v => v.Jurisdiction));
    }

    [Fact]
    public void HighBand_IsStricterThanReview()
    {
        var finding = Finding(Verdict("cz", "text", "high", band: FindingBand.Review), Verdict("sk", "text", "high"));

        Assert.Equal("sk", finding.Strictest.Jurisdiction);
    }

    [Theory]
    [InlineData("verify", VerdictStatus.Finding, "assess", VerdictStatus.Finding, "Remaining")]
    [InlineData("verify", VerdictStatus.Finding, null, null, "Verify")]
    [InlineData("text", VerdictStatus.Upcoming, null, null, "Upcoming")]
    [InlineData("text", VerdictStatus.Upcoming, "verify", VerdictStatus.Finding, "Verify")]
    public void CheckOfARewrite_KeepsTheChangeOpenWhileAnyJurisdictionStillFindsIt(
        string sk, VerdictStatus skStatus, string? cz, VerdictStatus? czStatus, string expected)
    {
        var verdicts = new List<JurisdictionVerdict> { Verdict("sk", sk, "high", skStatus) };
        if (cz is not null)
        {
            verdicts.Add(Verdict("cz", cz, "high", czStatus!.Value));
        }

        Assert.Equal(expected, Fix.PageRewriter.CheckGroup(Finding([.. verdicts])).ToString());
    }

    private static Finding Finding(params JurisdictionVerdict[] verdicts) => new()
    {
        RuleId = "rule", Module = "ucp", Scope = "segment", Text = "Text.", Verdicts = verdicts,
    };

    private static JurisdictionVerdict Verdict(string jurisdiction, string checkability, string severity, VerdictStatus status = VerdictStatus.Finding, FindingBand band = FindingBand.High) => new()
    {
        Jurisdiction = jurisdiction,
        Checkability = checkability,
        Severity = severity,
        Status = status,
        Band = band,
        Score = 0.9,
        RuleSet = "ucp",
        RuleSetVersion = "test",
    };
}
