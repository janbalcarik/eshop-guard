namespace EshopGuard.Storage;

/// <summary>
/// Files outside the database (HTML snapshots, extractions, PDFs). The only implementation so far is
/// <see cref="FileSystemBlobStore"/> (a local folder, in production on a mounted volume); cloud stores (Azure, AWS…)
/// are added later as further implementations selected by <c>Storage:Provider</c>.
/// </summary>
public interface IBlobStore
{
    /// <summary>Writes the file; an existing file under the key is replaced. The stream is not closed.</summary>
    Task PutAsync(BlobKey key, Stream content, string contentType, CancellationToken ct = default);

    /// <summary>Opens the file for reading, or returns <c>null</c> when it does not exist (never an empty stream).</summary>
    Task<Stream?> OpenReadAsync(BlobKey key, CancellationToken ct = default);

    /// <summary>True when the file exists.</summary>
    Task<bool> ExistsAsync(BlobKey key, CancellationToken ct = default);

    /// <summary>Deletes the file; a missing file is not an error.</summary>
    Task DeleteAsync(BlobKey key, CancellationToken ct = default);

    /// <summary>Deletes every file under the prefix (deleting a tenant or an e-shop) and returns how many were deleted.</summary>
    Task<int> DeletePrefixAsync(BlobKey prefix, CancellationToken ct = default);

    /// <summary>Time-limited read link, or <c>null</c> when the store cannot sign links (file system: the API streams the file).</summary>
    /// <remarks>Cloud implementations should cap <paramref name="validFor"/> and keep their containers private.</remarks>
    Task<Uri?> GetReadUrlAsync(BlobKey key, TimeSpan validFor, CancellationToken ct = default);
}

/// <summary>Reachability check of a store for <c>/health</c>; returns an error code or <c>null</c>.</summary>
public interface IBlobStoreProbe
{
    /// <summary>Checks the store without touching tenant data.</summary>
    Task<string?> ProbeAsync(CancellationToken ct = default);
}
