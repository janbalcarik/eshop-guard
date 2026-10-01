namespace EshopGuard.Storage.Tests;

public sealed class BlobKeyTests
{
    private static readonly Guid Tenant = Guid.Parse("0192a1b2-0000-7000-8000-000000000001");
    private static readonly Guid Shop = Guid.Parse("0192a1b2-0000-7000-8000-000000000002");

    [Fact]
    public void ForShop_BuildsTenantAndShopPath()
    {
        var key = BlobKey.ForShop(Tenant, Shop, "pages", "p1.html.gz");
        Assert.Equal($"tenants/{Tenant}/shops/{Shop}/pages/p1.html.gz", key.Value);
        Assert.Equal(key.Value, key.ToString());
        Assert.False(key.IsPrefix);
    }

    [Fact]
    public void ForTenant_BuildsTenantPath()
    {
        Assert.Equal($"tenants/{Tenant}/evidence/cert_2026-01.pdf", BlobKey.ForTenant(Tenant, "evidence", "cert_2026-01.pdf").Value);
    }

    [Fact]
    public void Prefix_EndsWithSlash()
    {
        Assert.Equal($"tenants/{Tenant}/", BlobKey.Prefix(Tenant).Value);
        Assert.Equal($"tenants/{Tenant}/shops/{Shop}/", BlobKey.Prefix(Tenant, Shop).Value);
        Assert.True(BlobKey.Prefix(Tenant).IsPrefix);
    }

    [Theory]
    [InlineData("..")]
    [InlineData(".")]
    [InlineData("")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("/etc")]
    [InlineData("C:")]
    [InlineData(".hidden")]
    [InlineData("-dash")]
    [InlineData("with space")]
    [InlineData("čeština")]
    public void InvalidPart_IsRejected(string part)
    {
        Assert.Throws<ArgumentException>(() => BlobKey.ForShop(Tenant, Shop, "pages", part));
    }

    [Fact]
    public void NullPart_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => BlobKey.ForShop(Tenant, Shop, "pages", null!));
        Assert.ThrowsAny<ArgumentException>(() => BlobKey.ForShop(Tenant, Shop, (string[])null!));
    }

    [Fact]
    public void MissingSegments_AreRejected()
    {
        Assert.Throws<ArgumentException>(() => BlobKey.ForShop(Tenant, Shop));
        Assert.Throws<ArgumentException>(() => BlobKey.ForTenant(Tenant));
    }

    [Fact]
    public void EmptyIds_AreRejected()
    {
        Assert.Throws<ArgumentException>(() => BlobKey.ForShop(Guid.Empty, Shop, "a"));
        Assert.Throws<ArgumentException>(() => BlobKey.ForShop(Tenant, Guid.Empty, "a"));
        Assert.Throws<ArgumentException>(() => BlobKey.Prefix(Guid.Empty));
    }

    [Fact]
    public void PartLongerThan200_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => BlobKey.ForTenant(Tenant, new string('a', 201)));
        Assert.Equal(200, BlobKey.ForTenant(Tenant, new string('a', 200)).Segments[^1].Length);
    }

    [Fact]
    public void KeyLongerThan1024_IsRejected()
    {
        var parts = Enumerable.Repeat(new string('a', 150), 7).ToArray();
        Assert.Throws<ArgumentException>(() => BlobKey.ForShop(Tenant, Shop, parts));
    }

    [Fact]
    public void Equality_IsByValue()
    {
        Assert.Equal(BlobKey.ForShop(Tenant, Shop, "a"), BlobKey.ForShop(Tenant, Shop, "a"));
        Assert.NotEqual(BlobKey.ForShop(Tenant, Shop, "a"), BlobKey.ForShop(Tenant, Shop, "b"));
    }
}
