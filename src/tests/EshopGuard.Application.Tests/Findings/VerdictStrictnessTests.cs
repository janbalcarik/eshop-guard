using System.Text.Json;
using EshopGuard.Application.Findings;
using EshopGuard.Core.Models;
using EshopGuard.Core.Rules;

namespace EshopGuard.Application.Tests.Findings;

/// <summary>
/// The strictest verdict (change 11, task 2.3; AD 2): the order of <see cref="VerdictOrder"/> of the library over the verdicts
/// as stored in <c>checks.findings.verdicts</c>, and the same number as the SQL function <c>checks.strictness_rank</c>.
/// </summary>
public sealed class VerdictStrictnessTests
{
    private static JsonDocument Verdicts(params string[] verdicts) => JsonDocument.Parse("[" + string.Join(",", verdicts) + "]");

    private static string V(string jurisdiction, string checkability, string severity, string band, string status = "finding") =>
        $$"""{"jurisdiction":"{{jurisdiction}}","status":"{{status}}","band":"{{band}}","severity":"{{severity}}","checkability":"{{checkability}}","legal_refs":[{"ref":"§ 5","status":"to_verify","jurisdiction":"{{jurisdiction}}"}]}""";

    [Fact]
    public void SkTextHigh_IsStricterThan_CzAssessHigh()
    {
        using var verdicts = Verdicts(V("cz", "assess", "high", "high"), V("sk", "text", "high", "review"));

        var strictest = VerdictStrictness.Strictest(verdicts)!;

        Assert.Equal("sk", strictest.Jurisdiction);
        Assert.Equal("text", strictest.Checkability);
        Assert.Equal(["sk", "cz"], VerdictStrictness.Verdicts(verdicts).Select(v => v.Jurisdiction));
        Assert.True(strictest.LegalRefs.HasValue);
    }

    [Fact]
    public void SameGroup_OrdersBySeverity_ThenBand()
    {
        using var bySeverity = Verdicts(V("cz", "assess", "medium", "high"), V("sk", "assess", "high", "review"));
        using var byBand = Verdicts(V("cz", "assess", "high", "review"), V("sk", "assess", "high", "high"));

        Assert.Equal("sk", VerdictStrictness.Strictest(bySeverity)!.Jurisdiction);
        Assert.Equal("sk", VerdictStrictness.Strictest(byBand)!.Jurisdiction);
    }

    [Fact]
    public void UpcomingVerdict_NeverOutranksAValidOne()
    {
        using var verdicts = Verdicts(V("cz", "text", "high", "high", "upcoming"), V("sk", "verify", "low", "review"));

        var strictest = VerdictStrictness.Strictest(verdicts)!;

        Assert.Equal("sk", strictest.Jurisdiction);
        Assert.Equal("upcoming", VerdictStrictness.Verdicts(verdicts)[1].Status);
    }

    [Fact]
    public void NotCheckable_IsReturnedAsVerify_AndRanksAfterVerify()
    {
        using var verdicts = Verdicts(V("sk", "not_checkable", "high", "high"), V("cz", "verify", "low", "review"));

        Assert.Equal("cz", VerdictStrictness.Strictest(verdicts)!.Jurisdiction);
        Assert.Equal("verify", VerdictStrictness.Verdicts(verdicts)[1].Checkability);
    }

    [Fact]
    public void Rank_HasTheNumbersOfTheSqlFunction()
    {
        // text/low/review = 0*16 + 2*4 + 1 = 9; assess/high/high = 16; upcoming adds 64; no verdict = 127.
        using var verdicts = Verdicts(V("sk", "assess", "high", "high"), V("cz", "text", "low", "review"));
        using var upcoming = Verdicts(V("sk", "text", "high", "high", "upcoming"));
        using var empty = Verdicts();

        Assert.Equal(9, VerdictStrictness.Rank(verdicts));
        Assert.Equal(64, VerdictStrictness.Rank(upcoming));
        Assert.Equal(VerdictStrictness.None, VerdictStrictness.Rank(empty));
    }

    [Theory]
    [InlineData("text", "high", FindingBand.High, "assess", "high", FindingBand.High)]
    [InlineData("assess", "high", FindingBand.Review, "assess", "medium", FindingBand.High)]
    [InlineData("verify", "medium", FindingBand.High, "verify", "medium", FindingBand.Review)]
    [InlineData("assess", "low", FindingBand.High, "verify", "high", FindingBand.High)]
    public void Order_IsTheOrderOfTheLibrary(string checkA, string sevA, FindingBand bandA, string checkB, string sevB, FindingBand bandB)
    {
        var a = new JurisdictionVerdict { Jurisdiction = "sk", Checkability = checkA, Severity = sevA, Band = bandA, RuleSet = "eco", RuleSetVersion = "1" };
        var b = new JurisdictionVerdict { Jurisdiction = "cz", Checkability = checkB, Severity = sevB, Band = bandB, RuleSet = "eco", RuleSetVersion = "1" };
        using var json = Verdicts(V("sk", checkA, sevA, bandA == FindingBand.High ? "high" : "review"), V("cz", checkB, sevB, bandB == FindingBand.High ? "high" : "review"));

        Assert.True(VerdictOrder.Compare(a, b) < 0);
        Assert.Equal("sk", VerdictStrictness.Strictest(json)!.Jurisdiction);
    }
}
