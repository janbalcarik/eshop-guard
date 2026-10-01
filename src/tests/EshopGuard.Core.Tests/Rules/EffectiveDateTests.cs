using EshopGuard.Core.Models;
using EshopGuard.Core.Rules;

namespace EshopGuard.Core.Tests;

/// <summary>
/// Effective dates of rules by jurisdiction (change 6): before the date the rule is evaluated and the verdict is upcoming, in
/// the review band, with a note of the date; from the date it is a finding. Effective dates only for jurisdictions of the set.
/// </summary>
public sealed class EffectiveDateTests
{
    [Fact]
    public void ShippedSlovakWithdrawalFunction_AppliesOnFirstOctober()
    {
        var output = Evaluate(TestTexts.Catalog.RuleSets.Single(s => s.Name == "legal_sk"), new DateOnly(2026, 10, 1));

        var finding = Assert.Single(output.Findings, f => f.RuleId == "legal_withdrawal_function_missing");
        var verdict = Assert.Single(finding.Verdicts);
        Assert.Equal(VerdictStatus.Finding, verdict.Status);
        Assert.Equal(new DateOnly(2026, 6, 19), verdict.EffectiveFrom);
        Assert.DoesNotContain(verdict.Notes, n => n.Code == EngineCodes.EffectiveFrom);
    }

    [Fact]
    public void BeforeTheDate_TheVerdictIsUpcomingWithANoteOfTheDate()
    {
        var output = Evaluate(TestTexts.Catalog.RuleSets.Single(s => s.Name == "legal_sk"), new DateOnly(2026, 6, 1));

        var finding = Assert.Single(output.Findings, f => f.RuleId == "legal_withdrawal_function_missing");
        var verdict = Assert.Single(finding.Verdicts);
        Assert.Equal(VerdictStatus.Upcoming, verdict.Status);
        Assert.Equal(FindingBand.Review, verdict.Band);
        var note = Assert.Single(verdict.Notes, n => n.Code == EngineCodes.EffectiveFrom);
        Assert.Equal("Povinnost platí od 19. 6. 2026; do té doby jde o upozornění dopředu.", TestTexts.Renderer.Note(note, "cs"));
        Assert.Contains(output.SiteObligations, o => o.RuleId == "legal_withdrawal_function_missing" && o.Status == ObligationStatus.Upcoming);
    }

    [Fact]
    public void WithoutADate_EveryRuleApplies()
    {
        var output = Evaluate(TestTexts.Catalog.RuleSets.Single(s => s.Name == "legal_sk"), asOf: null);

        Assert.All(output.Findings.SelectMany(f => f.Verdicts), v => Assert.Equal(VerdictStatus.Finding, v.Status));
    }

    [Fact]
    public void EffectiveDateOfAJurisdictionOutsideTheSet_IsAnError()
    {
        var set = new RuleSet
        {
            Version = "test", SourceFile = "test.yaml", Module = "legal", AppliesTo = "legal_paragraph", Jurisdictions = ["sk"],
            Questions = new() { ["q"] = new QuestionDefinition { TextEn = "Q?", TextCs = "Q?" } },
            Rules = [new RuleDefinition { Id = "r", Scope = "site_presence", Question = "q", EffectiveFrom = new() { ["pl"] = new DateOnly(2027, 1, 1) } }],
        };

        var errors = RuleValidator.Validate([set], new LabelConfiguration(), jurisdictions: TestTexts.Catalog.Jurisdictions);

        Assert.Contains(errors, e => e.Contains("test.yaml", StringComparison.Ordinal) && e.Contains("„r“", StringComparison.Ordinal) && e.Contains("effective_from pro jurisdikci „pl“", StringComparison.Ordinal));
    }

    [Fact]
    public void ShippedRules_HaveTheDatesOfTheirReferences()
    {
        var sets = TestTexts.Catalog.RuleSets.ToDictionary(s => s.Name);

        Assert.All(sets["eco"].Rules.Concat(sets["dur"].Rules), r => Assert.Equal(new DateOnly(2026, 9, 27), r.EffectiveFromFor("sk")));
        Assert.Equal(new DateOnly(2026, 9, 27), sets["legal_sk"].Rules.Single(r => r.Id == "legal_harmonized_notice_missing").EffectiveFromFor("sk"));
        Assert.Null(sets["legal_sk"].Rules.Single(r => r.Id == "legal_adr_missing").EffectiveFromFor("sk"));
    }

    /// <summary>A site without any sign of the withdrawal function and without legal paragraphs.</summary>
    private static RuleEngineOutput Evaluate(RuleSet set, DateOnly? asOf) => RuleEngine.Evaluate(new RuleEngineInput
    {
        RuleSets = [set],
        Labels = TestTexts.Catalog.Labels,
        Segments = [],
        Jurisdictions = ["sk"],
        AsOf = asOf,
        PageSignals = new Dictionary<string, PageSignals> { ["https://shop.example/"] = new("Úvod", "Kontakt https://shop.example/kontakt", "") },
        EvaluateSiteSignals = true,
        EvaluateSitePresence = false,
    });
}
