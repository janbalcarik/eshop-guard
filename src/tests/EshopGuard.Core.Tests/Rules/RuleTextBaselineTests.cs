using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using EshopGuard.Core.Options;
using EshopGuard.Core.Rules;
using Microsoft.Extensions.Logging.Abstractions;

namespace EshopGuard.Core.Tests;

/// <summary>
/// Texts of the rules as they were in the rule files before change 6 (<c>Baselines/rule-texts-cs.json</c>): title,
/// explanation for every jurisdiction, recommendation, label remarks and the built-in finding. Written once by
/// <see cref="DumpRuleTexts"/>; the texts moved into <c>rules/texts/</c> must render exactly the same.
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

    /// <summary>
    /// Writes the reference texts (task 1.1 of change 6). Explicit: runs only on request
    /// (<c>dotnet run --project src/tests/EshopGuard.Core.Tests -- -explicit only -trait "Category=Baseline"</c>).
    /// </summary>
    [Fact(Explicit = true)]
    [Trait("Category", "Baseline")]
    public async Task DumpRuleTexts()
    {
        var options = new EshopGuardOptions();
        options.Rules.Directory = TestServices.RulesDirectory;
        options.Rules.LabelsFile = TestServices.LabelsFile;
        options.Rules.LegalRequirementsFile = TestServices.LegalRequirementsFile;
        options.Rules.SieveFile = TestServices.SieveFile;
        var catalog = await new YamlRuleSetProvider(Microsoft.Extensions.Options.Options.Create(options), NullLogger<YamlRuleSetProvider>.Instance)
            .LoadAsync(TestContext.Current.CancellationToken);

        var sets = new JsonObject();
        foreach (var set in catalog.RuleSets.Where(s => s.Enabled).OrderBy(s => s.SourceFile, StringComparer.Ordinal))
        {
            var rules = new JsonObject();
            foreach (var rule in set.Rules)
            {
                // Explanations for the jurisdictions of the set and for every jurisdiction with its own text.
                var jurisdictions = set.Jurisdictions
                    .Concat(rule.ExplanationByJurisdiction?.Keys ?? Enumerable.Empty<string>())
                    .Distinct()
                    .Order(StringComparer.Ordinal);
                var explanations = new JsonObject();
                foreach (var jurisdiction in jurisdictions)
                {
                    explanations[jurisdiction] = rule.ExplanationFor(jurisdiction);
                }

                rules[rule.Id] = new JsonObject
                {
                    ["title"] = rule.Title,
                    ["explanation"] = rule.Explanation,
                    ["explanation_for"] = explanations,
                    ["recommendation"] = rule.Recommendation,
                };
            }

            sets[Path.GetFileNameWithoutExtension(set.SourceFile)] = new JsonObject
            {
                ["module"] = set.Module,
                ["version"] = set.Version,
                ["jurisdictions"] = new JsonArray([.. set.Jurisdictions.Select(j => (JsonNode)j)]),
                ["rules"] = rules,
            };
        }

        var missing = RuleEngine.Evaluate(new RuleEngineInput
        {
            RuleSets = [],
            Labels = catalog.Labels,
            Segments = [],
            Country = "cz",
            AddMissingLegalPagesFinding = true,
        }).Findings.Single();

        var document = new JsonObject
        {
            ["rule_sets"] = sets,
            ["label_notes"] = new JsonArray([.. catalog.Labels.Notes.Select(n => (JsonNode)new JsonObject
            {
                ["names"] = new JsonArray([.. n.Names.Select(name => (JsonNode)name)]),
                ["note"] = n.Note,
            })]),
            ["legal_pages_missing"] = new JsonObject
            {
                ["title"] = missing.Title,
                ["explanation"] = missing.Explanation,
                ["recommendation"] = missing.Recommendation,
            },
        };

        var json = document.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        await File.WriteAllTextAsync(Path.Combine(BaselineScenarios.SourceBaselines, FileName), json + "\n",
            new System.Text.UTF8Encoding(false), TestContext.Current.CancellationToken);
    }
}
