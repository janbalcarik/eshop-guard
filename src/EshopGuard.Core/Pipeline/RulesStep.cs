using EshopGuard.Core.Extract;
using EshopGuard.Core.Models;
using EshopGuard.Core.Rules;

namespace EshopGuard.Core.Pipeline;

/// <summary>
/// Applies the rule sets to the evaluated segments and the pages of the site for every chosen jurisdiction: findings of
/// sentences and paragraphs, of information missing on the site, of pages whose text was not loaded and of documents that
/// were not read, and the obligations of the whole site. Effective dates are compared with the date of the run.
/// </summary>
internal sealed class RulesStep(TimeProvider clock)
{
    public RuleEngineOutput Evaluate(RulesInput input, RuleCatalog catalog, IReadOnlyList<RuleSet> ruleSets)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(catalog);
        return RuleEngine.Evaluate(new RuleEngineInput
        {
            RuleSets = ruleSets,
            Labels = catalog.Labels,
            Segments = input.Segments,
            PageTexts = input.PageTexts,
            PageSignals = input.PageSignals,
            EvaluateSiteSignals = input.EvaluateSiteSignals,
            EvaluateSitePresence = input.EvaluateSitePresence,
            LegalRequirements = catalog.LegalRequirements,
            PageCategories = input.PageCategories,
            Jurisdictions = input.Jurisdictions,
            AsOf = input.AsOf ?? Today,
            UncheckedDocuments = input.UncheckedDocuments,
            TextNotLoadedPages = input.TextNotLoadedPages,
            AddMissingLegalPagesFinding = input.AddMissingLegalPagesFinding,
        });
    }

    /// <summary>Today by the clock of the step (UTC).</summary>
    public DateOnly Today => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

    /// <summary>The input of a scan: texts of the analyzed pages, signals of all pages.</summary>
    public static RulesInput ForScan(
        IReadOnlyList<string> jurisdictions, IReadOnlyList<Segment> segments, IReadOnlyList<ExtractedPageRecord> pages, IReadOnlyList<UncheckedDocument> uncheckedDocuments,
        IReadOnlyList<PageInfo> textNotLoaded, bool addMissingLegalPagesFinding)
    {
        var analyzed = pages.Where(p => p.Info.IncludedInAnalysis).ToList();
        return new RulesInput(
            jurisdictions,
            segments,
            analyzed.ToDictionary(p => p.Info.Url, p => PageText(p.Info, p.Content)),
            pages.ToDictionary(p => p.Info.Url, p => Signals(p.Info, p.Content)),
            analyzed.ToDictionary(p => p.Info.Url, p => p.Info.Category),
            uncheckedDocuments,
            textNotLoaded,
            addMissingLegalPagesFinding);
    }

    /// <summary>Modules whose sentences the sieve asks first: sentence modules with a sieve question.</summary>
    public static List<string> SieveModules(SieveDefinition? sieve, IReadOnlyList<RuleSet> ruleSets) =>
        sieve is null
            ? []
            : ruleSets.Where(s => s.AppliesTo == RuleValidator.Sentence && sieve.Questions.ContainsKey(s.Module)).Select(s => s.Module).Distinct().ToList();

    public static RuleSetInfo Describe(RuleSet set, IReadOnlyList<string> jurisdictions) => new()
    {
        Module = set.Module,
        Version = set.Version,
        File = set.SourceFile,
        Name = set.Name,
        Jurisdictions = set.Jurisdictions.Where(jurisdictions.Contains).Order(StringComparer.Ordinal).ToList(),
        QuestionIds = set.Questions.Keys.ToList(),
    };

    /// <summary>
    /// A page whose text was not in the HTML must never pass for a page without findings: the report says so,
    /// and when most of the site is like that, it says the whole result is incomplete.
    /// </summary>
    internal static IEnumerable<ScanWarning> NotLoadedWarnings(int notLoaded, int pages)
    {
        if (notLoaded == 0)
        {
            return [];
        }

        var warnings = new List<ScanWarning> { new(EngineCodes.TextNotLoaded, NoteParams.Of(("count", notLoaded), ("pages", pages))) };
        if (notLoaded * 2 >= pages)
        {
            warnings.Add(new ScanWarning(EngineCodes.MostlyScriptRendered, NoteParams.None));
        }

        return warnings;
    }

    /// <summary>Everything a customer sees on the page, for page-level allowlists: text, image alt texts and file names.</summary>
    private static string PageText(PageInfo info, ExtractedPage content) =>
        string.Join("\n",
            new[] { content.Title, content.MetaDescription, content.JsonLdDescription }.OfType<string>()
                .Concat(content.MainBlocks.Select(b => b.Text))
                .Concat(content.ChromeRegions.SelectMany(r => r).Select(b => b.Text))
                .Concat(content.RestBlocks.Select(b => b.Text))
                .Concat(content.ProfileSkippedBlocks.Select(b => b.Text))
                .Concat(info.Images.Select(i => $"{i.Alt} {i.FileName}")));

    /// <summary>What a customer can see on the page, split into visible text, links and images, for site_signal rules.</summary>
    private static PageSignals Signals(PageInfo info, ExtractedPage content) =>
        new(
            PageText(info, content),
            string.Join("\n", content.Links.Select(l => $"{l.Text} {l.Url.AbsoluteUri}")),
            string.Join("\n", info.Images.Select(i => $"{i.Alt} {i.Src}")));
}
