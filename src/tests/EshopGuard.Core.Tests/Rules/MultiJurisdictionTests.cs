using System.Collections.Concurrent;
using EshopGuard.Core.Jev;
using EshopGuard.Core.Models;
using EshopGuard.Core.Options;
using EshopGuard.Core.Rules;
using Microsoft.Extensions.DependencyInjection;

namespace EshopGuard.Core.Tests;

/// <summary>
/// One scan for several jurisdictions (change 6): Jev is asked once where the questions are the same and separately where
/// sets of different jurisdictions ask the same id with another wording; a finding of one rule and one text is one finding
/// with a verdict per jurisdiction; what did not run for a jurisdiction is in the coverage.
/// </summary>
public sealed class MultiJurisdictionTests
{
    [Fact]
    public async Task SentencesAreAskedOnce_LegalParagraphsOncePerJurisdiction()
    {
        var (skOnly, skClient) = await ScanAsync(["sk"]);
        var (both, bothClient) = await ScanAsync(["sk", "cz"]);

        Assert.Equal(skClient.Sentences, bothClient.Sentences);
        Assert.Equal(skClient.Sieve, bothClient.Sieve);
        Assert.True(skClient.Paragraphs > 0 && skClient.Sentences > 0 && skClient.Sieve > 0);
        Assert.Equal(2 * skClient.Paragraphs, bothClient.Paragraphs);

        // The answers of the two wordings of legal_adr stay apart.
        var paragraph = both.Segments.First(s => s.Kind == SegmentKind.LegalParagraph && s.Probabilities.Count > 0);
        Assert.Contains(QuestionKey.Of("legal_sk", "legal_adr"), paragraph.Probabilities.Keys);
        Assert.Contains(QuestionKey.Of("legal_cz", "legal_adr"), paragraph.Probabilities.Keys);
        Assert.DoesNotContain(skOnly.Segments.SelectMany(s => s.Probabilities.Keys), k => k.StartsWith("legal_cz:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OneFindingCarriesTheVerdictsOfEveryJurisdiction()
    {
        var (result, _) = await ScanAsync(["sk", "cz"]);

        var adr = Assert.Single(result.Findings, f => f.RuleId == "legal_adr_missing");
        Assert.Equal(["cz", "sk"], adr.Verdicts.Select(v => v.Jurisdiction).Order());
        Assert.Equal("legal_cz", adr.Verdicts.Single(v => v.Jurisdiction == "cz").RuleSet);
        Assert.Equal("legal_sk", adr.Verdicts.Single(v => v.Jurisdiction == "sk").RuleSet);

        // References of each verdict: the EU and its own jurisdiction.
        Assert.All(adr.Verdicts, v => Assert.All(v.LegalRefs, r => Assert.Contains(r.Jurisdiction, new[] { "eu", v.Jurisdiction })));

        // ucp runs for both countries on the same answers; eco and dur have rules only for Slovakia.
        Assert.Contains(result.Findings, f => f.Module == "ucp" && f.Verdicts.Select(v => v.Jurisdiction).Order().SequenceEqual(["cz", "sk"]));
        Assert.All(result.Findings.Where(f => f.Module is "eco" or "dur"), f => Assert.Equal(["sk"], f.Verdicts.Select(v => v.Jurisdiction)));
        Assert.Equal(result.Findings.Count, result.Findings.Select(f => (f.RuleId, f.Scope == "site" ? null : f.SegmentHash)).Distinct().Count());
    }

    [Fact]
    public async Task ModuleWithoutRulesForAJurisdiction_IsInTheCoverage()
    {
        var (result, _) = await ScanAsync(["sk", "cz"]);

        var cz = Assert.Single(result.JurisdictionCoverage, c => c.Jurisdiction == "cz");
        Assert.Contains(new ModuleNotRun("eco", EngineCodes.NoRulesForJurisdiction), cz.NotRun);
        Assert.Contains("ucp", cz.Modules);
        Assert.Contains("legal", cz.Modules);
        var sk = Assert.Single(result.JurisdictionCoverage, c => c.Jurisdiction == "sk");
        Assert.Empty(sk.NotRun);
        Assert.Equal(["sk", "cz"], result.Jurisdictions);
    }

    [Fact]
    public async Task ExplicitModuleWithoutRulesForAJurisdiction_IsAWarning()
    {
        await using var provider = TestServices.Create(FileSystemPageFetcher.ForSlovakFixture());
        var result = await provider.GetRequiredService<IEshopGuard>().AnalyzeTextsAsync(
            [new TextInput { Text = "Tento šampón je ekologický a šetrný k prírode." }],
            new AnalyzeOptions { Modules = ["eco"], Jurisdictions = ["sk", "cz"] },
            TestContext.Current.CancellationToken);

        var warning = Assert.Single(result.Warnings);
        Assert.Equal(EngineCodes.ModuleOtherJurisdiction, warning.Code);
        Assert.Equal("Modul eco má pravidla jen pro sk, pro zemi cz se nespustil.", TestTexts.Renderer.Warning(warning, "cs"));
    }

    [Fact]
    public void SameQuestionIdWithAnotherWording_GoesIntoAnotherRequest()
    {
        var segment = new Segment { Hash = "sha256:p", Kind = SegmentKind.LegalParagraph, Text = "Spory rieši SOI." };
        SegmentEvaluator.WorkItem Item(string set, string text) => new(
            segment,
            new Dictionary<string, JevQuestion> { ["legal_adr"] = new() { Type = "noul", Instructions = text } },
            segment.Text,
            default,
            "legal",
            set);

        var split = SegmentEvaluator.Group([Item("legal_sk", "Uvádza text subjekt ARS?"), Item("legal_cz", "Uvádí text subjekt ARS?")]);
        var shared = SegmentEvaluator.Group([Item("legal_sk", "Uvádza text subjekt ARS?"), Item("legal_cz", "Uvádza text subjekt ARS?")]);

        Assert.Equal(2, split.Count);
        var request = Assert.Single(shared);
        Assert.Equal(2, request.Items.Count);
        Assert.Single(request.Questions);
    }

    private static async Task<(ScanResult Result, CountingClient Client)> ScanAsync(IReadOnlyList<string> jurisdictions)
    {
        var client = new CountingClient();
        await using var provider = TestServices.Create(FileSystemPageFetcher.ForSlovakFixture(), client: client);
        var result = await provider.GetRequiredService<IEshopGuard>().ScanSiteAsync(
            FileSystemPageFetcher.DefaultBaseUrl, new ScanOptions { Jurisdictions = jurisdictions }, ct: TestContext.Current.CancellationToken);
        return (result, client);
    }

    /// <summary>The mock client that counts the requests of the sieve, of sentences and of legal paragraphs.</summary>
    private sealed class CountingClient : IJevClient
    {
        private readonly MockJevClient _inner = new();

        private readonly ConcurrentQueue<string> _requests = new();

        public int Sieve => _requests.Count(r => r == "sieve");

        public int Sentences => _requests.Count(r => r == "sentence");

        public int Paragraphs => _requests.Count(r => r == "paragraph");

        public Task<JevResult> EvaluateAsync(object state, IReadOnlyDictionary<string, JevQuestion> questions, CancellationToken ct)
        {
            // The sieve asks one question per module (sieve_eco…), legal paragraphs the questions of legal sets.
            _requests.Enqueue(questions.Keys.All(k => k.StartsWith("sieve_", StringComparison.Ordinal)) ? "sieve"
                : questions.Keys.Any(k => k.StartsWith("legal_", StringComparison.Ordinal)) ? "paragraph"
                : "sentence");
            return _inner.EvaluateAsync(state, questions, ct);
        }
    }
}
