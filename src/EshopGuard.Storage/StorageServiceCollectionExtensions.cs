using EshopGuard.Storage.Health;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace EshopGuard.Storage;

/// <summary>Registration of the file store.</summary>
public static class StorageServiceCollectionExtensions
{
    /// <summary>
    /// Binds <c>Storage</c>, validates it at host start (unknown provider or missing key → <c>config.storage_*</c>)
    /// and registers <see cref="IBlobStore"/> and <see cref="StorageHealthCheck"/>.
    /// </summary>
    public static IServiceCollection AddEshopGuardStorage(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddOptions<StorageOptions>().BindConfiguration(StorageOptions.SectionName).ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<StorageOptions>, StorageOptionsValidator>());
        services.TryAddSingleton<IBlobStore>(sp => Create(sp.GetRequiredService<IOptions<StorageOptions>>().Value));
        services.TryAddTransient<StorageHealthCheck>();
        return services;
    }

    /// <summary>Creates the store for the options; throws <see cref="StorageConfigurationException"/> when they are invalid.</summary>
    public static IBlobStore Create(StorageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (StorageOptionsValidator.Check(options) is { } failure)
        {
            throw new StorageConfigurationException(failure.Code, failure.Key);
        }

        return options.Provider == StorageOptions.S3Provider
            ? new S3BlobStore(options.S3, options.MaxSignedUrlMinutes)
            : new FileSystemBlobStore(options.FileSystem.Root);
    }
}

/// <summary>Validation of <see cref="StorageOptions"/>; failures name the code and the key, never a value.</summary>
internal sealed class StorageOptionsValidator : IValidateOptions<StorageOptions>
{
    public ValidateOptionsResult Validate(string? name, StorageOptions options) =>
        Check(options) is { } failure ? ValidateOptionsResult.Fail($"{failure.Code}: {failure.Key}") : ValidateOptionsResult.Success;

    internal static (string Code, string Key)? Check(StorageOptions options)
    {
        switch (options.Provider)
        {
            case StorageOptions.FileSystemProvider:
                return string.IsNullOrWhiteSpace(options.FileSystem.Root) ? (StorageErrorCodes.KeyMissing, "Storage:FileSystem:Root") : null;
            case StorageOptions.S3Provider:
                if (string.IsNullOrWhiteSpace(options.S3.Bucket))
                {
                    return (StorageErrorCodes.KeyMissing, "Storage:S3:Bucket");
                }

                if (string.IsNullOrWhiteSpace(options.S3.AccessKey))
                {
                    return (StorageErrorCodes.KeyMissing, "Storage:S3:AccessKey");
                }

                if (string.IsNullOrWhiteSpace(options.S3.SecretKey))
                {
                    return (StorageErrorCodes.KeyMissing, "Storage:S3:SecretKey");
                }

                return options.MaxSignedUrlMinutes is < 1 or > 10080 ? (StorageErrorCodes.KeyMissing, "Storage:MaxSignedUrlMinutes") : null;
            default:
                return (StorageErrorCodes.ProviderInvalid, "Storage:Provider");
        }
    }
}
