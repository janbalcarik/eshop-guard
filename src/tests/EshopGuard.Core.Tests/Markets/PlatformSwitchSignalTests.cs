using EshopGuard.Core.Languages;
using EshopGuard.Core.Markets;
using EshopGuard.Core.Platforms;

namespace EshopGuard.Core.Tests.Markets;

/// <summary>
/// The currency and language switches of a certain platform (Oprava I, 2. 10. 2026): a Shoptet shop offers its currencies and
/// languages only as links of the platform (<c>/action/Currency/changeCurrency/?currencyCode=EUR</c>,
/// <c>/action/Language/changeLanguage/?language=sk</c>) at the same address. They are read only where Shoptet is certain on the
/// page (its scripts and its link), from <c>config/platforms.yaml</c>.
/// </summary>
public sealed class PlatformSwitchSignalTests
{
    private const string Site = "https://www.bylinkovo-shop.cz/";

    private static string Page(bool shoptetScripts = true, bool currency = true, bool language = true, string linkHost = "") => $$"""
        <!doctype html><html lang="cs"><head><title>Bylinkovo</title>
        {{(shoptetScripts ? """<script src="https://cdn.myshoptet.com/prj/dist/master/cms/templates/frontend_templates/shared/js/jqueryui.js"></script>""" : "")}}
        </head><body>
        <header><div class="languagesMenu">
        {{(currency ? $"""<a href="{linkHost}/action/Currency/changeCurrency/?currencyCode=CZK" rel="nofollow">CZK</a><a href="{linkHost}/action/Currency/changeCurrency/?currencyCode=EUR" rel="nofollow">EUR</a>""" : "")}}
        {{(language ? $"""<a href="{linkHost}/action/Language/changeLanguage/?language=cs" rel="nofollow">CZ</a><a href="{linkHost}/action/Language/changeLanguage/?language=sk" rel="nofollow">SK</a>""" : "")}}
        </div></header>
        <main><h1>Bylinková kosmetika</h1><p>Levandulový šampon a heřmánkový krém z vlastní zahrady.</p></main>
        </body></html>
        """;

    private static async Task<PlatformSignatures> SignaturesAsync() =>
        await PlatformSignatures.LoadAsync(Path.Combine(AppContext.BaseDirectory, "config", "platforms.yaml"), TestContext.Current.CancellationToken);

    private static async Task<MarketSignals> ReadAsync(string html) =>
        MarketSignalReader.Read(MarketSignalReaderTests.Extract(Site, html), [], new Uri(Site), [], 300, platforms: await SignaturesAsync());

    [Fact]
    public async Task CertainShoptet_OffersItsCurrencies_AndASlovakVersionByCookie()
    {
        var signals = await ReadAsync(Page());

        Assert.Equal("shoptet", signals.Platform);
        Assert.Equal(["CZK", "EUR"], signals.OfferedCurrencies);
        Assert.Contains(("offered_currency", "EUR"), signals.Values());
        var sk = Assert.Single(signals.SwitcherCandidates, s => s.Language == "sk");
        Assert.Equal("cookie", sk.Kind);

        var versions = LanguageVersionFinder.Find(signals, new Uri(Site), MarketCatalog.From(TestTexts.Catalog));
        var slovak = Assert.Single(versions, v => v.Language == "sk");
        Assert.Equal((SwitchMethods.Cookie, Site), (slovak.SwitchMethod, slovak.BaseUrl));
        Assert.Single(versions, v => v.IsMain && v.Language == "cs");
    }

    [Fact]
    public async Task VersionWithItsOwnAddress_IsNotAddedAgainByTheCookieSwitch()
    {
        var html = Page().Replace("<title>", """<link rel="alternate" hreflang="sk" href="https://www.bylinkovo-shop.cz/sk/"><title>""", StringComparison.Ordinal);

        var signals = await ReadAsync(html);
        var versions = LanguageVersionFinder.Find(signals, new Uri(Site), MarketCatalog.From(TestTexts.Catalog));

        Assert.Equal(["CZK", "EUR"], signals.OfferedCurrencies);
        var slovak = Assert.Single(versions, v => v.Language == "sk");
        Assert.Equal("https://www.bylinkovo-shop.cz/sk/", slovak.BaseUrl);
    }

    [Theory]
    [InlineData(false, true, true, "")]
    [InlineData(true, false, false, "")]
    [InlineData(true, true, true, "https://www.jiny-obchod.cz")]
    public async Task WithoutACertainPlatform_OrItsOwnLinks_NothingIsRead(bool scripts, bool currency, bool language, string linkHost)
    {
        var signals = await ReadAsync(Page(scripts, currency, language, linkHost));

        Assert.Null(signals.Platform);
        Assert.Empty(signals.OfferedCurrencies);
        Assert.DoesNotContain(signals.SwitcherCandidates, s => s.Kind == "cookie");
    }

    [Fact]
    public void SwitchWithoutParameter_IsAnErrorOfTheFile()
    {
        var error = Assert.Throws<InvalidDataException>(() => PlatformSignatures.Parse("""
            shoptet:
              - code: cdn_host
                kind: host
                value: cdn.myshoptet.com
              - code: currency_switch
                kind: currency_switch
                value: /action/Currency/changeCurrency/
            """));

        Assert.Contains("shoptet.currency_switch", error.Message, StringComparison.Ordinal);
    }
}
