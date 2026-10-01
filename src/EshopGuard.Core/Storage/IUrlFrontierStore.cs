using System.Collections.Concurrent;

namespace EshopGuard.Core.Storage;

/// <summary>
/// State of the URL frontier of a crawl (as JSON), saved after every batch so that another worker continues after a crash.
/// </summary>
public interface IUrlFrontierStore
{
    /// <summary>The saved state, or null.</summary>
    Task<string?> LoadAsync(string scopeKey, CancellationToken ct = default);

    /// <summary>Saves the state (replaces the earlier one).</summary>
    Task SaveAsync(string scopeKey, string stateJson, CancellationToken ct = default);
}

/// <summary>Frontier states in memory.</summary>
internal sealed class InMemoryUrlFrontierStore : IUrlFrontierStore
{
    private readonly ConcurrentDictionary<string, string> _states = new(StringComparer.Ordinal);

    public Task<string?> LoadAsync(string scopeKey, CancellationToken ct = default) => Task.FromResult(_states.GetValueOrDefault(scopeKey));

    public Task SaveAsync(string scopeKey, string stateJson, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stateJson);
        _states[scopeKey] = stateJson;
        return Task.CompletedTask;
    }
}
