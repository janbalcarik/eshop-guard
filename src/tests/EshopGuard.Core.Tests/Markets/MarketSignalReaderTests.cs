using EshopGuard.Core.Crawl;
using EshopGuard.Core.Extract;
using EshopGuard.Core.Markets;
using EshopGuard.Core.Pipeline;
using Microsoft.Extensions.Logging.Abstractions;

namespace EshopGuard.Core.Tests;

/// <summary>Technical signs of markets and versions without a model (change 7, tasks 2.1 to 2.5).</summary>
public sealed class MarketSignalReaderTests
{
    private static readonly string[] Widgets = ["weglot", "gtranslate"];

    [Fact]
    public async Task PathShop_HasHreflangAndHtmlLang()
    {
        var home = await ExtractAsync("https://path-shop.cz/");

        var signals = MarketSignalReader.Read(home, [], new Uri("https://path-shop.cz/"), Widgets, 300);

        Assert.Equal("cs", signals.HtmlLang);
        Assert.Contains(signals.Hreflang, h => h is { Language: "sk", Url: "https://path-shop.cz/sk/", Source: "page" });
        Assert.DoesNotContain(signals.Hreflang, h => h.Language == "x-default");
        Assert.Contains(signals.SwitcherCandidates, c => c is { Kind: "path", Language: "sk", Url: "https://path-shop.cz/sk/" });
        Assert.Equal("cz", signals.Tld);
        Assert.Contains("420", signals.PhonePrefixes);
        Assert.Contains(signals.FooterLinks, l => l.Url == "https://path-shop.cz/doprava.html");
    }

    [Fact]
    public async Task DomainShop_LinkToAnotherDomainIsASwitchCandidate()
    {
        var home = await ExtractAsync("https://domain-shop.cz/");

        var signals = MarketSignalReader.Read(home, [], new Uri("https://domain-shop.cz/"), Widgets, 300);

        Assert.Empty(signals.Hreflang);
        Assert.Contains(signals.SwitcherCandidates, c => c is { Kind: "domain", Url: "https://domain-shop.sk/", Language: null });
        Assert.Contains(("switcher", "domain-shop.sk"), signals.Values());
    }

    [Fact]
    public void CurrencyAndPhonePrefix()
    {
        var html = """
            <html lang="sk"><head><script type="application/ld+json">{"@type":"Product","name":"Mydlo","offers":{"priceCurrency":"EUR","price":"3"}}</script></head>
            <body><main><p>Mydlo z prírodných surovín pre celú rodinu.</p></main>
            <footer><a href="tel:+421911705846">Zavolajte nám</a></footer></body></html>
            """;
        var home = Extract("https://shop.sk/", html);

        var signals = MarketSignalReader.Read(home, [], new Uri("https://shop.sk/"), Widgets, 300);

        Assert.Contains("EUR", signals.Currencies);
        Assert.Contains("421", signals.PhonePrefixes);
        Assert.Equal(["+421911705846"], home.PhoneNumbers);
    }

    [Fact]
    public void SwitchCandidates_DependOnTheAddressOnly()
    {
        var site = new Uri("https://shop.cz/");
        Uri[] targets =
        [
            new("https://shop.cz/sk/"), new("https://shop.cz/bio/"), new("https://shop.cz/?lang=sk"), new("https://sk.shop.cz/"),
            new("https://shop.sk/"), new("https://other.sk/"), new("https://shop.sk/produkt"), new("https://shop.cz/sk/produkt"),
        ];

        var candidates = MarketSignalReader.SwitcherCandidates(targets, site);

        Assert.Equal(
            [("https://shop.cz/sk/", "path", "sk"), ("https://shop.cz/?lang=sk", "query", "sk"), ("https://sk.shop.cz/", "subdomain", "sk"), ("https://shop.sk/", "domain", null)],
            candidates.Select(c => (c.Url, c.Kind, c.Language)));
    }

    [Fact]
    public async Task NoResult_DependsOnTheTextOfALink()
    {
        var home = await ExtractAsync("https://path-shop.cz/");
        var renamed = new ExtractedPage
        {
            HtmlLang = home.HtmlLang,
            Alternates = home.Alternates,
            Links = home.Links.Select(l => l with { Text = "x" }).ToList(),
            FooterLinks = home.FooterLinks.Select(l => l with { Text = "y" }).ToList(),
            PhoneNumbers = home.PhoneNumbers,
        };
        var site = new Uri("https://path-shop.cz/");

        var original = MarketSignalReader.Read(home, [], site, Widgets, 300);
        var withOtherTexts = MarketSignalReader.Read(renamed, [], site, Widgets, 300);

        Assert.Equal(original.SwitcherCandidates, withOtherTexts.SwitcherCandidates);
        Assert.Equal(original.Values(), withOtherTexts.Values());
        Assert.Equal(original.HomeLinks.Select(l => l.Url), withOtherTexts.HomeLinks.Select(l => l.Url));
    }

    [Fact]
    public async Task ScriptSwitchAndTranslationWidget()
    {
        var script = MarketSignalReader.Read(await ExtractAsync("https://script-shop.cz/"), [], new Uri("https://script-shop.cz/"), Widgets, 300);
        var widget = MarketSignalReader.Read(await ExtractAsync("https://widget-shop.cz/"), [], new Uri("https://widget-shop.cz/"), Widgets, 300);

        Assert.Contains(script.ScriptOnlySwitchElements, e => e is { Tag: "button", Language: "sk" });
        Assert.Empty(script.BrowserTranslationWidgets);
        Assert.Equal(["weglot"], widget.BrowserTranslationWidgets);
    }

    [Fact]
    public void Sitemap_ReadsAlternates()
    {
        var parsed = SitemapParser.Parse(System.Text.Encoding.UTF8.GetBytes(
            """
            <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9" xmlns:xhtml="http://www.w3.org/1999/xhtml">
              <url><loc>https://shop.cz/a</loc>
                <xhtml:link rel="alternate" hreflang="cs" href="https://shop.cz/a"/>
                <xhtml:link rel="alternate" hreflang="sk-SK" href="https://shop.cz/sk/a"/>
              </url>
            </urlset>
            """));

        var entry = Assert.Single(parsed.Entries);
        Assert.Equal([("cs", "https://shop.cz/a"), ("sk-SK", "https://shop.cz/sk/a")], entry.Alternates);
    }

    [Fact]
    public void JsonLd_ReadsIdentifiersAndCurrenciesInGraphAndLists()
    {
        var html = """
            <html><head>
            <script type="application/ld+json">{"@graph":[{"@type":"Product","name":"A","gtin13":"8591234000017","sku":"BD-1","offers":[{"priceCurrency":"czk"}]}]}</script>
            <script type="application/ld+json">[{"@type":"Product","name":"B","mpn":12345,"productID":"isbn:1"}]</script>
            </head><body><main><p>Text produktu s dostatečnou délkou pro čtení.</p></main></body></html>
            """;

        var page = Extract("https://shop.cz/p", html);

        Assert.Equal(["CZK"], page.Currencies);
        Assert.Equal(
            [("gtin13", "8591234000017"), ("sku", "BD-1"), ("mpn", "12345"), ("productID", "isbn:1")],
            page.ProductIds.Select(i => (i.Kind, i.Value)));
    }

    [Fact]
    public async Task TwoVersionsOfAProduct_HaveTheSameHreflangGroup()
    {
        var fetcher = MultiHostFileSystemPageFetcher.ForVersions();
        await using var provider = TestServices.Create(fetcher);
        var step = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ExtractStep>(provider);
        var site = new SiteScope(new Uri("https://path-shop.cz/"));
        var ct = TestContext.Current.CancellationToken;

        var cs = await step.ExtractPageAsync(site, new Uri("https://path-shop.cz/produkt-2.html"), await HtmlAsync(fetcher, "https://path-shop.cz/produkt-2.html"), false, [], ct);
        var sk = await step.ExtractPageAsync(site with { Version = Languages.VersionCrawlScope.ForRoot(new Uri("https://path-shop.cz/sk/"), "sk") },
            new Uri("https://path-shop.cz/sk/produkt-2.html"), await HtmlAsync(fetcher, "https://path-shop.cz/sk/produkt-2.html"), false, [], ct);

        Assert.NotNull(cs.Info.HreflangGroup);
        Assert.Equal(cs.Info.HreflangGroup, sk.Info.HreflangGroup);
        Assert.Equal(("cs", "sk"), (cs.Info.Language, sk.Info.Language));
        Assert.Equal(cs.Info.ProductIds, sk.Info.ProductIds);
    }

    internal static async Task<ExtractedPage> ExtractAsync(string url) =>
        Extract(url, await HtmlAsync(MultiHostFileSystemPageFetcher.ForVersions(), url));

    internal static ExtractedPage Extract(string url, string html) =>
        new ContentExtractor(NullLogger<ContentExtractor>.Instance).Extract(new Uri(url), html);

    private static async Task<string> HtmlAsync(IPageFetcher fetcher, string url)
    {
        var response = await fetcher.FetchAsync(new Uri(url), TestContext.Current.CancellationToken);
        return HtmlDecoding.Decode(response.Body!, response.Charset);
    }
}
