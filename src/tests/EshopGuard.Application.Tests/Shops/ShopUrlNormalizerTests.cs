using EshopGuard.Application.Problems;
using EshopGuard.Application.Shops;

namespace EshopGuard.Application.Tests.Shops;

/// <summary>The check of the address of an e-shop (change 10, task 1.6; AD 1).</summary>
public sealed class ShopUrlNormalizerTests
{
    [Theory]
    [InlineData("https://www.Bylinkovo.sk/", "bylinkovo.sk", "/", "https://www.bylinkovo.sk/")]
    [InlineData("bylinkovo.sk", "bylinkovo.sk", "/", "https://bylinkovo.sk/")]
    [InlineData("http://eshop.sk:80/sk/", "eshop.sk", "/sk/", "http://eshop.sk/sk/")]
    [InlineData("https://eshop.sk:443/cz", "eshop.sk", "/cz/", "https://eshop.sk/cz/")]
    [InlineData("https://eshop.sk/index.php?a=1#x", "eshop.sk", "/", "https://eshop.sk/")]
    [InlineData("https://eshop.sk/Obchod/index.html", "eshop.sk", "/Obchod/", "https://eshop.sk/Obchod/")]
    [InlineData("https://SHOP.example.CZ.", "shop.example.cz", "/", "https://shop.example.cz/")]
    public void Normalizes(string input, string domain, string basePath, string baseUrl)
    {
        var address = ShopUrlNormalizer.Normalize(input);

        Assert.Equal(new ShopAddress(domain, basePath, baseUrl), address);
    }

    [Fact]
    public void Idn_IsPunycode()
    {
        var address = ShopUrlNormalizer.Normalize("https://kvetináč.sk");

        Assert.StartsWith("xn--", address.Domain, StringComparison.Ordinal);
        Assert.EndsWith(".sk", address.Domain, StringComparison.Ordinal);
        Assert.All(address.Domain, c => Assert.True(c < 128));
        Assert.Equal($"https://{address.Domain}/", address.BaseUrl);
    }

    [Theory]
    [InlineData("http://169.254.169.254/latest/meta-data")]
    [InlineData("http://127.0.0.1/")]
    [InlineData("http://10.0.0.5/")]
    [InlineData("http://[::1]/")]
    [InlineData("http://[fd00::1]/")]
    [InlineData("http://2130706433/")]
    [InlineData("http://0x7f.1/")]
    [InlineData("http://intranet/")]
    [InlineData("http://localhost/")]
    [InlineData("http://printer.local/")]
    [InlineData("http://db.internal/")]
    [InlineData("http://nas.lan/")]
    [InlineData("http://router.home.arpa/")]
    [InlineData("https://eshop.sk:8443/")]
    [InlineData("https://user:pw@eshop.sk/")]
    [InlineData("ftp://eshop.sk/")]
    [InlineData("file:///etc/passwd")]
    public void Refuses_AddressesThatAreNotAllowed(string input)
    {
        var error = Assert.Throws<DomainException>(() => ShopUrlNormalizer.Normalize(input));

        Assert.Equal(ProblemCodes.ShopUrlNotAllowed, error.Code);
        Assert.Equal(400, error.Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("https://")]
    [InlineData("https://exa mple.sk/")]
    public void Refuses_InvalidAddresses(string? input)
    {
        var error = Assert.Throws<DomainException>(() => ShopUrlNormalizer.Normalize(input));

        Assert.Equal(ProblemCodes.ShopUrlInvalid, error.Code);
    }

    [Fact]
    public void Refuses_TooLongAddress()
    {
        var error = Assert.Throws<DomainException>(() => ShopUrlNormalizer.Normalize("https://eshop.sk/" + new string('a', ShopUrlNormalizer.MaxLength)));

        Assert.Equal(ProblemCodes.ShopUrlInvalid, error.Code);
    }

    [Fact]
    public void AllowedDevHost_PassesWithItsPort_OthersStayRefused()
    {
        var allowed = new[] { "localhost:8000" };

        var address = ShopUrlNormalizer.Normalize("http://localhost:8000/", allowed);

        Assert.Equal(new ShopAddress("localhost:8000", "/", "http://localhost:8000/"), address);
        Assert.Throws<DomainException>(() => ShopUrlNormalizer.Normalize("http://localhost:8001/", allowed));
        Assert.Throws<DomainException>(() => ShopUrlNormalizer.Normalize("http://localhost/", allowed));
    }
}
