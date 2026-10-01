using System.Net;
using EshopGuard.Tests.Shared;

namespace EshopGuard.Storage.Tests;

/// <summary>
/// The same contract against S3 (bucket <c>eshopguard-test</c>, settings <c>Storage:S3:*</c> from user-secrets
/// <c>eshopguard-tests</c>). Without them the tests fail with the missing key name.
/// </summary>
[Trait("Category", "S3")]
public sealed class S3BlobStoreTests : BlobStoreContractTests, IDisposable
{
    private readonly S3BlobStore _store = new(new S3StorageOptions
    {
        ServiceUrl = TestConfiguration.Current["Storage:S3:ServiceUrl"],
        Region = TestConfiguration.Current["Storage:S3:Region"] ?? "us-east-1",
        Bucket = TestConfiguration.Require("Storage:S3:Bucket"),
        ForcePathStyle = bool.TryParse(TestConfiguration.Current["Storage:S3:ForcePathStyle"], out var pathStyle) && pathStyle,
        AccessKey = TestConfiguration.Require("Storage:S3:AccessKey"),
        SecretKey = TestConfiguration.Require("Storage:S3:SecretKey"),
    }, maxSignedUrlMinutes: 15);

    protected override IBlobStore Store => _store;

    [Fact]
    public async Task AnonymousGet_IsForbidden()
    {
        var key = BlobKey.ForTenant(Guid.CreateVersion7(), "private.txt");
        await _store.PutAsync(key, new MemoryStream("secret"u8.ToArray()), "text/plain", Ct);
        try
        {
            var url = (await _store.GetReadUrlAsync(key, TimeSpan.FromMinutes(1), Ct))!;
            var anonymous = new UriBuilder(url) { Query = string.Empty }.Uri;
            using var http = new HttpClient();
            using var response = await http.GetAsync(anonymous, Ct);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        finally
        {
            await _store.DeleteAsync(key, Ct);
        }
    }

    [Fact]
    public async Task SignedUrl_WorksUntilItExpires()
    {
        var key = BlobKey.ForTenant(Guid.CreateVersion7(), "signed.txt");
        await _store.PutAsync(key, new MemoryStream("hello"u8.ToArray()), "text/plain", Ct);
        try
        {
            var url = (await _store.GetReadUrlAsync(key, TimeSpan.FromSeconds(3), Ct))!;
            using var http = new HttpClient();
            Assert.Equal("hello", await http.GetStringAsync(url, Ct));
            await Task.Delay(TimeSpan.FromSeconds(5), Ct);
            using var expired = await http.GetAsync(url, Ct);
            Assert.Equal(HttpStatusCode.Forbidden, expired.StatusCode);
        }
        finally
        {
            await _store.DeleteAsync(key, Ct);
        }
    }

    [Fact]
    public async Task SignedUrl_LongerThanLimit_IsRefused()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            _store.GetReadUrlAsync(BlobKey.ForTenant(Guid.CreateVersion7(), "a"), TimeSpan.FromMinutes(16), Ct));
    }

    [Fact]
    public async Task Probe_ReportsMissingBucket()
    {
        using var store = new S3BlobStore(new S3StorageOptions
        {
            ServiceUrl = TestConfiguration.Current["Storage:S3:ServiceUrl"],
            Region = TestConfiguration.Current["Storage:S3:Region"] ?? "us-east-1",
            Bucket = "eshopguard-missing-" + Guid.NewGuid().ToString("N")[..8],
            ForcePathStyle = true,
            AccessKey = TestConfiguration.Require("Storage:S3:AccessKey"),
            SecretKey = TestConfiguration.Require("Storage:S3:SecretKey"),
        }, 15);
        Assert.Equal(StorageErrorCodes.BucketMissing, await store.ProbeAsync(Ct));
    }

    public void Dispose() => _store.Dispose();
}
