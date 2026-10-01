using EshopGuard.Core.Models;

namespace EshopGuard.Core.Rules;

/// <summary>
/// The rule sets of a run: enabled sets of the chosen modules that have at least one chosen jurisdiction. Every set is asked
/// once (Jev answers are shared) and the engine evaluates it for each of its chosen jurisdictions. What did not run, and why,
/// is in <see cref="JurisdictionCoverage"/>; an explicitly requested module that did not run is also a warning.
/// </summary>
internal static class RuleSetSelector
{
    /// <summary>Order of modules in reports; modules not listed here follow alphabetically.</summary>
    private static readonly string[] ModuleOrder = ["eco", "dur", "lr", "ucp", "legal"];

    public static Selection Select(RuleCatalog catalog, IReadOnlyList<string> modules, IReadOnlyList<string> jurisdictions)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var warnings = new List<ScanWarning>();
        var explicitModules = modules.Count > 0;
        if (!explicitModules)
        {
            // No explicit choice: every module with enabled rules for a chosen jurisdiction, without warnings for the others.
            modules = catalog.RuleSets
                .Where(s => s.Enabled && s.Jurisdictions.Any(jurisdictions.Contains))
                .Select(s => s.Module)
                .Distinct()
                .OrderBy(m => Array.IndexOf(ModuleOrder, m) is var i && i >= 0 ? i : ModuleOrder.Length)
                .ThenBy(m => m, StringComparer.Ordinal)
                .ToList();
        }

        var coverage = jurisdictions.ToDictionary(j => j, _ => (Ran: new List<string>(), NotRun: new List<ModuleNotRun>()), StringComparer.Ordinal);
        var selected = new List<RuleSet>();
        foreach (var module in modules)
        {
            var ofModule = catalog.RuleSets.Where(s => s.Module == module).ToList();
            if (ofModule.Count == 0)
            {
                warnings.Add(new ScanWarning(EngineCodes.ModuleNoRuleSet, NoteParams.Of(("module", module))));
                foreach (var jurisdiction in jurisdictions)
                {
                    coverage[jurisdiction].NotRun.Add(new ModuleNotRun(module, EngineCodes.NoRulesForJurisdiction));
                }

                continue;
            }

            foreach (var jurisdiction in jurisdictions)
            {
                var sets = ofModule.Where(s => s.Jurisdictions.Contains(jurisdiction)).ToList();
                if (sets.Count == 0)
                {
                    var elsewhere = ofModule.SelectMany(s => s.Jurisdictions).Distinct().Order(StringComparer.Ordinal).ToList();
                    if (explicitModules)
                    {
                        warnings.Add(new ScanWarning(EngineCodes.ModuleOtherJurisdiction, NoteParams.Of(("module", module), ("jurisdictions", elsewhere), ("country", jurisdiction))));
                    }

                    coverage[jurisdiction].NotRun.Add(new ModuleNotRun(module, EngineCodes.NoRulesForJurisdiction));
                }
                else if (!sets.Any(s => s.Enabled))
                {
                    if (explicitModules)
                    {
                        warnings.Add(new ScanWarning(EngineCodes.ModuleDisabled, NoteParams.Of(("module", module), ("country", jurisdiction), ("files", sets.Select(s => s.SourceFile).ToList()))));
                    }

                    coverage[jurisdiction].NotRun.Add(new ModuleNotRun(module, EngineCodes.RuleSetDisabled));
                }
                else
                {
                    coverage[jurisdiction].Ran.Add(module);
                }
            }

            selected.AddRange(ofModule.Where(s => s.Enabled && s.Jurisdictions.Any(jurisdictions.Contains)));
        }

        return new Selection(
            selected,
            warnings,
            jurisdictions.Select(j => new JurisdictionCoverage { Jurisdiction = j, Modules = coverage[j].Ran, NotRun = coverage[j].NotRun }).ToList());
    }

    /// <summary>Rule sets of a run, warnings for requested modules that did not run, and coverage of every jurisdiction.</summary>
    public sealed record Selection(IReadOnlyList<RuleSet> RuleSets, IReadOnlyList<ScanWarning> Warnings, IReadOnlyList<JurisdictionCoverage> Coverage);
}
