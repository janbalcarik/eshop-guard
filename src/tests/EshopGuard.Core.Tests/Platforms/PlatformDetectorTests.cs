using System.Text;
using EshopGuard.Core.Platforms;

namespace EshopGuard.Core.Tests.Platforms;

/// <summary>Recognition of the platform from technical signatures only (change 10, task 2.3; AD 2).</summary>
public sealed class PlatformDetectorTests
{
    private static readonly Uri Home = new("https://www.example.sk/");

    private static PlatformSignatures Signatures { get; } =
        PlatformSignatures.LoadAsync(Path.Combine(AppContext.BaseDirectory, "config", "platforms.yaml")).GetAwaiter().GetResult();

    [Theory]
    [InlineData("shoptet", "certain")]
    [InlineData("upgates", "certain")]
    [InlineData("biznisweb", "certain")]
    [InlineData("woocommerce", "certain")]
    [InlineData("shopify", "certain")]
    public void EveryPlatform_IsRecognized(string platform, string confidence)
    {
        var result = PlatformDetector.Detect(Signatures, Page(platform));

        Assert.Equal(platform, result.Platform);
        Assert.Equal(confidence, result.Confidence.ToString().ToLowerInvariant());
        Assert.All(result.Signals, s => Assert.StartsWith(platform + ".", s, StringComparison.Ordinal));
    }

    [Fact]
    public void Shoptet_HasBothSignalsOfTheSavedPage()
    {
        var result = PlatformDetector.Detect(Signatures, Page("shoptet"));

        Assert.Equal(["shoptet.cdn_host", "shoptet.web_author"], result.Signals.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void PlainPage_WithArticleAboutPlatformsAndLinksInText_IsUnknown()
    {
        var result = PlatformDetector.Detect(Signatures, Page("unknown"));

        Assert.Equal(PlatformDetection.UnknownPlatform, result.Platform);
        Assert.Equal(PlatformConfidence.Unknown, result.Confidence);
        Assert.Empty(result.Signals);
    }

    [Fact]
    public void SignaturesOfTwoPlatforms_AreUnknown_WithEverySignal()
    {
        var result = PlatformDetector.Detect(Signatures, Page("conflict"));

        Assert.Equal(PlatformDetection.UnknownPlatform, result.Platform);
        Assert.Equal(PlatformConfidence.Unknown, result.Confidence);
        Assert.Contains("woocommerce.generator", result.Signals);
        Assert.Contains("shopify.cdn_host", result.Signals);
    }

    [Fact]
    public void OneSignature_IsLikely()
    {
        var html = "<html><head><script src=\"https://cdn.myshoptet.com/a.js\"></script></head><body></body></html>";

        var result = PlatformDetector.Detect(Signatures, new PlatformPage(Home, [], [], Encoding.UTF8.GetBytes(html), "utf-8"));

        Assert.Equal("shoptet", result.Platform);
        Assert.Equal(PlatformConfidence.Likely, result.Confidence);
    }

    [Fact]
    public void TheWordOfAPlatformInTheText_ChangesNothing()
    {
        var shoptet = Encoding.UTF8.GetString(Bytes("shoptet")).Replace("<h1>", "<h1>Shopify WooCommerce Upgates BiznisWeb ", StringComparison.Ordinal);

        var result = PlatformDetector.Detect(Signatures, new PlatformPage(Home, [], [], Encoding.UTF8.GetBytes(shoptet), "utf-8"));

        Assert.Equal("shoptet", result.Platform);
        Assert.Equal(PlatformConfidence.Certain, result.Confidence);
    }

    [Fact]
    public void PathOfAnotherHost_IsNotAPathOfThePlatform()
    {
        var html = "<html><head><script src=\"https://cdn.other.sk/wp-content/plugins/woocommerce/a.js\"></script></head></html>";

        var result = PlatformDetector.Detect(Signatures, new PlatformPage(Home, [], [], Encoding.UTF8.GetBytes(html), "utf-8"));

        Assert.Equal(PlatformDetection.UnknownPlatform, result.Platform);
    }

    [Fact]
    public void InvalidSignatureFile_IsRefusedWithEveryError()
    {
        var error = Assert.Throws<InvalidDataException>(() => PlatformSignatures.Parse("""
            shoptet:
              - code: a
                kind: text
                value: shoptet
            unknown:
              - code: b
                kind: host
                value: x.sk
                contains: y
            """));

        Assert.Contains("neznámý druh", error.Message, StringComparison.Ordinal);
        Assert.Contains("other ani unknown", error.Message, StringComparison.Ordinal);
        Assert.Contains("nemá mít contains", error.Message, StringComparison.Ordinal);
    }

    private static PlatformPage Page(string name)
    {
        var headers = new List<KeyValuePair<string, string>>();
        var cookies = new List<string>();
        foreach (var line in File.ReadAllLines(Fixture(name + ".headers")))
        {
            var colon = line.IndexOf(':', StringComparison.Ordinal);
            var key = line[..colon].Trim();
            var value = line[(colon + 1)..].Trim();
            if (key == "set-cookie")
            {
                cookies.Add(value);
            }
            else
            {
                headers.Add(new(key, value));
            }
        }

        return new PlatformPage(Home, headers, cookies, Bytes(name), "utf-8");
    }

    private static byte[] Bytes(string name) => File.ReadAllBytes(Fixture(name + ".html"));

    private static string Fixture(string file) => Path.Combine(AppContext.BaseDirectory, "Fixtures", "platforms", file);
}
