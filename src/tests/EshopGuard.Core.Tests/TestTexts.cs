using EshopGuard.Core.Models;
using EshopGuard.Core.Options;
using EshopGuard.Core.Report;
using EshopGuard.Core.Rules;
using EshopGuard.Core.Rules.Texts;
using Microsoft.Extensions.Logging.Abstractions;

namespace EshopGuard.Core.Tests;

/// <summary>
/// The shipped rules and their texts for tests that check what a person reads: warnings, notes and titles composed in
/// Czech from the codes of the result.
/// </summary>
internal static class TestTexts
{
    private static readonly Lazy<RuleCatalog> LazyCatalog = new(() => Provider().LoadAsync().GetAwaiter().GetResult());

    /// <summary>The shipped rule catalog.</summary>
    public static RuleCatalog Catalog => LazyCatalog.Value;

    /// <summary>Renderer over the shipped texts.</summary>
    public static RuleTextRenderer Renderer { get; } = new(Catalog);

    /// <summary>A provider of the shipped rules (or of another rules folder and labels file).</summary>
    public static YamlRuleSetProvider Provider(string? directory = null, string? labelsFile = null, Action<EshopGuardOptions>? configure = null)
    {
        var options = new EshopGuardOptions();
        options.Rules.Directory = directory ?? TestServices.RulesDirectory;
        options.Rules.LabelsFile = labelsFile ?? TestServices.LabelsFile;
        options.Rules.LegalRequirementsFile = TestServices.LegalRequirementsFile;
        options.Rules.SieveFile = TestServices.SieveFile;
        options.Rules.JurisdictionsFile = TestServices.JurisdictionsFile;
        options.Rules.TextsDirectory = Path.Combine(TestServices.RulesDirectory, "texts");
        configure?.Invoke(options);
        return new YamlRuleSetProvider(Microsoft.Extensions.Options.Options.Create(options), NullLogger<YamlRuleSetProvider>.Instance);
    }

    /// <summary>Warnings in Czech.</summary>
    public static List<string> Warnings(IEnumerable<ScanWarning> warnings) => warnings.Select(w => Renderer.Warning(w, "cs")).ToList();

    /// <summary>Notes of every verdict of the finding in Czech.</summary>
    public static List<string> Notes(Finding finding) => FindingDocument.RenderNotes(finding, Renderer, "cs", severalJurisdictions: false);

    /// <summary>Title of the strictest verdict in Czech.</summary>
    public static string Title(Finding finding) => Renderer.Render(finding, "cs").Title;
}
