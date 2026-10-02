using System.Text.Json.Nodes;
using EshopGuard.Core;
using EshopGuard.Core.Fix;
using EshopGuard.Core.Models;
using EshopGuard.Core.Options;
using EshopGuard.Data.Entities.Fixes;

namespace EshopGuard.Jobs.Fixes;

/// <summary>What the recheck of a text found: in total and by jurisdiction, with the rules that still find something.</summary>
public sealed record RecheckOutcome(RecheckStatus Status, IReadOnlyDictionary<string, string> Jurisdictions, IReadOnlyList<string> RuleIds)
{
    /// <summary><c>fix_proposals.recheck_result</c>: <c>{ text_hash, checked_at, jurisdictions: { sk: "ok" }, rule_ids }</c>.</summary>
    public string ToJson(string textHash, DateTimeOffset checkedAt) => new JsonObject
    {
        ["text_hash"] = textHash,
        ["checked_at"] = checkedAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
        ["jurisdictions"] = new JsonObject(Jurisdictions.Select(j => KeyValuePair.Create(j.Key, (JsonNode?)JsonValue.Create(j.Value)))),
        ["rule_ids"] = new JsonArray(RuleIds.Select(r => (JsonNode?)JsonValue.Create(r)).ToArray()),
    }.ToJsonString();
}

/// <summary>
/// The recheck of a new text by the rules, Jev included, as the rewrite of change 8 does it (<c>PageRewriter</c>): the text
/// with the block before and after it is evaluated for the jurisdictions of the active markets and the modules of the
/// findings; a finding of the new text whose valid verdict is decided by the text (<c>text</c>, <c>assess</c>) keeps it
/// open in that jurisdiction. A sentence that still waits for a fact (<c>[doplňte: …]</c>) does not count. An empty text
/// (the sentence removed) needs no recheck.
/// </summary>
public static class TextRecheck
{
    public static async Task<RecheckOutcome> CheckAsync(
        IEshopGuard guard, string text, string? before, string? after, IReadOnlyList<string> jurisdictions, IReadOnlyList<string> modules, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(guard);
        ArgumentNullException.ThrowIfNull(text);
        if (text.Trim().Length == 0)
        {
            return new RecheckOutcome(RecheckStatus.Ok, jurisdictions.ToDictionary(j => j, _ => "ok", StringComparer.Ordinal), []);
        }

        var window = string.Join("\n", new[] { before, text, after }.Where(t => !string.IsNullOrWhiteSpace(t)));
        var analysis = await guard.AnalyzeTextsAsync(
            [new TextInput { Text = window, Url = "recheck:text", Kind = TextKind.Sentence }],
            new AnalyzeOptions { Modules = modules, Jurisdictions = jurisdictions, Country = jurisdictions.FirstOrDefault() ?? "sk" },
            ct).ConfigureAwait(false);
        var normalized = PageText.Normalize(text);
        var remaining = analysis.Findings
            .Where(f => f.Scope == "segment" && f.Text is not null
                && normalized.Contains(PageText.Normalize(f.Text), StringComparison.Ordinal)
                && !f.Text.Contains(RewritePrompt.PlaceholderMarker, StringComparison.Ordinal))
            .ToList();
        var byJurisdiction = jurisdictions.ToDictionary(
            j => j,
            j => remaining.Any(f => f.Verdicts.Any(v => v.Jurisdiction == j && v.Status == VerdictStatus.Finding && v.Checkability is "text" or "assess"))
                ? "still_finding"
                : "ok",
            StringComparer.Ordinal);
        var rules = remaining.Where(f => f.Verdicts.Any(v => v.Status == VerdictStatus.Finding && v.Checkability is "text" or "assess"))
            .Select(f => f.RuleId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        return new RecheckOutcome(byJurisdiction.Values.Any(v => v == "still_finding") ? RecheckStatus.StillFinding : RecheckStatus.Ok, byJurisdiction, rules);
    }
}
