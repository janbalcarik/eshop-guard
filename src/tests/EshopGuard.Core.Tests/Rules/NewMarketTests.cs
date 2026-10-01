using EshopGuard.Core.Jev;
using EshopGuard.Core.Models;
using EshopGuard.Core.Options;
using EshopGuard.Core.Rules;
using EshopGuard.Core.Rules.Texts;
using Microsoft.Extensions.DependencyInjection;

namespace EshopGuard.Core.Tests;

/// <summary>
/// A new market is data, not code (change 6, design section 9): a made-up jurisdiction <c>xx</c> with its own language
/// <c>xx</c> gets a line in <c>config/jurisdictions.yaml</c>, a rule set, its texts and the fingerprint of its questions,
/// and then a check of a text finds and describes a problem in that language.
/// </summary>
public sealed class NewMarketTests
{
    [Fact]
    public async Task MadeUpJurisdictionAndLanguage_WorkWithoutCodeChanges()
    {
        using var rules = new TempRules();
        AddMarket(rules);
        var (added, errors) = await RuleMaintenance.UpdateQuestionSetHashesAsync(new RulesOptions { Directory = rules.RulesDirectory }, TestContext.Current.CancellationToken);
        Assert.Empty(errors);
        Assert.Equal(["claims claims-xx-1"], added);

        await using var provider = TestServices.Create(FileSystemPageFetcher.ForFixture(), rules.Apply, new YesClient());
        var result = await provider.GetRequiredService<IEshopGuard>().AnalyzeTextsAsync(
            [new TextInput { Text = "Xxian claim about the planet." }],
            new AnalyzeOptions { Jurisdictions = ["xx"] },
            TestContext.Current.CancellationToken);

        var finding = Assert.Single(result.Findings);
        var verdict = Assert.Single(finding.Verdicts);
        Assert.Equal(("xx", "claims_xx"), (verdict.Jurisdiction, verdict.RuleSet));
        Assert.Equal(["eu", "xx"], verdict.LegalRefs.Select(r => r.Jurisdiction));

        var catalog = await rules.LoadAsync();
        var rendered = new RuleTextRenderer(catalog).Render(finding, "xx");
        Assert.Equal(("xx", "Xx title"), (rendered.Locale, rendered.Title));
        var reference = rendered.LegalRefs.Single(r => r.Jurisdiction == "xx");
        Assert.Equal(("Xx ref", "xx-verify"), (reference.Ref, reference.Status));
        Assert.Equal("Xx EU ref", rendered.LegalRefs.Single(r => r.Jurisdiction == "eu").Ref);
        Assert.Contains("xx", catalog.Texts.ToolLocales);
        Assert.Equal("Xxland", new RuleTextRenderer(catalog).JurisdictionName("xx", "xx"));
    }

    [Fact]
    public async Task UnknownJurisdictionInARuleSet_StopsTheLoad()
    {
        using var rules = new TempRules();
        rules.Replace("rules/ucp.yaml", "jurisdictions: [sk, cz]", "jurisdictions: [sk, cs]");

        var error = await Assert.ThrowsAsync<RuleValidationException>(rules.LoadAsync);

        Assert.Contains(error.Errors, e => e.StartsWith("ucp.yaml: jurisdictions musí obsahovat jen jurisdikce z config/jurisdictions.yaml", StringComparison.Ordinal));
    }

    /// <summary>Everything a market needs: jurisdiction, rule set, texts in its language, its name in Czech.</summary>
    private static void AddMarket(TempRules rules)
    {
        rules.Write("config/jurisdictions.yaml", rules.Text("config/jurisdictions.yaml") + "xx:\n  law_language: xx\n");
        rules.Write("rules/claims_xx.yaml", """
            version: "claims-xx-1"
            module: claims
            applies_to: sentence
            jurisdictions: [xx]
            questions:
              xx_claim: {type: yes_no, text_en: "Is it a claim?", text_cs: "Je to tvrzení?"}
            rules:
              - id: xx_claim_unsupported
                scope: segment
                logic:
                  all: [{q: xx_claim, gte: 0.5}]
                severity: high
                checkability: assess
                legal_refs:
                  - {jurisdiction: eu, ref: "Směrnice", ref_by_language: {xx: "Xx EU ref"}, status: to_verify}
                  - {jurisdiction: xx, ref: "Xx ref", status: to_verify}
            """);
        rules.Write("rules/texts/xx/claims_xx.yaml", """
            locale: xx
            rule_set: claims_xx
            source_version: "claims-xx-1"
            review:
              original: true
            rules:
              xx_claim_unsupported:
                title: "Xx title"
                explanation: "Xx explanation"
                recommendation: "Xx recommendation"
            """);

        // The texts of the tool and the labels in the new language: the Czech ones with every text marked.
        var engine = rules.Text("rules/texts/cs/_engine.yaml")
            .Replace("locale: cs", "locale: xx", StringComparison.Ordinal)
            .Replace("culture: cs-CZ", "culture: \"\"", StringComparison.Ordinal)
            .Replace("  to_verify: \"ověřit\"", "  to_verify: \"xx-verify\"", StringComparison.Ordinal)
            .Replace("  cz: \"Česko\"\n", "  cz: \"Česko\"\n  xx: \"Xxland\"\n", StringComparison.Ordinal);
        rules.Write("rules/texts/xx/_engine.yaml", engine);
        rules.Write("rules/texts/xx/_labels.yaml", rules.Text("rules/texts/cs/_labels.yaml").Replace("locale: cs", "locale: xx", StringComparison.Ordinal));
        rules.Replace("rules/texts/cs/_engine.yaml", "  cz: \"Česko\"\n", "  cz: \"Česko\"\n  xx: \"Iksland\"\n");
    }

    /// <summary>Answers yes to every question.</summary>
    private sealed class YesClient : IJevClient
    {
        public Task<JevResult> EvaluateAsync(object state, IReadOnlyDictionary<string, JevQuestion> questions, CancellationToken ct) =>
            Task.FromResult(new JevResult
            {
                Model = "yes",
                Answers = questions.ToDictionary(q => q.Key, _ => new JevAnswer { Noul = 0.95 }),
                Usage = new JevUsage { InputTokens = 10 },
            });
    }
}
