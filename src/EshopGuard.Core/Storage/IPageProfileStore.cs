using EshopGuard.Core.Profiles;

namespace EshopGuard.Core.Storage;

/// <summary>
/// Stored profiles of page templates. Profiles are kept, not written again in every scan: the model does not return the
/// same profile twice, and monitoring must report changes of the shop, not of the profile.
/// </summary>
public interface IPageProfileStore
{
    /// <summary>All profiles of the site, oldest first.</summary>
    Task<IReadOnlyList<PageProfile>> GetAsync(string site, CancellationToken ct = default);

    /// <summary>Stores a new profile.</summary>
    Task AddAsync(PageProfile profile, CancellationToken ct = default);
}
