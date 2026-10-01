using System.Security.Cryptography;

namespace EshopGuard.Storage.Tests;

/// <summary>Behaviour every <see cref="IBlobStore"/> must have; each test uses fresh tenant ids.</summary>
public abstract class BlobStoreContractTests
{
    protected abstract IBlobStore Store { get; }

    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    protected static async Task<byte[]?> ReadAllAsync(IBlobStore store, BlobKey key)
    {
        await using var stream = await store.OpenReadAsync(key, Ct);
        if (stream is null)
        {
            return null;
        }

        using var copy = new MemoryStream();
        await stream.CopyToAsync(copy, Ct);
        return copy.ToArray();
    }

    [Fact]
    public async Task Put_ThenRead_ReturnsSameBytes()
    {
        var key = BlobKey.ForShop(Guid.CreateVersion7(), Guid.CreateVersion7(), "pages", "p1.html.gz");
        var bytes = RandomNumberGenerator.GetBytes(200_000);
        await Store.PutAsync(key, new MemoryStream(bytes), "application/gzip", Ct);
        Assert.Equal(bytes, await ReadAllAsync(Store, key));
        Assert.True(await Store.ExistsAsync(key, Ct));
        await Store.DeletePrefixAsync(BlobKey.Prefix(Guid.Parse(key.Segments[1])), Ct);
    }

    [Fact]
    public async Task Put_FromNonSeekableStream_Works()
    {
        var key = BlobKey.ForTenant(Guid.CreateVersion7(), "x.bin");
        var bytes = RandomNumberGenerator.GetBytes(10_000);
        await Store.PutAsync(key, new NonSeekableStream(bytes), "application/octet-stream", Ct);
        Assert.Equal(bytes, await ReadAllAsync(Store, key));
        await Store.DeleteAsync(key, Ct);
    }

    [Fact]
    public async Task Put_Twice_Overwrites()
    {
        var key = BlobKey.ForTenant(Guid.CreateVersion7(), "report.pdf");
        await Store.PutAsync(key, new MemoryStream([1, 2, 3]), "application/pdf", Ct);
        await Store.PutAsync(key, new MemoryStream([9]), "application/pdf", Ct);
        Assert.Equal([9], await ReadAllAsync(Store, key));
        await Store.DeleteAsync(key, Ct);
    }

    [Fact]
    public async Task Missing_ReadsAsNullAndDeletesWithoutError()
    {
        var key = BlobKey.ForTenant(Guid.CreateVersion7(), "missing.txt");
        Assert.Null(await Store.OpenReadAsync(key, Ct));
        Assert.False(await Store.ExistsAsync(key, Ct));
        await Store.DeleteAsync(key, Ct);
    }

    [Fact]
    public async Task Delete_RemovesFile()
    {
        var key = BlobKey.ForTenant(Guid.CreateVersion7(), "a.txt");
        await Store.PutAsync(key, new MemoryStream([1]), "text/plain", Ct);
        await Store.DeleteAsync(key, Ct);
        Assert.Null(await Store.OpenReadAsync(key, Ct));
    }

    [Fact]
    public async Task DeletePrefix_DeletesOnlyThatTenant()
    {
        var tenant = Guid.CreateVersion7();
        var other = Guid.CreateVersion7();
        var shop = Guid.CreateVersion7();
        BlobKey[] mine = [BlobKey.ForTenant(tenant, "a.txt"), BlobKey.ForShop(tenant, shop, "pages", "1.html"), BlobKey.ForShop(tenant, shop, "pages", "2.html")];
        var theirs = BlobKey.ForTenant(other, "a.txt");
        foreach (var key in mine.Append(theirs))
        {
            await Store.PutAsync(key, new MemoryStream([7]), "text/plain", Ct);
        }

        Assert.Equal(3, await Store.DeletePrefixAsync(BlobKey.Prefix(tenant), Ct));
        foreach (var key in mine)
        {
            Assert.False(await Store.ExistsAsync(key, Ct));
        }

        Assert.True(await Store.ExistsAsync(theirs, Ct));
        Assert.Equal(1, await Store.DeletePrefixAsync(BlobKey.Prefix(other), Ct));
        Assert.Equal(0, await Store.DeletePrefixAsync(BlobKey.Prefix(other), Ct));
    }

    [Fact]
    public async Task DeletePrefix_OfShop_KeepsOtherShops()
    {
        var tenant = Guid.CreateVersion7();
        var shop = Guid.CreateVersion7();
        var otherShop = Guid.CreateVersion7();
        await Store.PutAsync(BlobKey.ForShop(tenant, shop, "a"), new MemoryStream([1]), "text/plain", Ct);
        await Store.PutAsync(BlobKey.ForShop(tenant, otherShop, "a"), new MemoryStream([1]), "text/plain", Ct);
        Assert.Equal(1, await Store.DeletePrefixAsync(BlobKey.Prefix(tenant, shop), Ct));
        Assert.True(await Store.ExistsAsync(BlobKey.ForShop(tenant, otherShop, "a"), Ct));
        await Store.DeletePrefixAsync(BlobKey.Prefix(tenant), Ct);
    }

    [Fact]
    public async Task FileKeyAndPrefixKey_AreNotInterchangeable()
    {
        var tenant = Guid.CreateVersion7();
        await Assert.ThrowsAsync<ArgumentException>(() => Store.DeletePrefixAsync(BlobKey.ForTenant(tenant, "a"), Ct));
        await Assert.ThrowsAsync<ArgumentException>(() => Store.PutAsync(BlobKey.Prefix(tenant), new MemoryStream([1]), "text/plain", Ct));
    }

    [Fact]
    public async Task Probe_PassesOnAWorkingStore()
    {
        Assert.Null(await ((IBlobStoreProbe)Store).ProbeAsync(Ct));
    }

    private sealed class NonSeekableStream(byte[] data) : MemoryStream(data)
    {
        public override bool CanSeek => false;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => base.Position; set => throw new NotSupportedException(); }
    }
}
