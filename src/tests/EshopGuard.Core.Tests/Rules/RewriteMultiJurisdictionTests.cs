using EshopGuard.Core.Fix;
using EshopGuard.Core.Models;
using EshopGuard.Core.Options;
using EshopGuard.Core.Report;
using EshopGuard.Core.Rules;
using Microsoft.Extensions.DependencyInjection;

namespace EshopGuard.Core.Tests;

/// <summary>
/// The rewrite with verdicts by jurisdiction (change 6): the prompt has the texts in the language of the content of the shop
/// and the verdicts of every jurisdiction; the output of a scan, old or new, is read back without loss.
/// </summary>
public sealed class RewriteMultiJurisdictionTests
{
    private const string Url = "https://shop.example/produkt";

    [Fact]
    public async Task SlovakShop_GetsTheReviewedSlovakTextsInThePrompt()
    {
        using var rules = new TempRules();
        var czech = rules.Text("rules/texts/cs/eco.yaml");
        var review = czech.IndexOf("review:\n", StringComparison.Ordinal);
        var body = czech.IndexOf("rules:\n", StringComparison.Ordinal);
        rules.Write("rules/texts/sk/eco.yaml", czech[..review].Replace("locale: cs", "locale: sk", StringComparison.Ordinal)
            + "review:\n  reviewed_by: \"kontrolor\"\n  reviewed_at: \"2026-10-02\"\n"
            + czech[body..].Replace("title: \"Obecné environmentální tvrzení bez upřesnění\"", "title: \"Všeobecné environmentálne tvrdenie bez spresnenia\"", StringComparison.Ordinal));
        var client = new RewriteTests.RecordingClient(_ => """{"changes":[],"kept":[]}""");
        await using var provider = RewriteTests.Create(client, configure: rules.Apply);
        var input = new RewriteInput
        {
            Pages = [RewriteTests.Page(Url, "Tento šampón je ekologický.")],
            Findings = [Finding("eco_generic_claim", "eco", "Tento šampón je ekologický.", ("sk", "text"))],
            Country = "sk",
        };

        await provider.GetRequiredService<ITextRewriter>().RewriteAsync(input, ct: TestContext.Current.CancellationToken);

        Assert.Contains("  pravidlo: Všeobecné environmentálne tvrdenie bez spresnenia\n", Assert.Single(client.Requests).PagePart, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FindingInTwoCountries_ShowsTheVerdictOfEach()
    {
        var client = new RewriteTests.RecordingClient(_ => """{"changes":[],"kept":[]}""");
        await using var provider = RewriteTests.Create(client);
        var input = new RewriteInput
        {
            Pages = [RewriteTests.Page(Url, "Za hodnotenie 5 hviezdičkami vám vrátime 5 €.")],
            Findings = [Finding("ucp_review_reward_positive", "ucp", "Za hodnotenie 5 hviezdičkami vám vrátime 5 €.", ("sk", "text"), ("cz", "assess"))],
            Jurisdictions = ["sk", "cz"],
        };

        await provider.GetRequiredService<ITextRewriter>().RewriteAsync(input, ct: TestContext.Current.CancellationToken);

        var page = Assert.Single(client.Requests).PagePart;
        Assert.Contains("F1 | skupina: porušení", page, StringComparison.Ordinal);
        Assert.Contains("  CZ | skupina: k posouzení", page, StringComparison.Ordinal);

        // The same texts in both countries are written once.
        Assert.Single(page.Split('\n'), l => l.StartsWith("  vysvetlenie:", StringComparison.Ordinal));
        Assert.DoesNotContain("    vysvetlenie:", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ScanFolder_IsReadBackWithItsVerdictsAndCodes()
    {
        var directory = Directory.CreateTempSubdirectory("eshopguard-scan-").FullName;
        try
        {
            ScanResult scan;
            await using (var provider = TestServices.Create(FileSystemPageFetcher.ForSlovakFixture()))
            {
                scan = await provider.GetRequiredService<IEshopGuard>().ScanSiteAsync(
                    FileSystemPageFetcher.DefaultBaseUrl, new ScanOptions { Jurisdictions = ["sk", "cz"] }, ct: TestContext.Current.CancellationToken);
                foreach (var writer in provider.GetServices<IReportWriter>())
                {
                    await writer.WriteAsync(scan, directory, TestContext.Current.CancellationToken);
                }
            }

            var input = ScanOutputReader.Read(directory, "sk", jurisdictions: ["sk", "cz"]);

            Assert.Equal(["sk", "cz"], input.ResolvedJurisdictions);
            Assert.Equal(scan.Findings.Count, input.Findings.Count);
            for (var i = 0; i < scan.Findings.Count; i++)
            {
                Assert.Equal(scan.Findings[i].Verdicts.Select(v => (v.Jurisdiction, v.Status, v.Checkability, v.RuleSet)),
                    input.Findings[i].Verdicts.Select(v => (v.Jurisdiction, v.Status, v.Checkability, v.RuleSet)));
                Assert.Equal(scan.Findings[i].Verdicts.SelectMany(v => v.Notes), input.Findings[i].Verdicts.SelectMany(v => v.Notes));
                Assert.Equal(TestTexts.Title(scan.Findings[i]), TestTexts.Title(input.Findings[i]));
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void FindingsWrittenBeforeChange6_GetAVerdictOfTheCountry()
    {
        var directory = Directory.CreateTempSubdirectory("eshopguard-old-scan-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(directory, "findings.json"), """
                [{"rule_id": "legal_adr_missing", "module": "legal", "title": "Chýba odkaz", "severity": "high", "checkability": "text",
                  "scope": "site", "band": "high", "score": 0.95, "text": null, "urls": [], "occurrences": 0, "boilerplate": false,
                  "question_probs": {"legal_adr": 0.05},
                  "legal_refs": [{"jurisdiction": "sk", "ref": "§ 5 ods. 1 písm. q) zákona č. 108/2024 Z. z.", "status": "ověřit"}],
                  "explanation": "…", "recommendation": "…", "notes": ["Nejbližší nalezený odstavec má pravděpodobnost 0,05."]}]
                """);
            File.WriteAllText(Path.Combine(directory, "pages.jsonl"), "{\"url\": \"https://shop.example/\", \"type\": \"home\", \"main_text\": \"\"}\n");

            var finding = Assert.Single(ScanOutputReader.Read(directory, "sk").Findings);

            var verdict = Assert.Single(finding.Verdicts);
            Assert.Equal(("sk", "text", FindingBand.High, 0.95), (verdict.Jurisdiction, verdict.Checkability, verdict.Band, verdict.Score));
            Assert.Equal(EngineCodes.ToVerify, Assert.Single(verdict.LegalRefs).Status);
            Assert.StartsWith("Chýba odkaz na informácie o subjekte alternatívneho riešenia sporov", TestTexts.Title(finding), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static Finding Finding(string ruleId, string module, string text, params (string Jurisdiction, string Checkability)[] verdicts) => new()
    {
        RuleId = ruleId,
        Module = module,
        Scope = "segment",
        Text = text,
        Urls = [Url],
        Sources = [SegmentSource.Main],
        Verdicts = verdicts.Select(v => new JurisdictionVerdict
        {
            Jurisdiction = v.Jurisdiction, Checkability = v.Checkability, Severity = "high", Band = FindingBand.High, Score = 0.9,
            RuleSet = module, RuleSetVersion = "test",
        }).ToList(),
    };
}
