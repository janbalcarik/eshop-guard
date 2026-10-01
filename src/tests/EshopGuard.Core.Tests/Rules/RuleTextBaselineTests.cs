using System.Text.Json.Nodes;
using EshopGuard.Core.Models;
using EshopGuard.Core.Rules;
using EshopGuard.Core.Rules.Texts;

namespace EshopGuard.Core.Tests;

/// <summary>
/// The texts moved to <c>rules/texts/</c> render exactly as they were in the rule files before change 6
/// (<c>Baselines/rule-texts-cs.json</c>, written from the rule files by a one-off dump at commit 7980c24): title, explanation
/// for every jurisdiction, recommendation, remarks on labels and the built-in finding. The report asks for Czech; a set written
/// in Slovak (<c>legal_sk</c>) is shown in Slovak until its Czech translation is reviewed, exactly as before.
/// </summary>
public sealed class RuleTextBaselineTests
{
    /// <summary>File name of the reference texts in <c>Baselines/</c>.</summary>
    public const string FileName = "rule-texts-cs.json";

    [Fact]
    public void Baselines_Exist()
    {
        Assert.True(File.Exists(Path.Combine(BaselineScenarios.OutputBaselines, FileName)), FileName);
        Assert.True(File.Exists(Path.Combine(BaselineScenarios.OutputBaselines, "site", "report.md")));
        Assert.True(File.Exists(Path.Combine(BaselineScenarios.OutputBaselines, "site-sk", "report.md")));
    }

    [Fact]
    public void RenderedTexts_AreTheTextsOfTheRulesBeforeChange6()
    {
        var baseline = JsonNode.Parse(File.ReadAllText(Path.Combine(BaselineScenarios.OutputBaselines, FileName)))!;
        var catalog = TestTexts.Catalog;
        var renderer = TestTexts.Renderer;
        var differences = new List<string>();
        void Compare(string what, string expected, string actual)
        {
            if (expected != actual)
            {
                differences.Add($"{what}:\n  čekáno: {expected}\n  je:     {actual}");
            }
        }

        foreach (var (name, setNode) in baseline["rule_sets"]!.AsObject())
        {
            // Versions may have moved on (legal_cz got new rules); the texts of the rules that were there stay the same.
            var set = catalog.RuleSets.Single(s => s.Name == name);
            foreach (var (ruleId, ruleNode) in setNode!["rules"]!.AsObject())
            {
                foreach (var (jurisdiction, explanation) in ruleNode!["explanation_for"]!.AsObject())
                {
                    var variant = set.ExplanationVariants.TryGetValue(ruleId, out var variants) && variants.Contains(jurisdiction)
                        ? jurisdiction
                        : JurisdictionVerdict.DefaultVariant;
                    var finding = new Finding
                    {
                        RuleId = ruleId,
                        Module = set.Module,
                        Scope = "segment",
                        Verdicts =
                        [
                            new JurisdictionVerdict
                            {
                                Jurisdiction = jurisdiction, Severity = "high", Checkability = "text", RuleSet = name, RuleSetVersion = set.Version,
                                ExplanationVariant = variant,
                            },
                        ],
                    };
                    var rendered = renderer.Render(finding, "cs");
                    Compare($"{name}/{ruleId} title", ruleNode["title"]!.GetValue<string>(), rendered.Title);
                    Compare($"{name}/{ruleId} explanation {jurisdiction}", explanation!.GetValue<string>(), rendered.Explanation);
                    Compare($"{name}/{ruleId} recommendation", ruleNode["recommendation"]!.GetValue<string>(), rendered.Recommendation);
                }
            }
        }

        foreach (var noteNode in baseline["label_notes"]!.AsArray())
        {
            var names = noteNode!["names"]!.AsArray().Select(n => n!.GetValue<string>()).ToList();
            var label = catalog.Labels.Notes.Single(n => n.Names.SequenceEqual(names));
            Compare($"label_notes {label.Id}", noteNode["note"]!.GetValue<string>(),
                renderer.Note(new FindingNote(EngineCodes.LabelNote, NoteParams.Of(("label_id", label.Id))), "cs"));
        }

        var missing = baseline["legal_pages_missing"]!;
        var builtIn = RuleEngine.Evaluate(new RuleEngineInput
        {
            RuleSets = [], Labels = catalog.Labels, Segments = [], Jurisdictions = ["cz"], AddMissingLegalPagesFinding = true,
        }).Findings.Single();
        var texts = renderer.Render(builtIn, "cs");
        Compare("legal_pages_missing title", missing["title"]!.GetValue<string>(), texts.Title);
        Compare("legal_pages_missing explanation", missing["explanation"]!.GetValue<string>(), texts.Explanation);
        Compare("legal_pages_missing recommendation", missing["recommendation"]!.GetValue<string>(), texts.Recommendation);

        Assert.True(differences.Count == 0, string.Join('\n', differences));
    }
}
