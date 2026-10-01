using System.Text.Json;
using EshopGuard.Core.Models;
using EshopGuard.Core.Options;
using EshopGuard.Core.Rules;
using Microsoft.Extensions.Logging.Abstractions;

namespace EshopGuard.Core.Tests;

/// <summary>
/// The list of legal requirements for all products of a category (SK príloha č. 1 bod 15) and the claim_list_match check
/// with the shipped rule set lr.yaml and fixed probabilities.
/// </summary>
public class LegalRequirementTests
{
    private const string Url = "https://shop.example/produkt";

    [Fact]
    public async Task List_ComposesTheSameExpressionsAsTheResearch()
    {
        var list = (await LoadCatalogAsync()).LegalRequirements;

        var cases = Cases();
        Assert.Equal(61, cases.Count);
        foreach (var item in cases)
        {
            Assert.Equal(item.ComposedSk, list.For("sk").Single(r => r.Id == item.Id).ClaimPattern);
            Assert.Equal(item.ComposedCz, list.For("cz").Single(r => r.Id == item.Id).ClaimPattern);
        }
    }

    [Fact]
    public async Task List_MatchesThePositiveAndNotTheNegativeSentencesOfTheResearch()
    {
        var list = (await LoadCatalogAsync()).LegalRequirements;

        var failures = new List<string>();
        foreach (var item in Cases())
        {
            var sk = list.For("sk").Single(r => r.Id == item.Id);
            var cz = list.For("cz").Single(r => r.Id == item.Id);
            failures.AddRange(item.PositiveSk.Where(s => !sk.MatchesClaim(s)).Select(s => $"SK nezachytí ({item.Id}): {s}"));
            failures.AddRange(item.PositiveCz.Where(s => !cz.MatchesClaim(s)).Select(s => $"CZ nezachytí ({item.Id}): {s}"));
            failures.AddRange(item.Negative.Where(cz.MatchesClaim).Select(s => $"CZ zachytí navíc ({item.Id}): {s}"));
        }

        Assert.Empty(failures);
    }

    [Fact]
    public async Task RequirementForAllProducts_IsAFindingNamingTheLegalBasis()
    {
        var output = Evaluate(await LoadCatalogAsync(), "✓ Bez BPA – pre bezpečnosť vášho bábätka.", "Dojčenská fľaša zo skla 240 ml", Presented());

        var finding = Assert.Single(output.Findings);
        Assert.Equal("lr_requirement_as_feature", finding.RuleId);
        Assert.Contains(TestTexts.Notes(finding), n => n.Contains("„bez BPA“", StringComparison.Ordinal) && n.Contains("2024/3190", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SentenceSayingItAppliesToAllProducts_IsInformationAboutTheLaw()
    {
        var probabilities = Presented(("lr_as_feature", 0.2), ("lr_says_required_for_all", 0.9));
        var output = Evaluate(await LoadCatalogAsync(), "Ako všetky dojčenské fľaše predávané v EÚ neobsahuje BPA.", "Dojčenská fľaša zo skla 240 ml", probabilities);

        var finding = Assert.Single(output.Findings);
        Assert.Equal("lr_requirement_stated_as_law", finding.RuleId);
        Assert.Equal("low", finding.Severity);
    }

    [Fact]
    public async Task UnknownCategory_IsAnUnlistedClaimThatNamesTheCategoriesOfTheList()
    {
        var output = Evaluate(await LoadCatalogAsync(), "✓ Bez BPA – pre bezpečnosť vášho bábätka.", "Avent Natural 240 ml | Obchod", Presented());

        var finding = Assert.Single(output.Findings);
        Assert.Equal("lr_free_from_unlisted", finding.RuleId);
        Assert.Contains(TestTexts.Notes(finding), n => n.Contains("Kojenecké lahve", StringComparison.Ordinal) && n.Contains("Avent Natural 240 ml", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ClaimThatMayStay_GivesNoFinding()
    {
        var output = Evaluate(await LoadCatalogAsync(), "✓ Náušnice bez niklu.", "Strieborné náušnice s perlou", Presented());

        Assert.Empty(output.Findings);
        Assert.Contains(output.RuleResults, r => r.RuleId == "lr_requirement_as_feature" && r.Outcome == RuleOutcome.NotInList);
    }

    [Fact]
    public async Task LimitInsteadOfBan_IsToVerify()
    {
        var output = Evaluate(await LoadCatalogAsync(), "Prací gél bez fosfátov.", "Prací gél Levanduľa 1 l", Presented());

        var finding = Assert.Single(output.Findings);
        Assert.Equal("lr_requirement_as_feature_partial", finding.RuleId);
        Assert.Contains(TestTexts.Notes(finding), n => n.Contains("648/2004", StringComparison.Ordinal));
    }

    [Fact]
    public async Task IrrelevantBenefitFromTheList_IsOneFinding()
    {
        var probabilities = Presented(("lr_obvious_absence", 0.9));
        var output = Evaluate(await LoadCatalogAsync(), "Minerálna voda bez lepku.", "Minerálna voda Budiš 1,5 l", probabilities);

        var finding = Assert.Single(output.Findings);
        Assert.Equal("lr_irrelevant_benefit_listed", finding.RuleId);
    }

    [Fact]
    public async Task UnlistedClaimForASpecificGroup_GivesNoFinding()
    {
        var probabilities = Presented(("lr_specific_need", 0.9));
        var output = Evaluate(await LoadCatalogAsync(), "Gél neobsahuje zložky živočíšneho pôvodu, je vhodný aj pre vegánov.", "Prací gél z mydlových orechov", probabilities);

        Assert.Empty(output.Findings);
    }

    [Fact]
    public async Task CategoryFromTheSentenceAlone_IsNotUsed()
    {
        // The sentence names a baby bottle, but the page (a blog article) does not: the list cannot decide.
        var output = Evaluate(await LoadCatalogAsync(), "✓ Dojčenská fľaša bez BPA.", "Ako sa starať o bábätko v lete | Blog", Presented());

        var finding = Assert.Single(output.Findings);
        Assert.Equal("lr_free_from_unlisted", finding.RuleId);
    }

    private static async Task<RuleCatalog> LoadCatalogAsync()
    {
        return await TestTexts.Provider().LoadAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>An absence claim presented as a feature; the other questions answer no unless overridden.</summary>
    private static Dictionary<string, double> Presented(params (string Question, double Probability)[] overrides)
    {
        var probabilities = new Dictionary<string, double>
        {
            ["lr_free_from"] = 0.95,
            ["lr_compliance"] = 0.05,
            ["lr_no_animal_testing"] = 0.02,
            ["lr_as_feature"] = 0.9,
            ["lr_says_required_for_all"] = 0.05,
            ["lr_beyond_law"] = 0.03,
            ["lr_obvious_absence"] = 0.1,
            ["lr_specific_need"] = 0.05,
        };
        foreach (var (question, probability) in overrides)
        {
            probabilities[question] = probability;
        }

        // Answers are keyed by rule set and question (QuestionKey).
        return probabilities.ToDictionary(p => QuestionKey.Of("lr", p.Key), p => p.Value);
    }

    private static RuleEngineOutput Evaluate(RuleCatalog catalog, string text, string category, Dictionary<string, double> probabilities) =>
        RuleEngine.Evaluate(new RuleEngineInput
        {
            RuleSets = catalog.RuleSets.Where(s => s.Module == "lr").ToList(),
            Labels = catalog.Labels,
            Segments =
            [
                new Segment { Hash = TextTools.Sha256(text), Kind = SegmentKind.Sentence, Text = text, Urls = [Url], Probabilities = probabilities },
            ],
            Jurisdictions = ["sk"],
            LegalRequirements = catalog.LegalRequirements,
            PageCategories = new Dictionary<string, string> { [Url] = category },
        });

    private sealed record Case(string Id, string ComposedCz, string ComposedSk, List<string> PositiveCz, List<string> PositiveSk, List<string> Negative);

    /// <summary>Sentences of the research test script (podklady/reserse/zakonne-poziadavky-ako-prednost.md, oddíl 3.4).</summary>
    private static List<Case> Cases()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "legal_requirements_cases.json")));
        return json.RootElement.GetProperty("claims").EnumerateArray().Select(c => new Case(
            c.GetProperty("id").GetString()!,
            c.GetProperty("composed_cz").GetString()!,
            c.GetProperty("composed_sk").GetString()!,
            Strings(c, "positive_cz"),
            Strings(c, "positive_sk"),
            Strings(c, "negative"))).ToList();
    }

    private static List<string> Strings(JsonElement element, string property) =>
        element.GetProperty(property).EnumerateArray().Select(s => s.GetString()!).ToList();
}
