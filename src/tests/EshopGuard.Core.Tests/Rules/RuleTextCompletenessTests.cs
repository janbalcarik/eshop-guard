using EshopGuard.Core.Models;
using EshopGuard.Core.Rules;
using EshopGuard.Core.Rules.Texts;

namespace EshopGuard.Core.Tests;

/// <summary>
/// Texts of rules in language files (change 6): completeness of every language, the same placeholders, only translations
/// reviewed by a person are used, a set without texts or with texts left in the rule file does not load.
/// </summary>
public sealed class RuleTextCompletenessTests
{
    [Fact]
    public void Czech_HasEveryTextOfTheTool()
    {
        var texts = TestTexts.Catalog.Texts;

        Assert.Contains("cs", texts.ToolLocales);
        var engine = texts.Locales["cs"].Engine!;
        Assert.All(EngineCodes.All, code => Assert.False(string.IsNullOrWhiteSpace(engine.Codes.GetValueOrDefault(code)), code));
        Assert.All(TestTexts.Catalog.Jurisdictions.Codes, j => Assert.False(string.IsNullOrWhiteSpace(engine.Jurisdictions.GetValueOrDefault(j)), j));
        Assert.All(TestTexts.Catalog.Labels.Notes, n => Assert.False(string.IsNullOrWhiteSpace(texts.Locales["cs"].Labels!.Notes.GetValueOrDefault(n.Id)), n.Id));
    }

    [Fact]
    public void EveryEnabledSet_HasItsOriginalTexts()
    {
        var catalog = TestTexts.Catalog;

        foreach (var set in catalog.RuleSets.Where(s => s.Enabled))
        {
            var (file, locale) = catalog.Texts.SetTexts(set.Name, "cs")!.Value;
            Assert.Equal(set.Name == "legal_sk" ? "sk" : "cs", locale);
            Assert.All(set.Rules, r => Assert.True(file.Rules.ContainsKey(r.Id), $"{set.Name}/{r.Id}"));
            Assert.All(set.Rules, r => Assert.False(r.HasInlineTexts, $"{set.Name}/{r.Id}"));
        }

        // Until the Czech translation of legal_sk is reviewed (proposal, decision 8), Czech is not complete only because of it.
        Assert.All(catalog.Texts.Problems["cs"], p => Assert.StartsWith("cs/legal_sk.yaml", p, StringComparison.Ordinal));
    }

    /// <summary>Passes once the Slovak texts are translated and reviewed (tasks 4.4 and 4.5 of change 6).</summary>
    [Fact(Explicit = true)]
    [Trait("Category", "PendingTranslation")]
    public void SlovakAndCzech_AreComplete()
    {
        Assert.Equal(["cs", "sk"], TestTexts.Catalog.CompleteLocales);
    }

    [Theory]
    [InlineData("cs")]
    [InlineData("sk")]
    public async Task NumbersFollowTheLanguage(string locale)
    {
        using var rules = new TempRules();
        var renderer = new RuleTextRenderer(await rules.LoadAsync());
        var note = new FindingNote(EngineCodes.PresenceClosestParagraph, NoteParams.Of(("probability", 0.4213), ("threshold", 0.7)));

        // Slovak texts of the tool are not complete yet, so a Slovak reader gets the Czech note; the number is formatted the same.
        Assert.Contains("0,42", renderer.Note(note, locale), StringComparison.Ordinal);
        Assert.Contains("0,70", renderer.Note(note, locale), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DifferentPlaceholders_AreReportedAndTheTranslationIsNotUsed()
    {
        using var rules = new TempRules();
        rules.Replace("rules/texts/sk/_engine.yaml", "  presence_closest_paragraph: \"\"", "  presence_closest_paragraph: \"Najbližší odsek má pravdepodobnosť {probability:0.00}.\"");

        var catalog = await rules.LoadAsync();

        Assert.Contains(catalog.Texts.Problems["sk"], p => p.Contains("presence_closest_paragraph", StringComparison.Ordinal) && p.Contains("chybí {threshold}", StringComparison.Ordinal));
        Assert.DoesNotContain("sk", catalog.Texts.ToolLocales);
    }

    [Fact]
    public async Task MachineDraft_IsNotUsedUntilReviewed()
    {
        using var rules = new TempRules();
        var translation = Translation(rules, "eco", review: "  machine_draft: true\n  translated_by: \"gpt-6.1-sol\"\n");
        rules.Write("rules/texts/sk/eco.yaml", translation);

        var catalog = await rules.LoadAsync();

        Assert.Contains(catalog.Texts.Problems["sk"], p => p.StartsWith("sk/eco.yaml", StringComparison.Ordinal) && p.Contains("machine_draft", StringComparison.Ordinal));
        Assert.Equal("cs", catalog.Texts.SetTexts("eco", "sk")!.Value.Locale);
    }

    [Fact]
    public async Task ReviewedTranslation_IsUsedWithTheExplanationOfTheJurisdiction()
    {
        using var rules = new TempRules();
        var translation = Translation(rules, "eco", review: "  translated_by: \"překladatel\"\n  reviewed_by: \"kontrolor\"\n  reviewed_at: \"2026-10-02\"\n")
            .Replace("    title: \"Obecné environmentální tvrzení bez upřesnění\"", "    title: \"Všeobecné environmentálne tvrdenie bez spresnenia\"", StringComparison.Ordinal)
            .Replace("      cz: \"V Česku zatím výslovný zákaz", "      cz: \"SK: V Česku zatím výslovný zákaz", StringComparison.Ordinal);
        rules.Write("rules/texts/sk/eco.yaml", translation);
        var catalog = await rules.LoadAsync();
        var finding = new Finding
        {
            RuleId = "eco_generic_claim",
            Module = "eco",
            Scope = "segment",
            Verdicts = [new JurisdictionVerdict { Jurisdiction = "cz", Severity = "high", Checkability = "text", RuleSet = "eco", RuleSetVersion = "x", ExplanationVariant = "cz" }],
        };

        var rendered = new RuleTextRenderer(catalog).Render(finding, "sk");

        Assert.Equal("sk", rendered.Locale);
        Assert.Equal("Všeobecné environmentálne tvrdenie bez spresnenia", rendered.Title);
        Assert.StartsWith("SK: V Česku zatím", rendered.Explanation, StringComparison.Ordinal);
        Assert.DoesNotContain(catalog.Texts.Problems["sk"], p => p.StartsWith("sk/eco.yaml", StringComparison.Ordinal));
    }

    [Fact]
    public async Task MissingKeyInATranslation_IsAProblemOfThatFile()
    {
        using var rules = new TempRules();
        var translation = Translation(rules, "ucp", review: "  reviewed_by: \"kontrolor\"\n  reviewed_at: \"2026-10-02\"\n");
        var start = translation.IndexOf("  ucp_reviews_only_positive:", StringComparison.Ordinal);
        Assert.True(start > 0);
        var recommendation = translation.IndexOf("    recommendation:", start, StringComparison.Ordinal);
        var end = translation.IndexOf('\n', recommendation);
        rules.Write("rules/texts/sk/ucp.yaml", translation.Remove(recommendation, end - recommendation + 1));

        var catalog = await rules.LoadAsync();

        Assert.Contains(catalog.Texts.Problems["sk"], p => p == "sk/ucp.yaml: pravidlo „ucp_reviews_only_positive“: chybí recommendation.");
        Assert.DoesNotContain("sk", catalog.CompleteLocales);
    }

    [Fact]
    public async Task TextLeftInAnEnabledRuleSet_StopsTheLoad()
    {
        using var rules = new TempRules();
        rules.Replace("rules/ucp.yaml", "  - id: ucp_reviews_only_positive\n", "  - id: ucp_reviews_only_positive\n    title: \"Jen kladné recenze\"\n");

        var error = await Assert.ThrowsAsync<RuleValidationException>(rules.LoadAsync);

        Assert.Contains(error.Errors, e => e.StartsWith("ucp.yaml: pravidlo „ucp_reviews_only_positive“ má title", StringComparison.Ordinal));
    }

    [Fact]
    public async Task EnabledSetWithoutTexts_StopsTheLoad()
    {
        using var rules = new TempRules();
        File.Delete(Path.Combine(rules.TextsDirectory, "cs", "dur.yaml"));

        var error = await Assert.ThrowsAsync<RuleValidationException>(rules.LoadAsync);

        Assert.Contains(error.Errors, e => e.StartsWith("dur.yaml: chybí soubor textů rules/texts/<jazyk>/dur.yaml", StringComparison.Ordinal));
    }

    [Fact]
    public void TextsFolder_IsNotARuleSet()
    {
        Assert.DoesNotContain(TestTexts.Catalog.RuleSets, s => s.SourceFile.Contains("texts", StringComparison.Ordinal) || s.Name.StartsWith('_'));
        Assert.Equal(7, TestTexts.Catalog.RuleSets.Count);
    }

    /// <summary>The original Czech texts of a set as a Slovak translation with the given review.</summary>
    private static string Translation(TempRules rules, string set, string review)
    {
        var original = rules.Text($"rules/texts/cs/{set}.yaml");
        var start = original.IndexOf("review:\n", StringComparison.Ordinal);
        var end = original.IndexOf("rules:\n", StringComparison.Ordinal);
        return original[..start].Replace("locale: cs", "locale: sk", StringComparison.Ordinal) + "review:\n" + review + original[end..];
    }
}
