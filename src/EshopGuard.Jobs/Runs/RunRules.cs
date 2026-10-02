using EshopGuard.Core.Models;
using EshopGuard.Core.Pipeline;
using EshopGuard.Core.Rules;

namespace EshopGuard.Jobs.Runs;

/// <summary>The rule sets of a run, chosen as the CLI chooses them (<c>InMemoryPipelineRunner</c>): every step reads the same.</summary>
internal sealed record RunRules(
    RuleCatalog Catalog,
    IReadOnlyList<string> Jurisdictions,
    IReadOnlyList<RuleSet> RuleSets,
    IReadOnlyList<ScanWarning> Warnings,
    IReadOnlyList<JurisdictionCoverage> Coverage,
    SieveDefinition? Sieve,
    IReadOnlyList<string> SieveModules)
{
    public static async Task<RunRules> LoadAsync(IRuleSetProvider provider, RunRow run, RunsOptions runs, CancellationToken ct)
    {
        var catalog = await provider.LoadAsync(ct).ConfigureAwait(false);
        IReadOnlyList<string> jurisdictions = run.Jurisdictions.Count > 0 ? run.Jurisdictions : [run.ShopHomeCountry.ToLowerInvariant()];
        var selection = RuleSetSelector.Select(catalog, run.Modules, jurisdictions);

        // The sieve asks the topic of the sentence modules that have a sieve question; legal paragraphs always go in full.
        var sieve = runs.UseSieve ? catalog.Sieve : null;
        var sieveModules = RulesStep.SieveModules(sieve, selection.RuleSets);
        if (sieveModules.Count == 0)
        {
            sieve = null;
        }

        return new RunRules(catalog, jurisdictions, selection.RuleSets, selection.Warnings, selection.Coverage, sieve, sieveModules);
    }
}
