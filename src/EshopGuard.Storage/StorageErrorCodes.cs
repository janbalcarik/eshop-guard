namespace EshopGuard.Storage;

/// <summary>Codes of storage configuration and health failures (never exception texts).</summary>
public static class StorageErrorCodes
{
    /// <summary><c>Storage:Provider</c> is missing or unknown.</summary>
    public const string ProviderInvalid = "config.storage_provider_invalid";

    /// <summary>A required storage setting (bucket, access key, secret key, root) is missing.</summary>
    public const string KeyMissing = "config.storage_key_missing";

    /// <summary>The store cannot be reached or written.</summary>
    public const string Unreachable = "storage.unreachable";

    /// <summary>The configured bucket does not exist.</summary>
    public const string BucketMissing = "storage.bucket_missing";

    /// <summary>The store rejected the credentials or the operation.</summary>
    public const string AccessDenied = "storage.access_denied";
}

/// <summary>Invalid storage configuration; the message names only the code and the key.</summary>
public sealed class StorageConfigurationException(string code, string key) : Exception($"{code}: {key}")
{
    /// <summary>Error code.</summary>
    public string Code { get; } = code;

    /// <summary>Configuration key without its value.</summary>
    public string Key { get; } = key;
}
