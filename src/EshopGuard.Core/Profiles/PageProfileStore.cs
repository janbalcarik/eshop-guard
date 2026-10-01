using System.Collections.Concurrent;
using EshopGuard.Core.Storage;

namespace EshopGuard.Core.Profiles;

/// <summary>
/// Profiles in memory: the default of the library without a host store, the <c>--mock</c> runs of the CLI (which never
/// touch the database) and tests. A profile written here lives only as long as the process.
/// </summary>
internal sealed class InMemoryPageProfileStore : IPageProfileStore
{
    private readonly ConcurrentDictionary<string, List<PageProfile>> _profiles = new(StringComparer.Ordinal);

    public Task<IReadOnlyList<PageProfile>> GetAsync(string site, CancellationToken ct = default)
    {
        if (!_profiles.TryGetValue(site, out var list))
        {
            return Task.FromResult<IReadOnlyList<PageProfile>>([]);
        }

        lock (list)
        {
            return Task.FromResult<IReadOnlyList<PageProfile>>([.. list.OrderBy(p => p.CreatedAt)]);
        }
    }

    public Task AddAsync(PageProfile profile, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var list = _profiles.GetOrAdd(profile.Site, _ => []);
        lock (list)
        {
            if (!list.Any(p => p.Id == profile.Id))
            {
                list.Add(profile);
            }
        }

        return Task.CompletedTask;
    }
}
