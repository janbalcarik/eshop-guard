using System.Collections.Concurrent;
using EshopGuard.Core.Jev;

namespace EshopGuard.Core.Storage;

/// <summary>Jev answers in memory (tests, the behaviour of one batch).</summary>
internal sealed class InMemoryJevCache : IJevCache
{
    private readonly ConcurrentDictionary<string, JevResult> _answers = new(StringComparer.Ordinal);

    public int Count => _answers.Count;

    public Task<JevResult?> GetAsync(JevCacheKey key, CancellationToken ct = default) => Task.FromResult(_answers.GetValueOrDefault(key.LegacyKey));

    public Task SetAsync(JevCacheKey key, JevResult result, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        _answers[key.LegacyKey] = result;
        return Task.CompletedTask;
    }
}
