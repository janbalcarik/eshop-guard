using System.Collections.Concurrent;

namespace EshopGuard.Core.Storage;

/// <summary>A page of a site as last downloaded: final URL, type, validators for a conditional request and the text hash.</summary>
public sealed record PageRecord(
    string Site,
    string Url,
    string FinalUrl,
    string Type,
    string? ETag,
    DateTimeOffset? LastModified,
    string TextHash,
    DateTimeOffset FetchedAt);

/// <summary>
/// A version of a page: written only when its text changed (<see cref="TextHash"/>), with the 64-bit fingerprints of its
/// sentences (for finding a sentence across the site and comparing language versions) and the counts of the report.
/// </summary>
public sealed record PageVersionRecord(
    string Site,
    string Url,
    string TextHash,
    IReadOnlyList<long> SentenceFingerprints,
    int VisibleTextChars,
    int CheckedTextChars,
    string Extraction,
    string? ScriptApp,
    bool TextNotLoaded,
    DateTimeOffset FetchedAt);

/// <summary>Validators of the last download of a page.</summary>
public sealed record PageValidators(string? ETag, DateTimeOffset? LastModified);

/// <summary>
/// Pages of the scanned sites and their versions. Separate from the caches: deleting a cache never deletes the history
/// of a shop's pages.
/// </summary>
public interface IPageStore
{
    /// <summary>Validators of the given URLs that have been downloaded before (others are left out).</summary>
    Task<IReadOnlyDictionary<string, PageValidators>> GetValidatorsAsync(string site, IReadOnlyList<string> urls, CancellationToken ct = default);

    /// <summary>Inserts or replaces the page.</summary>
    Task UpsertPageAsync(PageRecord page, CancellationToken ct = default);

    /// <summary>Adds a version when the text differs from the last one; returns false when it is the same (nothing written).</summary>
    Task<bool> AddVersionAsync(PageVersionRecord version, CancellationToken ct = default);

    /// <summary>The current version of every page of the site.</summary>
    Task<IReadOnlyList<PageVersionRecord>> GetCurrentVersionsAsync(string site, CancellationToken ct = default);

    /// <summary>URLs whose current version contains a sentence with the fingerprint.</summary>
    Task<IReadOnlyList<string>> FindByFingerprintAsync(string site, long fingerprint, CancellationToken ct = default);
}

/// <summary>Pages in memory, for one CLI run and the tests.</summary>
internal sealed class InMemoryPageStore : IPageStore
{
    private readonly ConcurrentDictionary<(string Site, string Url), PageRecord> _pages = new();
    private readonly ConcurrentDictionary<(string Site, string Url), List<PageVersionRecord>> _versions = new();

    public Task<IReadOnlyDictionary<string, PageValidators>> GetValidatorsAsync(string site, IReadOnlyList<string> urls, CancellationToken ct = default)
    {
        var found = new Dictionary<string, PageValidators>(StringComparer.Ordinal);
        foreach (var url in urls.Distinct(StringComparer.Ordinal))
        {
            if (_pages.TryGetValue((site, url), out var page))
            {
                found[url] = new PageValidators(page.ETag, page.LastModified);
            }
        }

        return Task.FromResult<IReadOnlyDictionary<string, PageValidators>>(found);
    }

    public Task UpsertPageAsync(PageRecord page, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(page);
        _pages[(page.Site, page.Url)] = page;
        return Task.CompletedTask;
    }

    public Task<bool> AddVersionAsync(PageVersionRecord version, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(version);
        var list = _versions.GetOrAdd((version.Site, version.Url), _ => []);
        lock (list)
        {
            if (list.Count > 0 && list[^1].TextHash == version.TextHash)
            {
                return Task.FromResult(false);
            }

            list.Add(version);
            return Task.FromResult(true);
        }
    }

    public Task<IReadOnlyList<PageVersionRecord>> GetCurrentVersionsAsync(string site, CancellationToken ct = default)
    {
        var current = new List<PageVersionRecord>();
        foreach (var ((s, _), list) in _versions)
        {
            if (s == site)
            {
                lock (list)
                {
                    current.Add(list[^1]);
                }
            }
        }

        return Task.FromResult<IReadOnlyList<PageVersionRecord>>([.. current.OrderBy(v => v.Url, StringComparer.Ordinal)]);
    }

    public async Task<IReadOnlyList<string>> FindByFingerprintAsync(string site, long fingerprint, CancellationToken ct = default) =>
        [.. (await GetCurrentVersionsAsync(site, ct)).Where(v => v.SentenceFingerprints.Contains(fingerprint)).Select(v => v.Url)];
}
