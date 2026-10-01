using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EshopGuard.Storage.Tests;

public sealed class StorageRegistrationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("filesystem")]
    [InlineData("S3")]
    [InlineData("Azure")]
    public void UnknownProvider_IsRefusedWithCode(string? provider)
    {
        var ex = Assert.Throws<StorageConfigurationException>(() => StorageServiceCollectionExtensions.Create(new StorageOptions { Provider = provider }));
        Assert.Equal(StorageErrorCodes.ProviderInvalid, ex.Code);
        Assert.Equal("Storage:Provider", ex.Key);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public void FileSystemWithoutRoot_IsRefusedWithKeyName(string? root)
    {
        var options = new StorageOptions { Provider = "FileSystem", FileSystem = new FileSystemStorageOptions { Root = root } };
        var ex = Assert.Throws<StorageConfigurationException>(() => StorageServiceCollectionExtensions.Create(options));
        Assert.Equal(StorageErrorCodes.KeyMissing, ex.Code);
        Assert.Equal("Storage:FileSystem:Root", ex.Key);
    }

    [Fact]
    public void Registration_ReadsConfigurationAndCreatesFileSystemStore()
    {
        var root = Directory.CreateTempSubdirectory("eshopguard-reg-");
        try
        {
            using var provider = Build(new Dictionary<string, string?> { ["Storage:Provider"] = "FileSystem", ["Storage:FileSystem:Root"] = root.FullName });
            Assert.IsType<FileSystemBlobStore>(provider.GetRequiredService<IBlobStore>());
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public void Registration_ValidationFailure_NamesOnlyCodeAndKey()
    {
        using var provider = Build(new Dictionary<string, string?> { ["Storage:Provider"] = "FileSystem" });
        var ex = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<StorageOptions>>().Value);
        Assert.Equal(["config.storage_key_missing: Storage:FileSystem:Root"], ex.Failures);
    }

    private static ServiceProvider Build(Dictionary<string, string?> settings)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        services.AddEshopGuardStorage();
        return services.BuildServiceProvider();
    }
}
