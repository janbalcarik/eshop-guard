using EshopGuard.Core.Storage;

namespace EshopGuard.Core.Fix;

/// <summary>
/// No cache: with <c>--no-cache</c> and with the mock client.
/// </summary>
internal sealed class NullRewriteCache : IRewriteCache
{
    public Task<(string Json, string? Model)?> GetAsync(string key, CancellationToken ct = default) =>
        Task.FromResult<(string Json, string? Model)?>(null);

    public Task SetAsync(string key, string json, string? model, CancellationToken ct = default) => Task.CompletedTask;
}

/// <summary>Rewrites in memory (tests, one process).</summary>
internal sealed class InMemoryRewriteCache : IRewriteCache
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, (string Json, string? Model)> _answers = new(StringComparer.Ordinal);

    public Task<(string Json, string? Model)?> GetAsync(string key, CancellationToken ct = default) =>
        Task.FromResult(_answers.TryGetValue(key, out var answer) ? answer : ((string Json, string? Model)?)null);

    public Task SetAsync(string key, string json, string? model, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(json);
        _answers[key] = (json, model);
        return Task.CompletedTask;
    }
}
