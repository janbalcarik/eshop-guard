namespace EshopGuard.Storage;

/// <summary>Settings under <c>Storage</c>.</summary>
public sealed class StorageOptions
{
    /// <summary>Configuration section.</summary>
    public const string SectionName = "Storage";

    /// <summary>Value of <see cref="Provider"/> for S3 or MinIO.</summary>
    public const string S3Provider = "S3";

    /// <summary>Value of <see cref="Provider"/> for the local file system.</summary>
    public const string FileSystemProvider = "FileSystem";

    /// <summary><c>S3</c> or <c>FileSystem</c>; no default, a missing value fails at startup.</summary>
    public string? Provider { get; set; }

    /// <summary>File system store.</summary>
    public FileSystemStorageOptions FileSystem { get; set; } = new();

    /// <summary>S3 store.</summary>
    public S3StorageOptions S3 { get; set; } = new();

    /// <summary>Longest validity of a signed read link.</summary>
    public int MaxSignedUrlMinutes { get; set; } = 15;
}

/// <summary>Settings under <c>Storage:FileSystem</c>.</summary>
public sealed class FileSystemStorageOptions
{
    /// <summary>Root folder (relative to the working directory or absolute).</summary>
    public string Root { get; set; } = ".data/blobs";
}

/// <summary>Settings under <c>Storage:S3</c>; the keys come only from user-secrets or environment variables.</summary>
public sealed class S3StorageOptions
{
    /// <summary>Endpoint, e.g. <c>http://localhost:9000</c> for MinIO; empty for AWS (region endpoint).</summary>
    public string? ServiceUrl { get; set; }

    /// <summary>Signing region.</summary>
    public string Region { get; set; } = "us-east-1";

    /// <summary>Private bucket.</summary>
    public string? Bucket { get; set; }

    /// <summary>Path-style addressing (MinIO).</summary>
    public bool ForcePathStyle { get; set; }

    /// <summary>Access key id.</summary>
    public string? AccessKey { get; set; }

    /// <summary>Secret access key.</summary>
    public string? SecretKey { get; set; }
}
