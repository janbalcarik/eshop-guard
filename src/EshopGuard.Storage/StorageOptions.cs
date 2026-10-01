namespace EshopGuard.Storage;

/// <summary>Settings under <c>Storage</c>.</summary>
public sealed class StorageOptions
{
    /// <summary>Configuration section.</summary>
    public const string SectionName = "Storage";

    /// <summary>Value of <see cref="Provider"/> for the local file system (the only provider so far).</summary>
    public const string FileSystemProvider = "FileSystem";

    /// <summary>
    /// Which <see cref="IBlobStore"/> to use. Only <c>FileSystem</c> exists now; cloud stores (Azure, AWS…) are added as
    /// further implementations of <see cref="IBlobStore"/> with their own value. No default: a missing value fails at startup.
    /// </summary>
    public string? Provider { get; set; }

    /// <summary>File system store.</summary>
    public FileSystemStorageOptions FileSystem { get; set; } = new();
}

/// <summary>Settings under <c>Storage:FileSystem</c>.</summary>
public sealed class FileSystemStorageOptions
{
    /// <summary>
    /// Root folder (absolute, or relative to the working directory). No default, so files never end up in an unintended
    /// place (e.g. inside a container instead of on a mounted volume).
    /// </summary>
    public string? Root { get; set; }
}
