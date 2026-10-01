using System.Text.Json;
using EshopGuard.Core.Jev;

namespace EshopGuard.Core.Storage;

/// <summary>JSON of a cached Jev answer, the same as the client reads it (snake_case, nulls left out).</summary>
public static class JevCacheJson
{
    /// <summary>The answer as JSON.</summary>
    public static string Serialize(JevResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return JsonSerializer.Serialize(result, JevClient.Json);
    }

    /// <summary>The answer from its JSON.</summary>
    /// <exception cref="JsonException">The JSON is not a Jev answer.</exception>
    public static JevResult Deserialize(string json) =>
        JsonSerializer.Deserialize<JevResult>(json, JevClient.Json) ?? throw new JsonException("Empty Jev answer.");
}
