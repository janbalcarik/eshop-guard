using System.Text.RegularExpressions;
using EshopGuard.Core.Options;
using EshopGuard.Core.Rules;

namespace EshopGuard.Core.Tests;

/// <summary>
/// A copy of the shipped rules, texts and configuration in a temporary folder, for tests that change files (a translation,
/// a new market, a changed question) without touching the repository.
/// </summary>
internal sealed partial class TempRules : IDisposable
{
    public TempRules()
    {
        Root = Directory.CreateTempSubdirectory("eshopguard-rules-").FullName;
        Copy(TestServices.RulesDirectory, RulesDirectory);
        Directory.CreateDirectory(ConfigDirectory);
        foreach (var file in new[] { TestServices.LabelsFile, TestServices.LegalRequirementsFile, TestServices.SieveFile, TestServices.JurisdictionsFile })
        {
            File.Copy(file, Path.Combine(ConfigDirectory, Path.GetFileName(file)));
        }
    }

    public string Root { get; }

    public string RulesDirectory => Path.Combine(Root, "rules");

    public string TextsDirectory => Path.Combine(RulesDirectory, "texts");

    public string ConfigDirectory => Path.Combine(Root, "config");

    /// <summary>Points the rules of <paramref name="options"/> to this copy.</summary>
    public void Apply(EshopGuardOptions options)
    {
        options.Rules.Directory = RulesDirectory;
        options.Rules.TextsDirectory = null;
        options.Rules.LabelsFile = Path.Combine(ConfigDirectory, "labels.yaml");
        options.Rules.LegalRequirementsFile = Path.Combine(ConfigDirectory, "legal_requirements.yaml");
        options.Rules.SieveFile = Path.Combine(ConfigDirectory, "sieve.yaml");
        options.Rules.JurisdictionsFile = Path.Combine(ConfigDirectory, "jurisdictions.yaml");
    }

    public Task<RuleCatalog> LoadAsync() =>
        TestTexts.Provider(RulesDirectory, Path.Combine(ConfigDirectory, "labels.yaml"), Apply).LoadAsync(TestContext.Current.CancellationToken);

    public string Text(string relativePath) => File.ReadAllText(Path.Combine(Root, relativePath));

    public void Write(string relativePath, string content)
    {
        var path = Path.Combine(Root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    public void Replace(string relativePath, string oldValue, string newValue)
    {
        var text = Text(relativePath);
        Assert.Contains(oldValue, text, StringComparison.Ordinal);
        Write(relativePath, text.Replace(oldValue, newValue, StringComparison.Ordinal));
    }

    /// <summary>
    /// Marks every machine draft of a translation as reviewed by a person, as the reviewer will after checking it
    /// (removes <c>machine_draft</c>, fills <c>reviewed_by</c> and <c>reviewed_at</c>). Returns the reviewed files.
    /// </summary>
    public IReadOnlyList<string> ReviewDrafts()
    {
        var reviewed = new List<string>();
        foreach (var file in Directory.GetFiles(TextsDirectory, "*.yaml", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var text = File.ReadAllText(file);
            if (!DraftLine().IsMatch(text))
            {
                continue;
            }

            text = DraftLine().Replace(text, "")
                .Replace("  reviewed_by: \"\"", "  reviewed_by: \"kontrolor\"", StringComparison.Ordinal)
                .Replace("  reviewed_at: \"\"", "  reviewed_at: \"2026-10-02\"", StringComparison.Ordinal);
            File.WriteAllText(file, text);
            reviewed.Add(Path.GetRelativePath(TextsDirectory, file).Replace('\\', '/'));
        }

        return reviewed;
    }

    public void Dispose() => Directory.Delete(Root, recursive: true);

    [GeneratedRegex(@"^  machine_draft: true\r?\n", RegexOptions.Multiline)]
    private static partial Regex DraftLine();

    private static void Copy(string source, string target)
    {
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var destination = Path.Combine(target, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination);
        }
    }
}
