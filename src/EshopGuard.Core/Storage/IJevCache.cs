using EshopGuard.Core.Jev;

namespace EshopGuard.Core.Storage;

/// <summary>
/// Cache of Jev answers: the answers of the sentences and chunks a run asked, so that unchanged text is never paid or
/// counted against the request limit again. The CLI and the web application keep them in PostgreSQL by
/// <see cref="JevCacheKey.LegacyKey"/> per tenant (<c>EshopGuard.Data.Stores.PgJevCache</c>); without a host store the
/// library caches nothing.
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

    /// <summary>Stores an answer; when the key is already stored, the first answer stays (findings never change silently).</summary>
    Task SetAsync(JevCacheKey key, JevResult result, CancellationToken ct = default);
}
