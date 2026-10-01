using EshopGuard.Core.Extract;
using EshopGuard.Core.Models;
using EshopGuard.Core.Rules;

namespace EshopGuard.Core.Pipeline;

/// <summary>
/// Applies the rule sets to the evaluated segments and the pages of the site: findings of sentences and paragraphs, of
/// information missing on the site, of pages whose text was not loaded and of documents that were not read.
/// </summary>
internal sealed class RulesStep
{
    /// <summary>Order of modules in reports; modules not listed here follow alphabetically.</summary>
    private static readonly string[] ModuleOrder = ["eco", "dur", "lr", "ucp", "legal"];

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
            Country = input.Country,
            UncheckedDocuments = input.UncheckedDocuments,
            TextNotLoadedPages = input.TextNotLoadedPages,
            AddMissingLegalPagesFinding = input.AddMissingLegalPagesFinding,
        });
    }

    /// <summary>The input of a scan: texts of the analyzed pages, signals of all pages.</summary>
    public static RulesInput ForScan(
        string country, IReadOnlyList<Segment> segments, IReadOnlyList<ExtractedPageRecord> pages, IReadOnlyList<UncheckedDocument> uncheckedDocuments,
        IReadOnlyList<PageInfo> textNotLoaded, bool addMissingLegalPagesFinding)
    {
        var analyzed = pages.Where(p => p.Info.IncludedInAnalysis).ToList();
        return new RulesInput(
            country,
            segments,
            analyzed.ToDictionary(p => p.Info.Url, p => PageText(p.Info, p.Content)),
            pages.ToDictionary(p => p.Info.Url, p => Signals(p.Info, p.Content)),
            analyzed.ToDictionary(p => p.Info.Url, p => p.Info.Category),
            uncheckedDocuments,
            textNotLoaded,
            addMissingLegalPagesFinding);
    }

    /// <summary>The rule sets of the chosen modules for the country; a module without enabled rules is reported.</summary>
    public static List<RuleSet> SelectRuleSets(RuleCatalog catalog, IReadOnlyList<string> modules, string country, List<string> warnings)
    {
        if (modules.Count == 0)
        {
            // No explicit choice: every module with enabled rules for the country, without warnings for the others.
            modules = catalog.RuleSets
                .Where(s => s.Enabled && s.Jurisdictions.Contains(country))
                .Select(s => s.Module)
                .Distinct()
                .OrderBy(m => Array.IndexOf(ModuleOrder, m) is var i && i >= 0 ? i : ModuleOrder.Length)
                .ThenBy(m => m, StringComparer.Ordinal)
                .ToList();
        }

        var selected = new List<RuleSet>();
        foreach (var module in modules)
        {
            var sets = catalog.RuleSets.Where(s => s.Module == module && s.Jurisdictions.Contains(country)).ToList();
            if (sets.Count == 0)
            {
                var elsewhere = catalog.RuleSets.Where(s => s.Module == module).SelectMany(s => s.Jurisdictions).Distinct().Order().ToList();
                warnings.Add(elsewhere.Count > 0
                    ? $"Modul {module} má pravidla jen pro {string.Join(", ", elsewhere)}, pro zemi {country} se nespustil."
                    : $"Pro modul {module} neexistuje žádná sada pravidel, modul se nespustil.");
            }
            else if (!sets.Any(s => s.Enabled))
            {
                warnings.Add($"Sada pravidel modulu {module} pro zemi {country} je vypnutá ({string.Join(", ", sets.Select(s => s.SourceFile))}), modul se nespustil.");
            }

            selected.AddRange(sets.Where(s => s.Enabled));
        }

        return selected;
    }

    /// <summary>Modules whose sentences the sieve asks first: sentence modules with a sieve question.</summary>
    public static List<string> SieveModules(SieveDefinition? sieve, IReadOnlyList<RuleSet> ruleSets) =>
        sieve is null
            ? []
            : ruleSets.Where(s => s.AppliesTo == RuleValidator.Sentence && sieve.Questions.ContainsKey(s.Module)).Select(s => s.Module).Distinct().ToList();

    public static RuleSetInfo Describe(RuleSet set) => new()
    {
        Module = set.Module,
        Version = set.Version,
        File = set.SourceFile,
        QuestionIds = set.Questions.Keys.ToList(),
    };

    /// <summary>
    /// A page whose text was not in the HTML must never pass for a page without findings: the report says so,
    /// and when most of the site is like that, it says the whole result is incomplete.
    /// </summary>
    internal static IEnumerable<string> NotLoadedWarnings(int notLoaded, int pages)
    {
        if (notLoaded == 0)
        {
            return [];
        }

        var warnings = new List<string>
        {
            $"{notLoaded} z {pages} stažených stránek nemělo v HTML skoro žádný čitelný text; web je nejspíš vykresluje až JavaScriptem, "
            + "který nástroj nespouští. Na těchto stránkách je zkontrolovaný jen titulek, meta popis a popis z dat pro vyhledávače (JSON-LD); "
            + "to, že u nich nejsou nálezy, neznamená, že jsou v pořádku. Seznam je v části „Co nebylo zkontrolováno“.",
        };
        if (notLoaded * 2 >= pages)
        {
            warnings.Add("Web je z velké části vykreslovaný JavaScriptem, kontrola je proto neúplná. "
                + "Spolehlivý výsledek dá připojení e-shopu přes konektor nebo produktový feed.");
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
