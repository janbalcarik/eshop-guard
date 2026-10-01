using EshopGuard.Core.Models;
using EshopGuard.Core.Rules;

namespace EshopGuard.Core.Tests;

/// <summary>
/// The Czech button „Odstoupit od smlouvy“ (§ 1830a OZ as amended by Act No. 159/2026 Coll., effective 1. 1. 2027) with fixed
/// signals of pages: met with a link of that label, a finding to verify without it, upcoming before the date, not run for
/// Slovakia alone.
/// </summary>
public sealed class CzWithdrawalButtonTests
{
    private static readonly DateOnly AfterEffect = new(2027, 1, 2);

    [Fact]
    public void LinkWithTheLabel_IsMet()
    {
        var output = Evaluate(["cz"], AfterEffect, Signals("Odstoupit od smlouvy https://shop.example/ucet/odstoupeni"));

        var obligation = Assert.Single(output.SiteObligations, o => o.RuleId == "legal_withdrawal_function_missing");
        Assert.Equal(ObligationStatus.Met, obligation.Status);
        Assert.Equal(["https://shop.example/"], obligation.Urls);
        Assert.DoesNotContain(output.Findings, f => f.RuleId == "legal_withdrawal_function_missing");
    }

    [Fact]
    public void LinkToInformationAboutWithdrawal_IsNotTheButton()
    {
        var output = Evaluate(["cz"], AfterEffect, Signals("Odstoupení od smlouvy https://shop.example/odstoupeni\nJak odstoupit od smlouvy https://shop.example/jak"));

        Assert.Single(output.Findings, f => f.RuleId == "legal_withdrawal_function_missing");
    }

    [Fact]
    public void MissingButton_IsAFindingToVerifyFromTheFirstOfJanuary()
    {
        var output = Evaluate(["cz"], AfterEffect, Signals("Obchodní podmínky https://shop.example/vop"));

        var verdict = Assert.Single(Assert.Single(output.Findings, f => f.RuleId == "legal_withdrawal_function_missing").Verdicts);
        Assert.Equal(("cz", VerdictStatus.Finding, "verify", "legal_cz"), (verdict.Jurisdiction, verdict.Status, verdict.Checkability, verdict.RuleSet));
        Assert.Contains(verdict.Notes, n => n.Code == EngineCodes.SignalCartNotDownloaded);
        Assert.All(verdict.LegalRefs, r => Assert.Equal(EngineCodes.ToVerify, r.Status));
        Assert.Contains(verdict.LegalRefs, r => r.Ref.StartsWith("§ 1830a odst. 1 a 2 zákona č. 89/2012 Sb.", StringComparison.Ordinal));
    }

    [Fact]
    public void BeforeTheFirstOfJanuary_ItIsUpcoming()
    {
        var output = Evaluate(["cz"], new DateOnly(2026, 10, 1), Signals("Obchodní podmínky https://shop.example/vop"));

        var verdict = Assert.Single(Assert.Single(output.Findings, f => f.RuleId == "legal_withdrawal_function_missing").Verdicts);
        Assert.Equal((VerdictStatus.Upcoming, FindingBand.Review, new DateOnly(2027, 1, 1)), (verdict.Status, verdict.Band, verdict.EffectiveFrom));
        Assert.Equal(ObligationStatus.Upcoming, Assert.Single(output.SiteObligations, o => o.RuleId == "legal_withdrawal_function_missing").Status);
    }

    [Fact]
    public void ShopOnlyForSlovakia_DoesNotRunTheCzechRule()
    {
        var output = Evaluate(["sk"], AfterEffect, Signals("Obchodní podmínky https://shop.example/vop"));

        Assert.DoesNotContain(output.SiteObligations, o => o.RuleSet == "legal_cz");
        Assert.All(output.Findings.SelectMany(f => f.Verdicts), v => Assert.Equal("sk", v.Jurisdiction));
    }

    [Fact]
    public void BothCountries_GiveOneFindingWithTwoVerdicts()
    {
        var output = Evaluate(["sk", "cz"], AfterEffect, Signals("Obchodní podmínky https://shop.example/vop"));

        var finding = Assert.Single(output.Findings, f => f.RuleId == "legal_withdrawal_function_missing");
        Assert.Equal(["cz", "sk"], finding.Verdicts.Select(v => v.Jurisdiction).Order());
        Assert.Equal("Chybí tlačítko „Odstoupit od smlouvy“ (povinné od 1. 1. 2027)",
            TestTexts.Renderer.Render(finding, finding.Verdicts.Single(v => v.Jurisdiction == "cz"), "cs").Title);
    }

    [Fact]
    public void InformationAboutTheButton_IsAskedInLegalTexts()
    {
        var set = TestTexts.Catalog.RuleSets.Single(s => s.Name == "legal_cz");

        Assert.Equal("legal-cz-2026-10-01-draft4", set.Version);
        Assert.Equal("legal_withdrawal_online_option", set.Rules.Single(r => r.Id == "legal_withdrawal_button_info_missing").Question);
        Assert.Equal("legal_withdrawal_button_location", set.Rules.Single(r => r.Id == "legal_withdrawal_button_location_missing").Question);
        Assert.All(set.Rules.Where(r => r.Id.Contains("button", StringComparison.Ordinal)), r => Assert.Equal(new DateOnly(2027, 1, 1), r.EffectiveFromFor("cz")));
    }

    private static Dictionary<string, PageSignals> Signals(string links) => new() { ["https://shop.example/"] = new("Úvod", links, "") };

    private static RuleEngineOutput Evaluate(IReadOnlyList<string> jurisdictions, DateOnly asOf, Dictionary<string, PageSignals> signals) =>
        RuleEngine.Evaluate(new RuleEngineInput
        {
            RuleSets = TestTexts.Catalog.RuleSets.Where(s => s.Module == "legal" && s.Enabled).ToList(),
            Labels = TestTexts.Catalog.Labels,
            Segments = [],
            Jurisdictions = jurisdictions,
            AsOf = asOf,
            PageSignals = signals,
            EvaluateSiteSignals = true,
            EvaluateSitePresence = false,
        });
}
