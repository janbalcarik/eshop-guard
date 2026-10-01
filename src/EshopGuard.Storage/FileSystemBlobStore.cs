namespace EshopGuard.Storage;

/// <summary>
/// Files in a local folder. Writes go to a temporary file in the same folder and are moved into place,
/// so a reader never sees a half-written file. Every path is checked to stay under the root.
/// </summary>
public sealed class FileSystemBlobStore : IBlobStore, IBlobStoreProbe
{
    private readonly string _root;

    /// <summary>Creates the store over <paramref name="root"/> (created when missing).</summary>
    public FileSystemBlobStore(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        _root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        Directory.CreateDirectory(_root);
    }

    /// <inheritdoc />
    public async Task PutAsync(BlobKey key, Stream content, string contentType, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        var path = FilePath(key);
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        var temp = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
            {
                await content.CopyToAsync(file, ct).ConfigureAwait(false);
                await file.FlushAsync(ct).ConfigureAwait(false);
            }

            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }
        }
    }

    /// <inheritdoc />
    public Task<Stream?> OpenReadAsync(BlobKey key, CancellationToken ct = default)
    {
        var path = FilePath(key);
        if (!File.Exists(path))
        {
            return Task.FromResult<Stream?>(null);
        }

        try
        {
            Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 81920, FileOptions.Asynchronous);
            return Task.FromResult<Stream?>(stream);
        }
        catch (FileNotFoundException)
        {
            return Task.FromResult<Stream?>(null);
        }
        catch (DirectoryNotFoundException)
        {
            return Task.FromResult<Stream?>(null);
        }
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(BlobKey key, CancellationToken ct = default) => Task.FromResult(File.Exists(FilePath(key)));

    /// <inheritdoc />
    public Task DeleteAsync(BlobKey key, CancellationToken ct = default)
    {
        var path = FilePath(key);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<int> DeletePrefixAsync(BlobKey prefix, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        if (!prefix.IsPrefix)
        {
            throw new ArgumentException("A prefix key is required (BlobKey.Prefix).", nameof(prefix));
        }

        var directory = FullPath(prefix.Value.TrimEnd('/'));
        if (!Directory.Exists(directory))
        {
            return Task.FromResult(0);
        }

        var count = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Count(f => !Path.GetFileName(f).StartsWith('.'));
        Directory.Delete(directory, recursive: true);
        return Task.FromResult(count);
    }

    /// <inheritdoc />
    public Task<Uri?> GetReadUrlAsync(BlobKey key, TimeSpan validFor, CancellationToken ct = default) => Task.FromResult<Uri?>(null);

    /// <inheritdoc />
    public async Task<string?> ProbeAsync(CancellationToken ct = default)
    {
        var key = BlobKey.System("health", "probe");
        try
        {
            using var content = new MemoryStream([1]);
            await PutAsync(key, content, "application/octet-stream", ct).ConfigureAwait(false);
            await DeleteAsync(key, ct).ConfigureAwait(false);
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return StorageErrorCodes.Unreachable;
        }
    }

    private string FilePath(BlobKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.IsPrefix)
        {
            throw new ArgumentException("A file key is required, not a prefix.", nameof(key));
        }

        return FullPath(key.Value);
    }

    private string FullPath(string relative)
    {
        var path = Path.GetFullPath(Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ArgumentException("Blob key resolves outside the storage root.");
        }

        return path;
    }
}
