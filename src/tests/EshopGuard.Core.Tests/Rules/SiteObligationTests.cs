using EshopGuard.Core.Models;
using EshopGuard.Core.Options;
using EshopGuard.Core.Rules;
using Microsoft.Extensions.DependencyInjection;

namespace EshopGuard.Core.Tests;

/// <summary>
/// Obligations of the whole site by jurisdiction (change 6): met, missing, upcoming or not checked with a reason; an
/// obligation that could not be checked is never met.
/// </summary>
public sealed class SiteObligationTests
{
    [Fact]
    public void TwoJurisdictions_AreSideBySide()
    {
        var sets = TestTexts.Catalog.RuleSets.Where(s => s.Name is "legal_sk" or "legal_cz").ToList();
        var paragraph = new Segment
        {
            Hash = "sha256:adr",
            Kind = SegmentKind.LegalParagraph,
            Text = "Spory\nMimosoudní řešení sporů zajišťuje Česká obchodní inspekce, www.coi.cz.",
            Urls = ["https://shop.example/obchodni-podminky"],
            Probabilities = new Dictionary<string, double>
            {
                [QuestionKey.Of("legal_cz", "legal_adr")] = 0.95,
                [QuestionKey.Of("legal_sk", "legal_adr")] = 0.10,
            },
        };

        var output = RuleEngine.Evaluate(new RuleEngineInput
        {
            RuleSets = sets, Labels = TestTexts.Catalog.Labels, Segments = [paragraph], Jurisdictions = ["sk", "cz"], AsOf = new DateOnly(2026, 10, 1),
        });

        var cz = Assert.Single(output.SiteObligations, o => o.Jurisdiction == "cz" && o.RuleId == "legal_adr_missing");
        Assert.Equal(ObligationStatus.Met, cz.Status);
        Assert.Equal(["https://shop.example/obchodni-podminky"], cz.Urls);
        Assert.Equal(ObligationStatus.Missing, Assert.Single(output.SiteObligations, o => o.Jurisdiction == "sk" && o.RuleId == "legal_adr_missing").Status);
        Assert.Equal(["sk"], Assert.Single(output.Findings, f => f.RuleId == "legal_adr_missing").Verdicts.Select(v => v.Jurisdiction));
    }

    [Fact]
    public async Task CheckOfTexts_DoesNotCheckSignsOfTheWholeSite()
    {
        await using var provider = TestServices.Create(FileSystemPageFetcher.ForSlovakFixture());
        var result = await provider.GetRequiredService<IEshopGuard>().AnalyzeTextsAsync(
            [new TextInput { Kind = TextKind.Legal, Text = "Odstúpenie od zmluvy\nSpotrebiteľ môže odstúpiť od zmluvy do 14 dní." }],
            new AnalyzeOptions { Jurisdictions = ["sk"] },
            TestContext.Current.CancellationToken);

        var signal = Assert.Single(result.SiteObligations, o => o.RuleId == "legal_withdrawal_function_missing");
        Assert.Equal(ObligationStatus.NotChecked, signal.Status);
        Assert.Equal(EngineCodes.SiteSignalsNotEvaluated, signal.Reason);
        Assert.DoesNotContain(result.SiteObligations, o => o.Status == ObligationStatus.Met && o.RuleId == "legal_withdrawal_function_missing");
    }

    [Fact]
    public void LegalPagesNotLoaded_AreMissingToReviewNeverMet()
    {
        var set = TestTexts.Catalog.RuleSets.Single(s => s.Name == "legal_sk");
        var output = RuleEngine.Evaluate(new RuleEngineInput
        {
            RuleSets = [set],
            Labels = TestTexts.Catalog.Labels,
            Segments = [],
            Jurisdictions = ["sk"],
            AsOf = new DateOnly(2026, 10, 1),
            TextNotLoadedPages = [new PageInfo { Url = "https://shop.example/obchodne-podmienky", Type = PageType.Legal, TextNotLoaded = true }],
        });

        var presence = output.SiteObligations.Where(o => set.Rules.Single(r => r.Id == o.RuleId).Scope == "site_presence").ToList();
        Assert.NotEmpty(presence);
        Assert.All(presence, o => Assert.Equal(ObligationStatus.Missing, o.Status));
        var adr = Assert.Single(output.Findings, f => f.RuleId == "legal_adr_missing");
        Assert.Equal(FindingBand.Review, adr.Band);
        Assert.Contains(adr.Strictest.Notes, n => n.Code == EngineCodes.LegalPagesNotLoaded);
    }

    [Fact]
    public void EvaluationNotConfirmed_LeavesEveryObligationNotChecked()
    {
        var sets = TestTexts.Catalog.RuleSets.Where(s => s.Name is "legal_sk" or "legal_cz").ToList();

        var obligations = RuleEngine.NotChecked(sets, ["cz"], EngineCodes.EvaluationSkipped);

        Assert.NotEmpty(obligations);
        Assert.All(obligations, o => Assert.Equal(("cz", ObligationStatus.NotChecked, EngineCodes.EvaluationSkipped), (o.Jurisdiction, o.Status, o.Reason)));
    }
}
