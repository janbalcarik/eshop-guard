using EshopGuard.Core.Models;
using EshopGuard.Core.Options;
using EshopGuard.Core.Rules;
using Microsoft.Extensions.DependencyInjection;

namespace EshopGuard.Core.Tests;

/// <summary>
/// Evaluation of texts without crawling (<c>check-text</c> and product use before publication).
/// </summary>
public class AnalyzeTextsTests
{
    [Fact]
    public async Task Sentence_IsEvaluatedWithContextAndEveryRuleReportsItsOutcome()
    {
        var result = await AnalyzeAsync(new TextInput { Text = "Bylinný šampón. Tento šampón je ekologický a šetrný k prírode." }, "sk");

        var claim = Assert.Single(result.Segments, s => s.Text == "Tento šampón je ekologický a šetrný k prírode.");
        Assert.Equal("Bylinný šampón.", claim.ContextBefore);
        Assert.Contains(QuestionKey.Of("eco", "eco_generic"), claim.Probabilities.Keys);
        Assert.Contains(result.Findings, f => f.RuleId == "eco_generic_claim");

        // Every rule of the sentence modules (eco, dur, ucp) reports an outcome; legal rules are skipped without legal text.
        var outcomes = result.RuleResults.Where(r => r.SegmentHash == claim.Hash).Select(r => r.RuleId).ToList();
        Assert.Equal(outcomes.Distinct().Count(), outcomes.Count);
        Assert.Contains("eco_generic_claim", outcomes);
        Assert.Contains("eco_sustainable_claim", outcomes);
        Assert.Contains("dur_lifetime_claim", outcomes);
        Assert.Contains("ucp_reviews_verified_claim", outcomes);
        Assert.DoesNotContain("ucp_cure_claim", outcomes);
        Assert.All(outcomes, id => Assert.Contains(new[] { "eco_", "dur_", "ucp_" }, prefix => id.StartsWith(prefix, StringComparison.Ordinal)));
        Assert.DoesNotContain(result.Findings, f => f.Module == "legal");
    }

    [Fact]
    public async Task LegalText_IsCheckedForPresenceOfInformation()
    {
        var result = await AnalyzeAsync(new TextInput
        {
            Kind = TextKind.Legal,
            Text = "Odstoupení od smlouvy\nSpotřebitel může odstoupit od smlouvy uzavřené na dálku do 14 dnů od převzetí zboží.",
        });

        Assert.Contains(result.Segments, s => s.Kind == SegmentKind.LegalParagraph);
        Assert.Contains(result.RuleResults, r => r.RuleId == "legal_withdrawal_missing" && r.Outcome == RuleOutcome.Present);
        Assert.Contains(result.Findings, f => f.RuleId == "legal_adr_missing" && f.Scope == "site");
    }

    [Fact]
    public async Task ShortBadge_IsEvaluated()
    {
        var result = await AnalyzeAsync(new TextInput { Text = "EKO" }, "sk");

        Assert.Equal("EKO", Assert.Single(result.Segments).Text);
        Assert.Equal(1, result.Stats.JevCalls); // in Slovakia eco, dur and ucp read sentences, all in one request
    }

    [Fact]
    public async Task TextWithoutWord_IsNotEvaluated()
    {
        var result = await AnalyzeAsync(new TextInput { Text = "189,90 Kč" });

        Assert.Empty(result.Segments);
        Assert.Equal(0, result.Stats.JevCalls);
    }

    [Fact]
    public async Task DefaultCountry_IsSlovakiaWithSlovakLegalRulesAndReferences()
    {
        await using var provider = TestServices.Create(FileSystemPageFetcher.ForFixture());
        var result = await provider.GetRequiredService<IEshopGuard>().AnalyzeTextsAsync(
            [
                new TextInput { Text = "Tento šampón je ekologický a šetrný k prírode." },
                new TextInput { Kind = TextKind.Legal, Text = "Odstúpenie od zmluvy\nSpotrebiteľ môže odstúpiť od zmluvy uzavretej na diaľku do 14 dní od prevzatia tovaru." },
            ],
            new AnalyzeOptions(),
            TestContext.Current.CancellationToken);

        Assert.Contains(result.RuleSets, r => r.Version.StartsWith("legal-sk-", StringComparison.Ordinal));
        Assert.Contains(result.RuleResults, r => r.RuleId == "legal_withdrawal_missing" && r.Outcome == RuleOutcome.Present);
        var finding = Assert.Single(result.Findings, f => f.RuleId == "eco_generic_claim");
        Assert.Contains(finding.Strictest.LegalRefs, r => r.Jurisdiction == "sk");
        Assert.DoesNotContain(finding.Strictest.LegalRefs, r => r.Jurisdiction == "cz");
    }

    private static async Task<AnalysisResult> AnalyzeAsync(TextInput input, string country = "cz")
    {
        await using var provider = TestServices.Create(FileSystemPageFetcher.ForFixture());
        return await provider.GetRequiredService<IEshopGuard>()
            .AnalyzeTextsAsync([input], new AnalyzeOptions { Country = country }, TestContext.Current.CancellationToken);
    }
}
