using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using EshopGuard.Data.Entities.Fixes;

namespace EshopGuard.Application.Fixes;

/// <summary>A variant of a proposal („S upresnením“, „Bez environmentálneho slova“, <c>answer_no</c> „Riadok odstrániť“).</summary>
public sealed record ProposalAlternative(string Key, string Text, RecheckStatus RecheckStatus);

/// <summary>A fact the merchant fills in („Obal: [materiál obalu]“); the value goes in exactly as written.</summary>
public sealed record ProposalPlaceholder(string Key, string? Value);

/// <summary>
/// The text of a proposal of a fix (change 11, AD 5): the own wording of the merchant when there is one, otherwise the chosen
/// variant, otherwise the proposal of the rewrite; the facts the merchant filled in replace their markers (<c>[doplňte: …]</c>
/// or <c>[…]</c>) literally. The variants are <c>fix_proposals.alternatives</c> (<c>[{ key, text, recheck_status }]</c>), the
/// facts <c>fix_proposals.placeholders</c> (the keys of change 8, or <c>[{ key, value }]</c> once filled).
/// </summary>
public static class ProposalText
{
    /// <summary>Key of the variant prepared for the answer „Nie“.</summary>
    public const string AnswerNo = "answer_no";

    public static IReadOnlyList<ProposalAlternative> Alternatives(JsonDocument? alternatives) =>
        alternatives?.RootElement is { ValueKind: JsonValueKind.Array } root
            ? root.EnumerateArray()
                .Where(a => a.ValueKind == JsonValueKind.Object && Text(a, "key") is { Length: > 0 } && Text(a, "text") is not null)
                .Select(a => new ProposalAlternative(Text(a, "key")!, Text(a, "text")!, Recheck(Text(a, "recheck_status"))))
                .ToList()
            : [];

    public static IReadOnlyList<ProposalPlaceholder> Placeholders(JsonDocument? placeholders) =>
        placeholders?.RootElement is { ValueKind: JsonValueKind.Array } root
            ? root.EnumerateArray().Select(p => p.ValueKind switch
                {
                    JsonValueKind.String => new ProposalPlaceholder(p.GetString()!, null),
                    JsonValueKind.Object when Text(p, "key") is { Length: > 0 } key => new ProposalPlaceholder(key, Text(p, "value")),
                    _ => null,
                })
                .OfType<ProposalPlaceholder>()
                .ToList()
            : [];

    public static string PlaceholdersJson(IEnumerable<ProposalPlaceholder> placeholders) =>
        new JsonArray(placeholders.Select(p => (JsonNode)new JsonObject { ["key"] = p.Key, ["value"] = p.Value }).ToArray()).ToJsonString();

    /// <summary>The text before the facts are filled in: own wording, chosen variant, or the proposal.</summary>
    public static string RawText(FixProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        if (proposal.EditedText is { } edited)
        {
            return edited;
        }

        if (proposal.SelectedAlternative is { } key && Alternatives(proposal.Alternatives).FirstOrDefault(a => a.Key == key) is { } alternative)
        {
            return alternative.Text;
        }

        return proposal.ProposedText;
    }

    /// <summary>The text with the filled facts.</summary>
    public static string Text(FixProposal proposal) => Fill(RawText(proposal), Placeholders(proposal?.Placeholders));

    /// <summary>
    /// The state of the recheck of the current text: of the own wording (the column), of the chosen variant (its own state),
    /// or of the proposal (the column).
    /// </summary>
    public static RecheckStatus Recheck(FixProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        if (proposal.EditedText is null && proposal.SelectedAlternative is { } key
            && Alternatives(proposal.Alternatives).FirstOrDefault(a => a.Key == key) is { } alternative)
        {
            return alternative.RecheckStatus;
        }

        return proposal.RecheckStatus;
    }

    /// <summary>Keys of the facts of the current text that are still empty.</summary>
    public static IReadOnlyList<string> MissingPlaceholders(FixProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        var raw = RawText(proposal);
        return Placeholders(proposal.Placeholders)
            .Where(p => string.IsNullOrWhiteSpace(p.Value) && Contains(raw, p.Key))
            .Select(p => p.Key)
            .ToList();
    }

    /// <summary>The facts written into the text, each exactly as the merchant wrote it.</summary>
    public static string Fill(string text, IEnumerable<ProposalPlaceholder> placeholders)
    {
        ArgumentNullException.ThrowIfNull(text);
        foreach (var placeholder in placeholders ?? [])
        {
            if (string.IsNullOrWhiteSpace(placeholder.Value))
            {
                continue;
            }

            text = text.Replace($"[doplňte: {placeholder.Key}]", placeholder.Value, StringComparison.Ordinal)
                .Replace($"[{placeholder.Key}]", placeholder.Value, StringComparison.Ordinal);
        }

        return text;
    }

    /// <summary>A short fingerprint of a text (the recheck of a text is kept only for that very text).</summary>
    public static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..32];

    private static bool Contains(string text, string key) =>
        text.Contains($"[doplňte: {key}]", StringComparison.Ordinal) || text.Contains($"[{key}]", StringComparison.Ordinal);

    private static RecheckStatus Recheck(string? value) => value switch
    {
        "ok" => RecheckStatus.Ok,
        "still_finding" => RecheckStatus.StillFinding,
        _ => RecheckStatus.Pending,
    };

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
