using System.Net;
using System.Text;
using EshopGuard.Core.Crawl;
using EshopGuard.Core.Languages;
using EshopGuard.Core.Markets;
using EshopGuard.Core.Pipeline;
using Microsoft.Extensions.DependencyInjection;

namespace EshopGuard.Core.Tests;

/// <summary>
/// The number of products from the sitemap (bonami.sk, 2. 10. 2026): a product sitemap bigger than a page is read, product
/// sitemaps are read before the others, a product sitemap not read whole makes the number unknown with its lower bound, and
/// a product listed in two sitemaps (products and their images) counts once.
/// </summary>
public sealed class SitemapCompletenessTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task HttpFetcher_TakesTheLimitOfTheRequest_OverTheLimitOfAPage()
    {
        await using var server = new LocalHttpServer(_ => new LocalHttpServer.Answer(200, new string('x', 3000), "application/xml"));
        await using var provider = HttpServices(o => o.Crawl.MaxPageBytes = 1000);
        var fetcher = provider.GetRequiredService<IPageFetcher>();
        var url = new Uri($"http://shop.test:{server.Port}/sitemap/product_0.xml");

        var page = await fetcher.FetchAsync(new FetchRequest(url), Ct);
        var sitemap = await fetcher.FetchAsync(new FetchRequest(url) { MaxBytes = 5000 }, Ct);

        Assert.True(page.TooLarge);
        Assert.True(sitemap.IsSuccess);
        Assert.Equal(3000, sitemap.Body!.Length);
    }

    [Fact]
    public async Task ProductSitemapBiggerThanAPage_IsRead_ByTheDiscovery()
    {
        var products = Urlset(Enumerable.Range(1, 40).Select(i => $"/p/produkt-cislo-{i:D4}"));
        await using var server = new LocalHttpServer(request => request.Path switch
        {
            "/robots.txt" => new LocalHttpServer.Answer(200, "User-agent: *\nAllow: /\nSitemap: /sitemap.xml\n", "text/plain"),
            "/sitemap.xml" => new LocalHttpServer.Answer(200, Index("/sitemap/product_0.xml"), "application/xml"),
            "/sitemap/product_0.xml" => new LocalHttpServer.Answer(200, products, "application/xml"),
            _ => new LocalHttpServer.Answer(404),
        });
        await using var provider = HttpServices(o =>
        {
            o.Crawl.MaxPageBytes = 1000;
            o.Crawl.RequestsPerSecond = 0;
        });
        var site = new SiteScope(new Uri($"http://shop.test:{server.Port}/"));

        var discovery = await provider.GetRequiredService<DiscoveryStep>().DiscoverAsync(new DiscoveryInput(site, new CrawlLimits(200, 100, [], [], null)), null, Ct);

        Assert.True(products.Length > 1000);
        Assert.Equal(40, discovery.SitemapEntries.Count(e => e.ProductHint));
        Assert.False(discovery.ProductSitemapsIncomplete);
    }

    [Fact]
    public async Task UnreadableProductSitemap_MakesTheNumberUnknown_WithItsLowerBound()
    {
        var result = await AnalyzeAsync(new Dictionary<string, string>
        {
            ["/sitemap.xml"] = Index("/sitemap/product_0.xml", "/sitemap/product_1.xml"),
            ["/sitemap/product_0.xml"] = Urlset(Products(1, 12)),
        });

        var main = Assert.Single(result.Versions);
        Assert.Null(main.ProductCount);
        Assert.Equal(12, main.ProductCountAtLeast);
        Assert.Contains(VersionCodes.ProductCountIncomplete, main.Codes);
        Assert.True(result.Plan!.ProductCountUnknown);
        Assert.Contains(result.Warnings, w => w.Code == "sitemap_unreadable");
    }

    [Fact]
    public async Task ProductInTwoSitemaps_CountsOnce()
    {
        var result = await AnalyzeAsync(new Dictionary<string, string>
        {
            ["/sitemap.xml"] = Index("/sitemap/product_0.xml", "/sitemap/product_images_0.xml"),
            ["/sitemap/product_0.xml"] = Urlset(Products(1, 12)),
            ["/sitemap/product_images_0.xml"] = Urlset(Products(1, 12)),
        });

        var main = Assert.Single(result.Versions);
        Assert.Equal(12, main.ProductCount);
        Assert.Null(main.ProductCountAtLeast);
        Assert.DoesNotContain(VersionCodes.ProductCountIncomplete, main.Codes);
    }

    [Fact]
    public async Task LimitOfSitemapUrls_StopsTheOtherPages_NotTheProducts()
    {
        var result = await AnalyzeAsync(
            new Dictionary<string, string>
            {
                ["/sitemap.xml"] = Index("/sitemap/static_pages.xml", "/sitemap/product_0.xml"),
                ["/sitemap/static_pages.xml"] = Urlset(Enumerable.Range(1, 30).Select(i => $"/clanok-{i}")),
                ["/sitemap/product_0.xml"] = Urlset(Products(1, 12)),
            },
            o => o.Crawl.MaxSitemapUrls = 20);

        var main = Assert.Single(result.Versions);
        Assert.Equal(12, main.ProductCount);
        Assert.DoesNotContain(VersionCodes.ProductCountIncomplete, main.Codes);
        Assert.Contains(result.Warnings, w => w.Code == "sitemap_too_many");
    }

    private static IEnumerable<string> Products(int from, int to) => Enumerable.Range(from, to - from + 1).Select(i => $"/p/produkt-{i}");

    private static string Index(params string[] paths) =>
        """<?xml version="1.0"?><sitemapindex xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">"""
        + string.Concat(paths.Select(p => $"<sitemap><loc>{p}</loc></sitemap>")) + "</sitemapindex>";

    private static string Urlset(IEnumerable<string> paths) =>
        """<?xml version="1.0"?><urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">"""
        + string.Concat(paths.Select(p => $"<url><loc>{p}</loc></url>")) + "</urlset>";

    private static ServiceProvider HttpServices(Action<Options.EshopGuardOptions> configure)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostAddressResolver>(new HttpPageFetcherSsrfTests.FakeResolver([IPAddress.Loopback]));
        services.AddEshopGuard(o =>
        {
            o.Crawl.AllowPrivateNetwork = true;
            o.Jev.UseMock = true;
            o.Rewrite.UseMock = true;
            configure(o);
        });
        return services.BuildServiceProvider();
    }

    private static async Task<MarketsAnalysisResult> AnalyzeAsync(Dictionary<string, string> sitemaps, Action<Options.EshopGuardOptions>? configure = null)
    {
        await using var provider = TestServices.Create(new SitemapShopFetcher(sitemaps), configure, register: s => s.AddSingleton<IMarketModel>(new MockMarketModel()));
        return await provider.GetRequiredService<IMarketsAnalyzer>().AnalyzeAsync(
            new MarketsAnalysisRequest(new Uri("https://sitemap-shop.sk/")), null, null, Ct);
    }

    /// <summary>A shop of one version: robots.txt, a home page, product pages and the given sitemaps (missing ones answer 404).</summary>
    private sealed class SitemapShopFetcher(Dictionary<string, string> sitemaps) : IPageFetcher
    {
        public Task<FetchResponse> FetchAsync(Uri url, CancellationToken ct)
        {
            var path = url.AbsolutePath;
            var (type, body) = path switch
            {
                "/robots.txt" => ("text/plain", "User-agent: *\nAllow: /\nSitemap: https://sitemap-shop.sk/sitemap.xml\n"),
                "/" => ("text/html", """<!DOCTYPE html><html lang="sk"><head><meta charset="utf-8"><title>Obchod</title></head><body><main><p>Obchod s bylinkami z horských lúk a čajmi.</p></main></body></html>"""),
                _ when sitemaps.TryGetValue(path, out var xml) => ("application/xml", xml.Replace("<loc>/", "<loc>https://sitemap-shop.sk/", StringComparison.Ordinal)),
                _ when path.StartsWith("/p/", StringComparison.Ordinal) => ("text/html", $"""<!DOCTYPE html><html lang="sk"><head><meta charset="utf-8"><title>Čaj</title></head><body><main><h1>Čaj {path[3..]}</h1><p>Bylinkový čaj z horských lúk, zbieraný ručne počas leta.</p></main></body></html>"""),
                _ => (null, null),
            };
            return Task.FromResult(body is null
                ? new FetchResponse { Url = url, StatusCode = 404 }
                : new FetchResponse { Url = url, StatusCode = 200, MediaType = type, Charset = "utf-8", Body = Encoding.UTF8.GetBytes(body) });
        }
    }
}
