using System.IO.Compression;
using System.Text;
using EshopGuard.Core.Classify;
using EshopGuard.Core.Crawl;
using EshopGuard.Core.Extract;
using EshopGuard.Core.Models;
using EshopGuard.Core.Options;
using Microsoft.Extensions.Logging.Abstractions;

namespace EshopGuard.Core.Tests;

public class CrawlTests
{
    [Fact]
    public void Robots_LongestMatchWinsAndAllowWinsTie()
    {
        var robots = RobotsTxt.Parse(
            """
            User-agent: *
            Disallow: /kosik
            Disallow: /*?sort=
            Disallow: /private/
            Allow: /private/verejne
            Disallow: /*.pdf$
            """,
            "EshopGuard");

        Assert.False(robots.IsAllowed(new Uri("https://shop.example/kosik.html")));
        Assert.False(robots.IsAllowed(new Uri("https://shop.example/kosik")));
        Assert.True(robots.IsAllowed(new Uri("https://shop.example/kosmetika")));
        Assert.False(robots.IsAllowed(new Uri("https://shop.example/sampony?sort=price")));
        Assert.False(robots.IsAllowed(new Uri("https://shop.example/private/tajne")));
        Assert.True(robots.IsAllowed(new Uri("https://shop.example/private/verejne-info")));
        Assert.False(robots.IsAllowed(new Uri("https://shop.example/vop.pdf")));
        Assert.True(robots.IsAllowed(new Uri("https://shop.example/vop.pdf?download=1")));
        Assert.True(robots.IsAllowed(new Uri("https://shop.example/robots.txt")));
    }

    [Fact]
    public void Robots_UsesOwnGroupBeforeStarGroup()
    {
        var robots = RobotsTxt.Parse("User-agent: *\nDisallow: /\n\nUser-agent: EshopGuard\nDisallow: /admin\n", "EshopGuard");

        Assert.True(robots.IsAllowed(new Uri("https://shop.example/produkt")));
        Assert.False(robots.IsAllowed(new Uri("https://shop.example/admin/nastaveni")));
    }

    [Fact]
    public void Robots_ReadsSitemapsAndEmptyDisallowAllowsEverything()
    {
        var robots = RobotsTxt.Parse("Sitemap: https://shop.example/sitemap_index.xml\nUser-agent: *\nDisallow:\n", "EshopGuard");

        Assert.Equal(["https://shop.example/sitemap_index.xml"], robots.Sitemaps);
        Assert.True(robots.IsAllowed(new Uri("https://shop.example/cokoliv")));
        Assert.False(RobotsTxt.DisallowAll.IsAllowed(new Uri("https://shop.example/")));
    }

    [Fact]
    public void Normalize_RemovesTrackingParametersAndFragment()
    {
        var url = UrlTools.Normalize(new Uri("HTTPS://WWW.Shop.Example:443/produkt?id=5&utm_source=x&gclid=1#recenze"));

        Assert.Equal("https://www.shop.example/produkt?id=5", url.AbsoluteUri);
        Assert.True(UrlTools.IsSameSite(url, new Uri("http://shop.example/")));
        Assert.False(UrlTools.IsSameSite(url, new Uri("https://jiny-shop.example/")));
        Assert.False(UrlTools.IsSameSite(new Uri("http://localhost:8000/"), new Uri("http://localhost:9000/")));
    }

    [Theory]
    [InlineData("https://shop.example/kosik", true)]
    [InlineData("https://shop.example/cart/", true)]
    [InlineData("https://shop.example/prihlaseni", true)]
    [InlineData("https://shop.example/vyhledavani?q=mydlo", true)]
    [InlineData("https://shop.example/sampony?razeni=cena", true)]
    [InlineData("https://shop.example/sampony/strana-2", true)]
    [InlineData("https://shop.example/vop.pdf", true)]
    [InlineData("https://shop.example/img/foto.jpg", true)]
    [InlineData("https://shop.example/kosmetika/bylinny-sampon", false)]
    [InlineData("https://shop.example/obchodni-podminky", false)]
    public void Filter_ExcludesUrlsThatAreNotContentPages(string url, bool excluded)
    {
        var filter = new UrlFilter(new CrawlOptions().ExcludeUrlPatterns, [], []);

        Assert.Equal(excluded, filter.IsExcluded(new Uri(url)));
    }

    [Fact]
    public void Filter_AppliesUserIncludeAndExclude()
    {
        var filter = new UrlFilter([], include: ["/kosmetika/"], exclude: ["akce"]);

        Assert.False(filter.IsExcluded(new Uri("https://shop.example/kosmetika/sampon")));
        Assert.True(filter.IsExcluded(new Uri("https://shop.example/potraviny/most")));
        Assert.True(filter.IsExcluded(new Uri("https://shop.example/kosmetika/akce-sampon")));
    }

    [Fact]
    public void Sitemap_ReadsPlainGzipAndIndex()
    {
        const string urlset = """<?xml version="1.0" encoding="UTF-8"?><urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9"><url><loc> https://shop.example/a </loc></url><url><loc>https://shop.example/b</loc></url></urlset>""";

        var plain = SitemapParser.Parse(Encoding.UTF8.GetBytes(urlset));
        Assert.False(plain.IsIndex);
        Assert.Equal(["https://shop.example/a", "https://shop.example/b"], plain.Locations);

        using var compressed = new MemoryStream();
        using (var gzip = new GZipStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
        {
            gzip.Write(Encoding.UTF8.GetBytes(urlset));
        }

        Assert.Equal(plain.Locations, SitemapParser.Parse(compressed.ToArray()).Locations);

        var index = SitemapParser.Parse(Encoding.UTF8.GetBytes(
            """<sitemapindex xmlns="http://www.sitemaps.org/schemas/sitemap/0.9"><sitemap><loc>https://shop.example/product-sitemap.xml.gz</loc></sitemap></sitemapindex>"""));
        Assert.True(index.IsIndex);
        Assert.Equal(["https://shop.example/product-sitemap.xml.gz"], index.Locations);
    }

    [Fact]
    public void Sitemap_ReadsLastmod_InvalidDateIsNull()
    {
        var parsed = SitemapParser.Parse(Encoding.UTF8.GetBytes(
            """
            <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
              <url><loc>https://shop.example/a</loc><lastmod>2026-09-30</lastmod></url>
              <url><loc>https://shop.example/b</loc><lastmod>2026-09-30T14:05:00+02:00</lastmod></url>
              <url><loc>https://shop.example/c</loc><lastmod>včera</lastmod></url>
              <url><loc>https://shop.example/d</loc></url>
            </urlset>
            """));

        Assert.Equal(
            [
                new SitemapParser.SitemapLocation("https://shop.example/a", new DateTimeOffset(2026, 9, 30, 0, 0, 0, TimeSpan.Zero)),
                new SitemapParser.SitemapLocation("https://shop.example/b", new DateTimeOffset(2026, 9, 30, 12, 5, 0, TimeSpan.Zero)),
                new SitemapParser.SitemapLocation("https://shop.example/c", null),
                new SitemapParser.SitemapLocation("https://shop.example/d", null),
            ],
            parsed.Entries);
    }

    [Fact]
    public void Decode_UsesMetaCharsetOfLegacyPages()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var html = "<html><head><meta charset=\"windows-1250\"></head><body>Šetrný k přírodě</body></html>";

        var text = HtmlDecoding.Decode(Encoding.GetEncoding(1250).GetBytes(html), charset: null);

        Assert.Contains("Šetrný k přírodě", text);
    }

    [Fact]
    public void Extract_SeparatesFrameAndReadsStructuredData()
    {
        const string html = """
            <html><head><title>Šampon | Shop</title><meta name="Description" content="Popis stránky.">
            <meta property="og:type" content="product">
            <script type="application/ld+json">{"@graph":[{"@type":"WebPage"},{"@type":["Product"],"description":"<p>Šampon s levandulí.</p>"}]}</script>
            <script type="application/ld+json">{ broken json </script>
            </head><body>
            <header>Doručujeme uhlíkově neutrálně po celé republice.</header>
            <nav><a href="/kategorie">Kategorie</a></nav>
            <main><h1>Šampon</h1><p>Tento šampon je šetrný k přírodě.<br>Obsahuje levanduli.</p>
            <script>var x = "neviditelné";</script>
            <img src="/img/eco-sampon.jpg" alt="Eko šampon">
            <p><a href="/obchodni-podminky.pdf">Obchodní podmínky (PDF)</a></p></main>
            <footer><p>Rodinný e-shop od roku 2010.</p></footer>
            </body></html>
            """;

        var page = new ContentExtractor(NullLogger<ContentExtractor>.Instance).Extract(new Uri("https://shop.example/sampon"), html);

        Assert.Equal("Šampon | Shop", page.Title);
        Assert.Equal("Popis stránky.", page.MetaDescription);
        Assert.Equal("product", page.OgType);
        Assert.True(page.HasProductJsonLd);
        Assert.Equal("Šampon s levandulí.", page.JsonLdDescription);
        Assert.Equal(2, page.ChromeRegions.Count);
        Assert.Contains(page.ChromeRegions, r => r.Any(b => b.Text == "Doručujeme uhlíkově neutrálně po celé republice."));
        Assert.Contains(page.MainBlocks, b => b.Text == "Tento šampon je šetrný k přírodě.");
        Assert.Contains(page.MainBlocks, b => b.Text == "Obsahuje levanduli.");
        Assert.DoesNotContain(page.MainBlocks, b => b.Text.Contains("neviditelné") || b.Text.Contains("Rodinný") || b.Text.Contains("uhlíkově"));
        var image = Assert.Single(page.Images);
        Assert.Equal("eco-sampon.jpg", image.FileName);
        Assert.Equal("Eko šampon", image.Alt);
        Assert.Contains(page.Links, l => l.Url.AbsoluteUri == "https://shop.example/obchodni-podminky.pdf" && l.Text == "Obchodní podmínky (PDF)");
    }

    [Theory]
    [InlineData("<div class=\"flags\"><span>Novinka</span><span>EKO</span></div>", "Novinka EKO")]
    [InlineData("<p><b>Eko</b>logický šampon</p>", "Ekologický šampon")]
    [InlineData("<p>Viz <a href=\"/vop\">podmínky</a>.</p>", "Viz podmínky.")]
    [InlineData("<p>Obal<wbr>materiál je papír</p>", "Obalmateriál je papír")]
    [InlineData("<ul><li>Bez parabenů</li><li>Veganské</li></ul>", "Bez parabenů|Veganské")]
    [InlineData("<p>Šetrné k přírodě<br>Vyrobeno v ČR</p>", "Šetrné k přírodě|Vyrobeno v ČR")]
    [InlineData("<h2><span>Eko</span><span>řada</span> (novinka)</h2>", "Eko řada (novinka)")]
    [InlineData("<div hidden><p>Text ve sbalené záložce</p></div>", "Text ve sbalené záložce")]
    public void HtmlText_SeparatesInlineElementsButKeepsFormattingInsideWords(string html, string expected)
    {
        var document = new AngleSharp.Html.Parser.HtmlParser().ParseDocument($"<html><body>{html}</body></html>");

        var blocks = HtmlText.ExtractBlocks(document.Body);

        Assert.Equal(expected.Split('|'), blocks.Select(b => b.Text));
    }

    [Fact]
    public void Extract_LeavesMenusAndBreadcrumbsOutOfTheText()
    {
        const string html = """
            <html><body>
            <header><a href="/">Obchod</a><ul role="menubar"><li role="none"><a role="menuitem" href="/eko">Ekologická drogéria</a></li></ul></header>
            <main><ol class="breadcrumb" itemscope itemtype="https://schema.org/BreadcrumbList"><li>Domov</li><li>Šampón s levanduľou</li></ol>
            <h1>Šampón s levanduľou</h1><p>Tento šampón je ekologický a šetrný k prírode.</p></main>
            <footer><nav><a href="/op">Obchodné podmienky</a></nav><p>Rodinný e-shop od roku 2010.</p></footer>
            </body></html>
            """;

        var page = new ContentExtractor(NullLogger<ContentExtractor>.Instance).Extract(new Uri("https://shop.example/sampon"), html);

        var frame = page.ChromeRegions.SelectMany(r => r).Select(b => b.Text).ToList();
        Assert.DoesNotContain("Ekologická drogéria", frame);
        Assert.DoesNotContain("Obchodné podmienky", frame);
        Assert.Contains("Rodinný e-shop od roku 2010.", frame);
        var main = page.MainBlocks.Select(b => b.Text).ToList();
        Assert.DoesNotContain("Domov", main);
        Assert.Contains("Šampón s levanduľou", main);
        // Links are still read, for crawling and for signs such as the withdrawal function.
        Assert.Contains(page.Links, l => l.Text == "Ekologická drogéria");
    }

    [Fact]
    public void HtmlText_ReadsEveryBadgeAsItsOwnBlock()
    {
        var document = new AngleSharp.Html.Parser.HtmlParser().ParseDocument(
            """<p>Šampón <span class="flag flag-eco">Eco</span><span class="flag">Vegan</span> s levanduľou</p>""");

        var blocks = HtmlText.ExtractBlocks(document.Body).Select(b => b.Text).ToList();

        Assert.Equal(["Šampón", "Eco", "Vegan", "s levanduľou"], blocks);
    }

    [Fact]
    public void Extract_LeavesFilterOptionsOutButKeepsOtherLabels()
    {
        const string html = """
            <html><body>
            <aside><div><input type="checkbox" id="f1"><label for="f1">Eco<span>1</span></label></div>
            <label><input type="radio" name="s"> Najlacnejšie</label>
            <p>Doprava zadarmo nad 50 €.</p></aside>
            <main><h1>Prací gél</h1><p>Prací gél na všetky druhy bielizne.</p></main>
            </body></html>
            """;

        var page = new ContentExtractor(NullLogger<ContentExtractor>.Instance).Extract(new Uri("https://shop.example/gel"), html);

        Assert.Equal(["Doprava zadarmo nad 50 €."], page.ChromeRegions.SelectMany(r => r).Select(b => b.Text));
    }

    [Fact]
    public void Extract_LeavesListsOfLinksOutButKeepsListsOfText()
    {
        const string html = """
            <html><body>
            <aside><ul><li><a href="/k1">Liečivé huby</a></li><li><a href="/k2">Zelená domácnosť</a><ul><li><a href="/k3">Ekodrogéria</a></li></ul></li></ul>
            <p>Doprava zadarmo nad 50 €.</p></aside>
            <main><h1>Prací gél</h1><ul><li>Ekologický prací gél na všetky druhy bielizne.</li><li>Balenie 1 l na 30 praní.</li></ul></main>
            </body></html>
            """;

        var page = new ContentExtractor(NullLogger<ContentExtractor>.Instance).Extract(new Uri("https://shop.example/gel"), html);

        var frame = page.ChromeRegions.SelectMany(r => r).Select(b => b.Text).ToList();
        Assert.Equal(["Doprava zadarmo nad 50 €."], frame);
        Assert.Contains("Ekologický prací gél na všetky druhy bielizne.", page.MainBlocks.Select(b => b.Text));
    }

    [Fact]
    public void Extract_UsesSmartReaderForArticleLikePages()
    {
        var paragraphs = string.Concat(Enumerable.Range(1, 8).Select(i =>
            $"<p>Odstavec {i}: Naše mýdla vaříme ručně z rostlinných olejů a bylin z vlastní zahrady, každou várku necháváme zrát šest týdnů, aby bylo mýdlo jemné a vydrželo dlouho.</p>"));
        var html = $"<html><head><title>Jak vaříme mýdlo</title></head><body><nav><a href=\"/\">Úvod</a></nav><article><h1>Jak vaříme mýdlo</h1>{paragraphs}</article><footer><p>Rodinný e-shop od roku 2010.</p></footer></body></html>";

        var page = new ContentExtractor(NullLogger<ContentExtractor>.Instance).Extract(new Uri("https://shop.example/blog/mydlo"), html);

        Assert.Equal(ExtractionMethod.Readability, page.Method);
        Assert.Contains(page.MainBlocks, b => b.Text.StartsWith("Odstavec 8:", StringComparison.Ordinal));
        Assert.DoesNotContain(page.MainBlocks, b => b.Text.Contains("Rodinný e-shop", StringComparison.Ordinal));
    }

    [Fact]
    public void Extract_KeepsBadgesAndBreadcrumbsRecognizableOnTheSmartReaderPath()
    {
        // SmartReader removes class attributes; badges and breadcrumbs are recognized by their classes.
        var paragraphs = string.Concat(Enumerable.Range(1, 8).Select(i =>
            $"<p>Odstavec {i}: Kakaové karamelky vyrábame ručne z kakaového masla a kokosového cukru, každú várku balíme do papiera, aby vydržali čerstvé celé týždne.</p>"));
        var html = $"""
            <html><head><title>Kakaové karamelky</title></head><body>
            <article><ol class="breadcrumb"><li><a href="/">Domov</a></li><li><a href="/sladkosti">Sladkosti</a></li><li>Kakaové karamelky</li></ol>
            <h1>Kakaové karamelky</h1><div class="flags flags-default"><span class="flag flag-vegan-2">Vegan</span><span class="flag flag-eco">Eco</span><span class="flag flag-doprava">Doprava ZDARMA nad 39,90 €</span></div>
            {paragraphs}</article></body></html>
            """;

        var page = new ContentExtractor(NullLogger<ContentExtractor>.Instance).Extract(new Uri("https://shop.example/karamelky"), html);

        Assert.Equal(ExtractionMethod.Readability, page.Method);
        var main = page.MainBlocks.Select(b => b.Text).ToList();
        Assert.Contains("Vegan", main);
        Assert.Contains("Eco", main);
        Assert.Contains("Doprava ZDARMA nad 39,90 €", main);
        Assert.DoesNotContain(main, t => t.Contains("Domov", StringComparison.Ordinal));
    }

    [Fact]
    public void Extract_ReadsTheProductCategoryFromTitleHeadingBreadcrumbAndStructuredData()
    {
        const string html = """
            <html><head><title>Avent Natural 240 ml | Obchod</title>
            <script type="application/ld+json">[{"@type":"Product","name":"Avent Natural 240 ml","category":{"@type":"Thing","name":"Dojčenské fľaše"}},
            {"@type":"BreadcrumbList","itemListElement":[{"@type":"ListItem","position":1,"name":"Pre bábätká"}]},
            {"@type":"ItemList","itemListElement":[{"@type":"ListItem","position":1,"name":"Iný produkt"}]}]</script></head>
            <body><div class="breadcrumbs" itemscope itemtype="https://schema.org/BreadcrumbList"><a href="/">Domov</a> / <a href="/deti">Detský svet</a> / <span>Avent Natural 240 ml</span></div>
            <meta itemprop="category" content="Domov &gt; Kŕmenie">
            <main><h1>Avent Natural 240 ml</h1><p>Fľaša s cumlíkom pre novorodencov.</p></main></body></html>
            """;

        var page = new ContentExtractor(NullLogger<ContentExtractor>.Instance).Extract(new Uri("https://shop.example/avent"), html);

        Assert.Equal("Avent Natural 240 ml | Obchod | Avent Natural 240 ml | Domov / Detský svet / Avent Natural 240 ml | Domov > Kŕmenie | Dojčenské fľaše | Pre bábätká", page.Category);
        Assert.DoesNotContain("Iný produkt", page.Category, StringComparison.Ordinal);
        Assert.DoesNotContain(page.MainBlocks, b => b.Text.Contains("Detský svet", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("""<div itemscope itemtype="http://schema.org/Product"><h1 itemprop="name">Acerola</h1><div itemprop="offers" itemscope itemtype="http://schema.org/Offer"></div></div>""", true)]
    [InlineData("""<div itemscope itemtype="https://schema.org/Product"><div itemscope itemtype="https://schema.org/Product">varianta</div></div>""", true)]
    [InlineData("""<ul><li itemscope itemtype="http://schema.org/Product">A</li><li itemscope itemtype="http://schema.org/Product">B</li></ul>""", false)]
    [InlineData("""<div itemscope itemtype="http://schema.org/ProductGroup">A</div>""", false)]
    [InlineData("""<div itemscope itemtype="http://schema.org/WebPage">A</div>""", false)]
    public void Extract_FindsOneProductInMicrodata_ButNotAListing(string body, bool product)
    {
        // goodie.sk (2. 10. 2026): product pages only in microdata, og:type=article, no JSON-LD.
        var html = $"<html><head><meta property=\"og:type\" content=\"article\"></head><body><main>{body}<p>Popis produktu.</p></main></body></html>";

        var page = new ContentExtractor(NullLogger<ContentExtractor>.Instance).Extract(new Uri("https://shop.example/p/acerola"), html);
        var type = new PageClassifier(Microsoft.Extensions.Options.Options.Create(new EshopGuardOptions())).Classify(new Uri("https://shop.example/p/acerola"), page, isHome: false);

        Assert.Equal(product, page.HasProductMicrodata);
        Assert.Equal(product ? PageType.Product : PageType.Content, type);
    }

    [Fact]
    public void Classifier_RecognizesLegalProductHomeAndContentPages()
    {
        var classifier = new PageClassifier(Microsoft.Extensions.Options.Options.Create(new EshopGuardOptions()));
        var empty = new ExtractedPage();

        Assert.Equal(PageType.Legal, classifier.Classify(new Uri("https://shop.example/obchodni-podminky/"), empty, isHome: false));
        Assert.Equal(PageType.Legal, classifier.Classify(new Uri("https://shop.example/obchodne-podmienky"), empty, isHome: false));
        Assert.Equal(PageType.Legal, classifier.Classify(new Uri("https://shop.example/info/123"), new ExtractedPage { Title = "Reklamační řád | Shop" }, isHome: false));
        Assert.Equal(PageType.Product, classifier.Classify(new Uri("https://shop.example/sampon"), new ExtractedPage { HasProductJsonLd = true }, isHome: false));
        Assert.Equal(PageType.Product, classifier.Classify(new Uri("https://shop.example/sampon"), new ExtractedPage { OgType = "product.item" }, isHome: false));
        Assert.Equal(PageType.Home, classifier.Classify(new Uri("https://shop.example/"), empty, isHome: true));
        Assert.Equal(PageType.Content, classifier.Classify(new Uri("https://shop.example/blog/jak-prat"), empty, isHome: false));
        Assert.True(classifier.IsLegalText("Obchodní podmínky (PDF)"));
    }
}
