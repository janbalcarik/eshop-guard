using System.Text.Json;
using System.Text.Json.Nodes;
using EshopGuard.Data.Entities.Fixes;

namespace EshopGuard.Application.Fixes;

/// <summary>
/// The facts of a group (change 11, AD 7): its keys are <c>fix_groups.placeholders</c> (an array of keys, or of
/// <c>{ key }</c>), the values the merchant filled in are <c>fix_groups.filled_values</c> (<c>{ key: value }</c>); the
/// replacement is the template with the values written in literally.
/// </summary>
public static class GroupValues
{
    public static IReadOnlyList<string> Keys(FixGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        return group.Placeholders?.RootElement is { ValueKind: JsonValueKind.Array } root
            ? root.EnumerateArray().Select(p => p.ValueKind switch
                {
                    JsonValueKind.String => p.GetString(),
                    JsonValueKind.Object when p.TryGetProperty("key", out var key) && key.ValueKind == JsonValueKind.String => key.GetString(),
                    _ => null,
                })
                .OfType<string>().Where(k => k.Length > 0).Distinct(StringComparer.Ordinal).ToList()
            : [];
    }

    public static IReadOnlyDictionary<string, string> Values(FixGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        return group.FilledValues?.RootElement is { ValueKind: JsonValueKind.Object } root
            ? root.EnumerateObject().Where(p => p.Value.ValueKind == JsonValueKind.String)
                .ToDictionary(p => p.Name, p => p.Value.GetString()!, StringComparer.Ordinal)
            : new Dictionary<string, string>(StringComparer.Ordinal);
    }

    /// <summary>Keys still without a value (only the mode <c>replace</c> needs them).</summary>
    public static IReadOnlyList<string> Missing(FixGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        if (group.Mode != FixGroupMode.Replace)
        {
            return [];
        }

        var values = Values(group);
        return Keys(group).Where(k => !values.TryGetValue(k, out var v) || string.IsNullOrWhiteSpace(v)).ToList();
    }

    /// <summary>The wording that replaces the sentence: the filled template, nothing (<c>remove</c>) or the own wording.</summary>
    public static string? Replacement(FixGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        return group.Mode switch
        {
            FixGroupMode.Remove => "",
            FixGroupMode.Custom => group.CustomText,
            _ => group.ReplacementTemplate is null
                ? null
                : ProposalText.Fill(group.ReplacementTemplate, Values(group).Select(v => new ProposalPlaceholder(v.Key, v.Value))),
        };
    }

    public static string ValuesJson(IReadOnlyDictionary<string, string> values) =>
        new JsonObject(values.Select(v => KeyValuePair.Create(v.Key, (JsonNode?)JsonValue.Create(v.Value)))).ToJsonString();
}
