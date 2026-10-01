using System.Text.Encodings.Web;
using System.Text.Json;
using EshopGuard.Core.Storage;

namespace EshopGuard.Core.Rules;

/// <summary>
/// Guards the version of a rule set: <c>rules/question-set-hashes.json</c> keeps the fingerprint of the questions of every
/// version (module → version → SHA-256 of the canonical JSON of the questions). Questions changed without a new version would
/// give two definitions of one version (in the cache keys, in <c>checks.rule_sets</c>), so the rules do not load. Texts are
/// outside the fingerprint: fixing a text never changes the version.
/// </summary>
internal static class QuestionSetHashes
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>Fingerprint of the questions of the set (type, English and local wording, options, levels).</summary>
    public static string Of(RuleSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        return TextTools.Sha256(JevCacheKeys.Canonical(set.Questions.ToDictionary(
            q => q.Key,
            q => new { type = q.Value.Type, text_en = q.Value.TextEn, text_cs = q.Value.TextCs, options = q.Value.Options, levels = q.Value.Levels })));
    }

    /// <summary>Errors for sets whose questions differ from the fingerprint of their version, or whose version is not recorded.</summary>
    public static async Task<List<string>> CheckAsync(IReadOnlyList<RuleSet> sets, string file, CancellationToken ct)
    {
        var errors = new List<string>();
        if (!File.Exists(file))
        {
            // Without the file (rule folders made by tests) there is nothing to compare with.
            return errors;
        }

        var recorded = await ReadAsync(file, errors, ct);
        foreach (var set in sets)
        {
            var hash = Of(set);
            if (!recorded.TryGetValue(set.Module, out var versions) || !versions.TryGetValue(set.Version, out var expected))
            {
                errors.Add($"{set.SourceFile}: verze {set.Version} nemá otisk otázek v {Path.GetFileName(file)}; po kontrole ho zapište příkazem eshopguard rules check-texts --update-hashes.");
            }
            else if (expected != hash)
            {
                errors.Add($"{set.SourceFile}: otázky se změnily, ale version {set.Version} zůstala stejná; změna otázek potřebuje novou version (a záznam v rules/CHANGELOG.md).");
            }
        }

        return errors;
    }

    /// <summary>
    /// Records the fingerprints of the versions that are not in the file yet. A recorded version with other questions is not
    /// overwritten; the set needs a new version instead.
    /// </summary>
    public static async Task<(List<string> Added, List<string> Errors)> UpdateAsync(IReadOnlyList<RuleSet> sets, string file, CancellationToken ct)
    {
        var errors = new List<string>();
        var recorded = File.Exists(file) ? await ReadAsync(file, errors, ct) : [];
        var added = new List<string>();
        foreach (var set in sets)
        {
            var hash = Of(set);
            if (!recorded.TryGetValue(set.Module, out var versions))
            {
                versions = new SortedDictionary<string, string>(StringComparer.Ordinal);
                recorded[set.Module] = versions;
            }

            if (!versions.TryGetValue(set.Version, out var expected))
            {
                versions[set.Version] = hash;
                added.Add($"{set.Module} {set.Version}");
            }
            else if (expected != hash)
            {
                errors.Add($"{set.SourceFile}: otázky verze {set.Version} se liší od zapsaného otisku; zvyšte version, otisk se nepřepisuje.");
            }
        }

        if (errors.Count == 0 && added.Count > 0)
        {
            await File.WriteAllTextAsync(file, JsonSerializer.Serialize(recorded, Json) + "\n", new System.Text.UTF8Encoding(false), ct);
        }

        return (added, errors);
    }

    private static async Task<SortedDictionary<string, SortedDictionary<string, string>>> ReadAsync(string file, List<string> errors, CancellationToken ct)
    {
        try
        {
            var raw = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(await File.ReadAllTextAsync(file, ct)) ?? [];
            return new SortedDictionary<string, SortedDictionary<string, string>>(
                raw.ToDictionary(p => p.Key, p => new SortedDictionary<string, string>(p.Value, StringComparer.Ordinal)), StringComparer.Ordinal);
        }
        catch (JsonException ex)
        {
            errors.Add($"{Path.GetFileName(file)} není platný JSON: {ex.Message}");
            return new SortedDictionary<string, SortedDictionary<string, string>>(StringComparer.Ordinal);
        }
    }
}
