using System.Text.Json;
using System.Text.Json.Nodes;
using EshopGuard.Core.Jev;

namespace EshopGuard.Core.Storage;

/// <summary>Which answers a key belongs to: the detailed questions of a segment, or the sieve of a chunk.</summary>
public enum JevCacheKind
{
    /// <summary>Questions of rule sets for one sentence or legal paragraph.</summary>
    Detail,

    /// <summary>Topic questions of the sieve for one chunk of the main text.</summary>
    Sieve,
}

/// <summary>
/// Key of a cached Jev answer. <see cref="QuestionSetHash"/> = SHA-256 of the configured model, the question set
/// version, the question language and the canonical JSON of the questions; <see cref="StateHash"/> = SHA-256 of the
/// canonical JSON of the state (sentence with context, or text of a chunk); <see cref="LegacyKey"/> = SHA-256 of all of
/// them together, exactly the key the CLI cache used before change 5, so its stored answers stay valid. The configured
/// model is used, not the returned one, because OpenRouter returns a snapshot id.
/// </summary>
public readonly record struct JevCacheKey(JevCacheKind Kind, string QuestionSetHash, string StateHash, string LegacyKey);

/// <summary>Builds <see cref="JevCacheKey"/>.</summary>
internal static class JevCacheKeys
{
    private const char Separator = '\u001F';

    public static JevCacheKey Create(
        JevCacheKind kind, string model, string questionSetVersion, string questionLanguage,
        IReadOnlyDictionary<string, JevQuestion> questions, object state)
    {
        var questionsJson = Canonical(questions);
        var stateJson = Canonical(state);
        return new JevCacheKey(
            kind,
            TextTools.Sha256(string.Join(Separator, model, questionSetVersion, questionLanguage, questionsJson)),
            TextTools.Sha256(stateJson),
            TextTools.Sha256(string.Join(Separator, model, questionSetVersion, questionLanguage, questionsJson, stateJson)));
    }

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
