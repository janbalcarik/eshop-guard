using EshopGuard.Core.Jev;

namespace EshopGuard.Core.Storage;

/// <summary>
/// Cache of Jev answers. The CLI keeps them in a local SQLite file (by <see cref="JevCacheKey.LegacyKey"/>), the web
/// application in PostgreSQL (by <see cref="JevCacheKey.QuestionSetHash"/> and <see cref="JevCacheKey.StateHash"/>).
/// </summary>
public interface IJevCache
{
    /// <summary>Returns the cached answer, or null.</summary>
    Task<JevResult?> GetAsync(JevCacheKey key, CancellationToken ct = default);

    /// <summary>Answers of the keys that are in the cache (missing keys are left out). By default one by one.</summary>
    async Task<IReadOnlyDictionary<JevCacheKey, JevResult>> GetManyAsync(IReadOnlyList<JevCacheKey> keys, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var found = new Dictionary<JevCacheKey, JevResult>();
        foreach (var key in keys.Distinct())
        {
            if (await GetAsync(key, ct) is { } result)
            {
                found[key] = result;
            }
        }

        return found;
    }

    /// <summary>Stores an answer; storing the same key again replaces it.</summary>
    Task SetAsync(JevCacheKey key, JevResult result, CancellationToken ct = default);
}
