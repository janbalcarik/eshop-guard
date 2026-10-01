using EshopGuard.Core.Rules;
using EshopGuard.Core.Rules.Texts;

namespace EshopGuard.Core.Tests;

/// <summary>
/// The description of a rule set version for the database and the guard of versions (change 6): texts change the source
/// fingerprint only; questions changed without a new version do not load.
/// </summary>
public sealed class RuleSetDescriptorTests
{
    [Fact]
    public async Task FixedTypoInAText_ChangesOnlyTheSourceHash()
    {
        using var rules = new TempRules();
        var before = (await rules.LoadAsync()).Describe("jev-test").Single(d => d.Name == "eco");
        rules.Replace("rules/texts/cs/eco.yaml", "Obecné environmentální tvrzení bez upřesnění", "Obecné environmentální tvrzení bez upřesnění.");

        var after = (await rules.LoadAsync()).Describe("jev-test").Single(d => d.Name == "eco");

        Assert.NotEqual(before.SourceHash, after.SourceHash);
        Assert.Equal(before.QuestionSetHash, after.QuestionSetHash);
        Assert.Equal(before.QuestionsHash, after.QuestionsHash);
        Assert.Equal(before.Version, after.Version);
        Assert.Equal(before.Definition, after.Definition);
    }

    [Fact]
    public void QuestionSetHash_IsTheOneOfTheCacheKeys()
    {
        var descriptor = TestTexts.Catalog.Describe("jev-1.13.0", "en").Single(d => d.Name == "ucp");
        var set = TestTexts.Catalog.RuleSets.Single(s => s.Name == "ucp");
        var questions = SegmentEvaluator.BuildQuestions(set, "en");

        Assert.Equal(Storage.JevCacheKeys.Create(Storage.JevCacheKind.Detail, "jev-1.13.0", set.Version, "en", questions, "x").QuestionSetHash, descriptor.QuestionSetHash);
        Assert.Equal(["cs"], descriptor.CompleteLocales);
        Assert.True(descriptor.Enabled);
    }

    [Fact]
    public async Task ChangedQuestionWithoutANewVersion_DoesNotLoad()
    {
        using var rules = new TempRules();
        rules.Replace("rules/ucp.yaml", "text_en: \"", "text_en: \"Changed: ");

        var error = await Assert.ThrowsAsync<RuleValidationException>(rules.LoadAsync);

        Assert.Contains(error.Errors, e => e.StartsWith("ucp.yaml: otázky se změnily, ale version", StringComparison.Ordinal));
    }

    [Fact]
    public async Task NewVersion_NeedsItsFingerprint()
    {
        using var rules = new TempRules();
        rules.Replace("rules/ucp.yaml", "version: \"ucp-2026-09-26-draft4\"", "version: \"ucp-2026-10-02-draft5\"");
        rules.Replace("rules/texts/cs/ucp.yaml", "source_version: \"ucp-2026-09-26-draft4\"", "source_version: \"ucp-2026-10-02-draft5\"");

        var error = await Assert.ThrowsAsync<RuleValidationException>(rules.LoadAsync);
        Assert.Contains(error.Errors, e => e.Contains("ucp-2026-10-02-draft5 nemá otisk otázek", StringComparison.Ordinal));

        var (added, errors) = await RuleMaintenance.UpdateQuestionSetHashesAsync(new Options.RulesOptions { Directory = rules.RulesDirectory }, TestContext.Current.CancellationToken);
        Assert.Empty(errors);
        Assert.Equal(["ucp ucp-2026-10-02-draft5"], added);
        Assert.NotNull(await rules.LoadAsync());
    }
}
