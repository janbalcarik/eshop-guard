namespace EshopGuard.Storage.Tests;

public sealed class StorageRegistrationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Azure")]
    [InlineData("s3")]
    public void UnknownProvider_IsRefusedWithCode(string? provider)
    {
        var ex = Assert.Throws<StorageConfigurationException>(() => StorageServiceCollectionExtensions.Create(new StorageOptions { Provider = provider }));
        Assert.Equal(StorageErrorCodes.ProviderInvalid, ex.Code);
        Assert.Equal("Storage:Provider", ex.Key);
    }

    [Fact]
    public void S3WithoutSecretKey_IsRefusedWithKeyName()
    {
        var options = new StorageOptions { Provider = "S3", S3 = new S3StorageOptions { Bucket = "b", AccessKey = "a" } };
        var ex = Assert.Throws<StorageConfigurationException>(() => StorageServiceCollectionExtensions.Create(options));
        Assert.Equal(StorageErrorCodes.KeyMissing, ex.Code);
        Assert.Equal("Storage:S3:SecretKey", ex.Key);
    }
}
