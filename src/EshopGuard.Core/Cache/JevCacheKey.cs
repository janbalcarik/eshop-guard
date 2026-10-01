using System.Text.Json;
using System.Text.Json.Nodes;
using EshopGuard.Core.Jev;

namespace EshopGuard.Core.Cache;

/// <summary>
/// Cache key: SHA-256 of the configured model, the question set version, the question language,
/// the canonical JSON of the questions and the canonical JSON of the state (sentence and context).
/// The configured model is used, not the returned one, because OpenRouter returns a snapshot id.
/// </summary>
internal static class JevCacheKey
{
    public static string Create(
        string model, string questionSetVersion, string questionLanguage,
        IReadOnlyDictionary<string, JevQuestion> questions, object state) =>
        TextTools.Sha256(string.Join('\u001F', model, questionSetVersion, questionLanguage, Canonical(questions), Canonical(state)));

    /// <summary>JSON with object properties sorted by name, so the key does not depend on property order.</summary>
    public static string Canonical(object value) =>
        Sort(JsonSerializer.SerializeToNode(value, JevClient.Json))?.ToJsonString() ?? "null";

    private static JsonNode? Sort(JsonNode? node) => node switch
    {
        JsonObject obj => new JsonObject(obj
            .OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => KeyValuePair.Create(p.Key, Sort(p.Value)))),
        JsonArray array => new JsonArray(array.Select(Sort).ToArray()),
        null => null,
        _ => node.DeepClone(),
    };
}
