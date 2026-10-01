using EshopGuard.Core.Jev;

namespace EshopGuard.Core.Cache;

/// <summary>
/// Cache of Jev responses. The default is a local SQLite file; a product may use Redis or its database.
/// </summary>
public interface IJevCache
{
    /// <summary>Returns the cached response, or null.</summary>
    Task<JevResult?> GetAsync(string key, CancellationToken ct = default);

    /// <summary>Stores a response.</summary>
    Task SetAsync(string key, JevResult result, CancellationToken ct = default);
}

/// <summary>
/// No cache: used with <c>--no-cache</c> and with the mock client, whose answers must never reach the real cache.
/// </summary>
internal sealed class NullJevCache : IJevCache
{
    public Task<JevResult?> GetAsync(string key, CancellationToken ct = default) => Task.FromResult<JevResult?>(null);

    public Task SetAsync(string key, JevResult result, CancellationToken ct = default) => Task.CompletedTask;
}
