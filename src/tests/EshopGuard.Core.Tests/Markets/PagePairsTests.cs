using System.Globalization;
using System.Text;
using EshopGuard.Core.Crawl;
using EshopGuard.Core.Languages;
using EshopGuard.Core.Markets;
using Microsoft.Extensions.DependencyInjection;

namespace EshopGuard.Core.Tests;

/// <summary>
/// The comparison of versions on a shop like goodie.sk (2. 10. 2026): the sitemap names no alternates, the product pages do
/// (hreflang), products are marked only in microdata, and the other domain is named twice (hreflang cs-CZ, a switcher link).
/// </summary>
public sealed class PagePairsTests
{
    [Fact]
    public async Task TranslatedVersion_IsPairedByTheHreflangOfItsPages_AndCountsWithItsOwnTexts()
    {
        var result = await AnalyzeAsync(copied: false);

        var cz = Assert.Single(result.Versions, v => !v.IsMain);
        Assert.Equal(("cs-cz", "https://pair-shop.cz/"), (cz.Language, cz.BaseUrl));
        Assert.Equal(VersionSamplePlanner.ModeHreflang, result.PairingMode);
        Assert.Equal(20, result.SamplePlan!.Pairs.Count);
        Assert.Equal(100, result.SamplePlan.Urls.Count);
        Assert.True(cz.Comparison!.PairKinds[PairKinds.Translation] >= 20);
        Assert.Equal([PairKinds.Translation], cz.Comparison.PairKinds.Keys);
        Assert.All(cz.Comparison.Examples, e => Assert.True(e.SharedSentenceShare < 0.2, e.MainUrl));
        Assert.True(cz.OwnTextShare > 0.7, $"own {cz.OwnTextShare}");
        Assert.True(cz.Counted);
        Assert.True(result.Versions.Single(v => v.IsMain).OwnTextShare > 0.7);
    }

    [Fact]
    public async Task VersionCopyingTheTexts_HasPairsSharingTheirSentences_AndNoOwnTexts()
    {
        var result = await AnalyzeAsync(copied: true);

        var cz = Assert.Single(result.Versions, v => !v.IsMain);
        Assert.Equal(20, result.SamplePlan!.Pairs.Count);
        Assert.All(cz.Comparison!.Examples, e => Assert.True(e.SharedSentenceShare > 0.6, e.MainUrl));
        Assert.True(cz.OwnTextShare < 0.2, $"own {cz.OwnTextShare}");
        Assert.False(cz.Counted);
    }

    private static async Task<MarketsAnalysisResult> AnalyzeAsync(bool copied)
    {
        await using var provider = TestServices.Create(new PairShopFetcher(copied), register: s => s.AddSingleton<IMarketModel>(new MockMarketModel()));
        return await provider.GetRequiredService<IMarketsAnalyzer>().AnalyzeAsync(
            new MarketsAnalysisRequest(new Uri("https://pair-shop.sk/")), null, null, TestContext.Current.CancellationToken);
    }

    /// <summary>pair-shop.sk and pair-shop.cz with 40 products each, the same paths, hreflang only on the pages.</summary>
    private sealed class PairShopFetcher(bool copied) : IPageFetcher
    {
        private const int Products = 60;

        public Task<FetchResponse> FetchAsync(Uri url, CancellationToken ct)
        {
            var sk = url.Host == "pair-shop.sk";
            var host = $"https://{url.Host}";
            string? body = url.AbsolutePath switch
            {
                "/robots.txt" => $"User-agent: *\nAllow: /\n\nSitemap: {host}/sitemap.xml\n",
                "/sitemap.xml" => $"""<?xml version="1.0"?><sitemapindex xmlns="http://www.sitemaps.org/schemas/sitemap/0.9"><sitemap><loc>{host}/sitemap-products.xml</loc></sitemap></sitemapindex>""",
                "/sitemap-products.xml" => """<?xml version="1.0"?><urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">"""
                    + string.Concat(Enumerable.Range(1, Products).Select(i => $"<url><loc>{host}/p/produkt-{i}</loc></url>")) + "</urlset>",
                "/" => Page(sk, "/", sk ? "Obchod s bylinkami." : "Obchod s bylinkami a čaji.", "<a href=\"https://pair-shop.cz/\">Česká verze</a>"),
                _ when url.AbsolutePath.StartsWith("/p/produkt-", StringComparison.Ordinal) => Product(sk, url.AbsolutePath),
                _ => null,
            };
            var type = url.AbsolutePath.EndsWith(".xml", StringComparison.Ordinal) ? "application/xml" : url.AbsolutePath == "/robots.txt" ? "text/plain" : "text/html";
            return Task.FromResult(body is null
                ? new FetchResponse { Url = url, StatusCode = 404 }
                : new FetchResponse { Url = url, StatusCode = 200, MediaType = type, Charset = "utf-8", Body = Encoding.UTF8.GetBytes(body) });
        }

        private string Product(bool sk, string path)
        {
            var n = int.Parse(path["/p/produkt-".Length..], CultureInfo.InvariantCulture);
            var slovak = $"Čaj číslo {n} pochádza z horských lúk. Lístky zbierame ručne počas leta. Sušíme ich v tieni na drevených lieskach. "
                + $"Pripravíte ho za päť minút vo vriacej vode. Chutí aj vychladený s citrónom. Balenie číslo {n} vystačí na mesiac. "
                + $"Skladujte v suchu a chlade. Hodí sa na pokojný večer po práci číslo {n}. Neobsahuje pridaný cukor ani arómy.";
            var czech = $"Čaj číslo {n} pochází z horských luk. Lístky sbíráme ručně během léta. Sušíme je ve stínu na dřevěných lískách. "
                + $"Připravíte ho za pět minut ve vroucí vodě. Chutná i vychlazený s citronem. Balení číslo {n} vystačí na měsíc. "
                + $"Skladujte v suchu a chladu. Hodí se na klidný večer po práci číslo {n}. Neobsahuje přidaný cukr ani aromata.";
            var text = sk || copied ? slovak : czech;
            var product = $"""<div itemscope itemtype="http://schema.org/Product"><h1 itemprop="name">Čaj {n}</h1><p>{text}</p><p>Cena: {n},90 {(sk ? "€" : "Kč")}</p></div>""";
            return Page(sk, path, product, "");
        }

        private static string Page(bool sk, string path, string main, string extra) => $$"""
            <!DOCTYPE html><html lang="{{(sk ? "sk" : "cs")}}"><head><meta charset="utf-8"><title>Bylinky</title>
            <meta property="og:type" content="article">
            <link rel="alternate" hreflang="sk-SK" href="https://pair-shop.sk{{path}}">
            <link rel="alternate" hreflang="cs-CZ" href="https://pair-shop.cz{{path}}">
            </head><body><header><a href="/">Bylinky</a>{{extra}}</header><main>{{main}}</main><footer><p>Bylinky s.r.o.</p></footer></body></html>
            """;
    }
}
