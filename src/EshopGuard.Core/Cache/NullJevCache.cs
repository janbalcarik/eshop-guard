using EshopGuard.Core.Jev;
using EshopGuard.Core.Storage;

namespace EshopGuard.Core.Cache;

/// <summary>
/// No cache: used with <c>--no-cache</c> and with the mock client, whose answers must never reach the real cache.
/// </summary>
internal sealed class NullJevCache : IJevCache
{
    public Task<JevResult?> GetAsync(JevCacheKey key, CancellationToken ct = default) => Task.FromResult<JevResult?>(null);

    public Task SetAsync(JevCacheKey key, JevResult result, CancellationToken ct = default) => Task.CompletedTask;
}
