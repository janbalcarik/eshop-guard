using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using EshopGuard.Core.Options;
using EshopGuard.Core.Storage;

namespace EshopGuard.Core.Rules.Texts;

/// <summary>
/// Maintenance of rule files for the CLI (<c>eshopguard rules …</c>): moving texts out of rule sets, skeletons of a new
/// language and the fingerprints of question sets. These work on the files as they are, without the full validation, which
/// they prepare.
/// </summary>
public static partial class RuleMaintenance
{
    private static readonly string[] TextFields = ["title", "explanation", "explanation_by_jurisdiction", "recommendation"];

    /// <summary>
    /// Moves title, explanation, explanations by jurisdiction and recommendation of every enabled rule set from
    /// <c>rules/&lt;set&gt;.yaml</c> to <c>rules/texts/&lt;locale&gt;/&lt;set&gt;.yaml</c> unchanged (the language of a set is
    /// <paramref name="setLocales"/>[set], otherwise <paramref name="defaultLocale"/>), and the remarks on labels from the
    /// labels file to <c>_labels.yaml</c>. The rule file keeps everything else line by line, comments included; the command
    /// checks that the set is the same without its texts. Disabled sets keep their texts until they are enabled.
    /// </summary>
    /// <returns>Written files and errors; with an error nothing of that set is changed.</returns>
    public static async Task<(List<string> Written, List<string> Errors)> ExtractTextsAsync(
        RulesOptions options, string defaultLocale, IReadOnlyDictionary<string, string> setLocales, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(setLocales);
        var written = new List<string>();
        var errors = new List<string>();
        var today = DateOnly.FromDateTime(DateTime.Today).ToString("d. M. yyyy", CultureInfo.GetCultureInfo("cs-CZ"));
        foreach (var file in Directory.GetFiles(options.Directory, "*.yaml").Order(StringComparer.Ordinal))
        {
            var set = await YamlRuleSetProvider.LoadRuleSetAsync(file, errors, ct);
            if (set is null || !set.Enabled || !set.Rules.Any(r => r.HasInlineTexts))
            {
                continue;
            }

            var locale = setLocales.GetValueOrDefault(set.Name, defaultLocale);
            var target = Path.Combine(options.ResolvedTextsDirectory, locale, set.Name + ".yaml");
            if (File.Exists(target))
            {
                errors.Add($"{target} už existuje; texty sady {set.Name} se nepřesunuly.");
                continue;
            }

            var texts = SetTexts(set, locale, today);
            var lines = await File.ReadAllLinesAsync(file, ct);
            var kept = RemoveTextLines(lines);
            var check = new List<string>();
            var stripped = await ParseAsync(kept, set.SourceFile, check, ct);
            if (stripped is null || check.Count > 0 || stripped.Rules.Any(r => r.HasInlineTexts) || Definition(stripped) != Definition(WithoutTexts(set)))
            {
                errors.Add($"{set.SourceFile}: po odebrání textů by se sada změnila jinak než o texty ({string.Join(" ", check)}); soubor zůstal beze změny.");
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await File.WriteAllTextAsync(target, texts, new UTF8Encoding(false), ct);
            await File.WriteAllLinesAsync(file, kept, new UTF8Encoding(false), ct);
            written.Add(target);
            written.Add(file);
        }

        var labels = await ExtractLabelNotesAsync(options, defaultLocale, today, errors, ct);
        written.AddRange(labels);
        return (written, errors);
    }

    /// <summary>
    /// Writes the skeleton of a language: every file of the original texts with the same keys and empty values, and an empty
    /// <c>review</c>, for a translator. Existing files are left alone.
    /// </summary>
    public static async Task<List<string>> WriteSkeletonAsync(RulesOptions options, string locale, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var root = options.ResolvedTextsDirectory;
        var written = new List<string>();
        var originals = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var folder in Directory.GetDirectories(root).Order(StringComparer.Ordinal))
        {
            foreach (var path in Directory.GetFiles(folder, "*.yaml").Order(StringComparer.Ordinal))
            {
                var name = Path.GetFileName(path);
                if (!originals.ContainsKey(name) && OriginalMarker().IsMatch(await File.ReadAllTextAsync(path, ct)))
                {
                    originals[name] = path;
                }
            }
        }

        foreach (var (name, source) in originals)
        {
            var target = Path.Combine(root, locale, name);
            if (File.Exists(target) || Path.GetFileName(Path.GetDirectoryName(source)) == locale)
            {
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await File.WriteAllTextAsync(target, Skeleton(await File.ReadAllLinesAsync(source, ct), locale), new UTF8Encoding(false), ct);
            written.Add(target);
        }

        return written;
    }

    /// <summary>
    /// Records the fingerprints of the questions of every rule set version not yet in <c>rules/question-set-hashes.json</c>.
    /// A recorded version whose questions changed is an error: it needs a new version.
    /// </summary>
    public static async Task<(List<string> Added, List<string> Errors)> UpdateQuestionSetHashesAsync(RulesOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var errors = new List<string>();
        var sets = new List<RuleSet>();
        foreach (var file in Directory.GetFiles(options.Directory, "*.yaml").Order(StringComparer.Ordinal))
        {
            if (await YamlRuleSetProvider.LoadRuleSetAsync(file, errors, ct) is { } set)
            {
                sets.Add(set);
            }
        }

        if (errors.Count > 0)
        {
            return ([], errors);
        }

        return await QuestionSetHashes.UpdateAsync(sets, options.ResolvedQuestionSetHashesFile, ct);
    }

    /// <summary>Lines of a rule file without the text fields of its rules (four-space indented keys and their nested lines).</summary>
    internal static List<string> RemoveTextLines(IReadOnlyList<string> lines)
    {
        var kept = new List<string>();
        var skipping = false;
        foreach (var line in lines)
        {
            // The entries of explanation_by_jurisdiction are indented deeper than the key.
            if (skipping && line.StartsWith("      ", StringComparison.Ordinal))
            {
                continue;
            }

            skipping = false;
            var field = TextField().Match(line);
            if (field.Success && TextFields.Contains(field.Groups[1].Value))
            {
                skipping = field.Groups[1].Value == "explanation_by_jurisdiction";
                continue;
            }

            kept.Add(line);
        }

        return kept;
    }

    private static async Task<RuleSet?> ParseAsync(IReadOnlyList<string> lines, string name, List<string> errors, CancellationToken ct)
    {
        var temp = Path.Combine(Path.GetTempPath(), $"eshopguard-{Guid.NewGuid():N}-{name}");
        try
        {
            await File.WriteAllLinesAsync(temp, lines, ct);
            var set = await YamlRuleSetProvider.LoadRuleSetAsync(temp, errors, ct);
            if (set is not null)
            {
                set.SourceFile = name;
            }

            return set;
        }
        finally
        {
            File.Delete(temp);
        }
    }

    private static RuleSet WithoutTexts(RuleSet set)
    {
        foreach (var rule in set.Rules)
        {
            rule.Title = null;
            rule.Explanation = null;
            rule.ExplanationByJurisdiction = null;
            rule.Recommendation = null;
        }

        return set;
    }

    private static string Definition(RuleSet set) => JevCacheKeys.Canonical(new { set.Version, set.Module, set.AppliesTo, set.Jurisdictions, set.Enabled, set.PresenceThreshold, set.Questions, set.Rules });

    private static string SetTexts(RuleSet set, string locale, string today)
    {
        var text = new StringBuilder();
        text.Append($"# Texty pravidel sady {set.Name} ({locale}). Otázky pro Jev, logika a odkazy na zákon jsou v rules/{set.SourceFile}.\n");
        text.Append("# Klíče a zástupné symboly musí být ve všech jazycích stejné; kontrola: eshopguard rules check-texts.\n");
        text.Append($"locale: {locale}\n");
        text.Append($"rule_set: {set.Name}\n");
        text.Append($"source_version: {Quote(set.Version)}\n");
        text.Append("review:\n");
        text.Append("  original: true\n");
        text.Append($"  note: {Quote($"Původní texty pravidel, přesunuté beze změny z rules/{set.SourceFile} ({today}).")}\n");
        text.Append("rules:\n");
        foreach (var rule in set.Rules)
        {
            text.Append($"  {rule.Id}:\n");
            text.Append($"    title: {Quote(rule.Title ?? "")}\n");
            text.Append($"    explanation: {Quote(rule.Explanation ?? "")}\n");
            if (rule.ExplanationByJurisdiction is { Count: > 0 } byJurisdiction)
            {
                text.Append("    explanation_by_jurisdiction:\n");
                foreach (var (jurisdiction, explanation) in byJurisdiction)
                {
                    text.Append($"      {jurisdiction}: {Quote(explanation)}\n");
                }
            }

            text.Append($"    recommendation: {Quote(rule.Recommendation ?? "")}\n");
        }

        return text.ToString();
    }

    /// <summary>
    /// <c>label_notes</c> of the labels file with <c>note</c>: each gets an <c>id</c> (from its first name) and its remark moves
    /// to <c>rules/texts/&lt;locale&gt;/_labels.yaml</c>.
    /// </summary>
    private static async Task<List<string>> ExtractLabelNotesAsync(RulesOptions options, string locale, string today, List<string> errors, CancellationToken ct)
    {
        var file = options.LabelsFile;
        var target = Path.Combine(options.ResolvedTextsDirectory, locale, YamlRuleTextProvider.LabelsFile);
        if (!File.Exists(file) || File.Exists(target))
        {
            return [];
        }

        var lines = await File.ReadAllLinesAsync(file, ct);
        if (!lines.Any(l => LabelNote().IsMatch(l)))
        {
            return [];
        }

        var kept = new List<string>();
        var notes = new List<(string Id, string Note)>();
        for (var i = 0; i < lines.Length; i++)
        {
            var names = LabelNames().Match(lines[i]);
            if (names.Success && i + 1 < lines.Length && LabelNote().Match(lines[i + 1]) is { Success: true } note)
            {
                var first = FirstName().Match(names.Groups[1].Value).Groups[1].Value;
                var id = TextTools.Slugify(first).Replace('-', '_');
                if (id.Length == 0 || notes.Any(n => n.Id == id))
                {
                    errors.Add($"{Path.GetFileName(file)}, řádek {i + 1}: z názvu „{first}“ nejde udělat jedinečné id poznámky.");
                    return [];
                }

                kept.Add($"  - id: {id}");
                kept.Add($"    names: {names.Groups[1].Value}");
                notes.Add((id, Unquote(note.Groups[1].Value)));
                i++;
                continue;
            }

            kept.Add(lines[i]);
        }

        var text = new StringBuilder();
        text.Append($"# Poznámky ke značkám ({locale}), podle id z label_notes v {Path.GetFileName(file)}.\n");
        text.Append($"locale: {locale}\n");
        text.Append("review:\n");
        text.Append("  original: true\n");
        text.Append($"  note: {Quote($"Původní poznámky, přesunuté beze změny z {Path.GetFileName(file)} ({today}).")}\n");
        text.Append("notes:\n");
        foreach (var (id, note) in notes)
        {
            text.Append($"  {id}: {Quote(note)}\n");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        await File.WriteAllTextAsync(target, text.ToString(), new UTF8Encoding(false), ct);
        await File.WriteAllLinesAsync(file, kept, new UTF8Encoding(false), ct);
        return [target, file];
    }

    /// <summary>The same file with every text emptied, <c>locale</c> set and the review waiting for a translator.</summary>
    private static string Skeleton(IReadOnlyList<string> lines, string locale)
    {
        var text = new StringBuilder();
        text.Append($"# Kostra překladu do jazyka {locale}: vyplňte texty a review; zástupné symboly {{…}} musí zůstat stejné.\n");
        var inReview = false;
        foreach (var line in lines)
        {
            if (line.StartsWith('#'))
            {
                continue;
            }

            if (line.StartsWith("locale:", StringComparison.Ordinal))
            {
                text.Append($"locale: {locale}\n");
                continue;
            }

            if (line.StartsWith("culture:", StringComparison.Ordinal))
            {
                // Numbers and dates follow the language until the translator sets its culture (e.g. de-DE).
                text.Append("culture: \"\"\n");
                continue;
            }

            if (line.StartsWith("review:", StringComparison.Ordinal))
            {
                inReview = true;
                text.Append("review:\n  translated_by: \"\"\n  reviewed_by: \"\"\n  reviewed_at: \"\"\n");
                continue;
            }

            if (inReview && line.StartsWith("  ", StringComparison.Ordinal))
            {
                continue;
            }

            inReview = false;
            var value = Value().Match(line);
            text.Append(value.Success && !line.TrimStart().StartsWith("source_version", StringComparison.Ordinal) && !line.TrimStart().StartsWith("rule_set", StringComparison.Ordinal)
                ? $"{value.Groups[1].Value}\"\"\n"
                : line + "\n");
        }

        return text.ToString();
    }

    /// <summary>A YAML double-quoted string.</summary>
    internal static string Quote(string value) =>
        "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal) + "\"";

    private static string Unquote(string value) =>
        value.StartsWith('"') && value.EndsWith('"') && value.Length >= 2
            ? value[1..^1].Replace("\\\"", "\"", StringComparison.Ordinal).Replace("\\\\", "\\", StringComparison.Ordinal)
            : value;

    [GeneratedRegex(@"^    ([a-z_]+):")]
    private static partial Regex TextField();

    [GeneratedRegex(@"^  - names: (\[.*\])\s*$")]
    private static partial Regex LabelNames();

    [GeneratedRegex(@"^    note: (.*)$")]
    private static partial Regex LabelNote();

    [GeneratedRegex("^\\[\\s*\"([^\"]+)\"")]
    private static partial Regex FirstName();

    [GeneratedRegex(@"^\s*original:\s*true\s*$", RegexOptions.Multiline)]
    private static partial Regex OriginalMarker();

    /// <summary>A key with a quoted text value, e.g. <c>    title: "…"</c>.</summary>
    [GeneratedRegex("^(\\s*[a-z_]+:\\s)\".*\"\\s*$")]
    private static partial Regex Value();
}
