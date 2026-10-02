using System.Globalization;
using System.Text;
using EshopGuard.Core.Crawl;

namespace EshopGuard.Jobs.Tests.Runs.Support;

/// <summary>
/// A shop on two domains like goodie.sk (2. 10. 2026): the Slovak and the Czech version with the same paths, hreflang only on
/// the pages, products marked in microdata. The descriptions are translated; under every product both versions show the same
/// customer reviews in Czech. Legal pages in the footer, robots.txt and a sitemap of the products on each domain.
/// </summary>
internal sealed class BilingualShopFetcher(string skHost, string czHost, int products = 40) : IPageFetcher
{
    public Task<FetchResponse> FetchAsync(Uri url, CancellationToken ct)
    {
        var sk = url.Host == skHost;
        var host = $"{url.Scheme}://{url.Host}";
        var path = url.AbsolutePath;
        var (status, type, body) = path switch
        {
            "/robots.txt" => (200, "text/plain", $"User-agent: *\nAllow: /\n\nSitemap: {host}/sitemap.xml\n"),
            "/sitemap.xml" => (200, "application/xml", $"""<?xml version="1.0"?><sitemapindex xmlns="http://www.sitemaps.org/schemas/sitemap/0.9"><sitemap><loc>{host}/sitemap-products.xml</loc></sitemap></sitemapindex>"""),
            "/sitemap-products.xml" => (200, "application/xml", """<?xml version="1.0"?><urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">"""
                + string.Concat(Enumerable.Range(1, products).Select(i => $"<url><loc>{host}/p/caj-{i}</loc></url>")) + "</urlset>"),
            "/" => (200, "text/html", Page(sk, path, Home(sk))),
            "/obchodne-podmienky" when sk => (200, "text/html", Page(sk, path,
                "<h1>Obchodné podmienky</h1><p>Spotrebiteľ môže odstúpiť od zmluvy bez udania dôvodu do 14 dní od prevzatia tovaru.</p>")),
            "/obchodni-podminky" when !sk => (200, "text/html", Page(sk, path,
                "<h1>Obchodní podmínky</h1><p>Spotřebitel může odstoupit od smlouvy bez udání důvodu do 14 dnů od převzetí zboží.</p>")),
            _ when Number(path) is { } n => (200, "text/html", Page(sk, path, Product(sk, n))),
            _ => (404, "text/html", "<html><body>Nenájdené</body></html>"),
        };
        return Task.FromResult(new FetchResponse { Url = url, StatusCode = status, MediaType = type, Charset = "utf-8", Body = Encoding.UTF8.GetBytes(body) });
    }

    private int? Number(string path) =>
        path.StartsWith("/p/caj-", StringComparison.Ordinal) && int.TryParse(path[7..], NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n >= 1 && n <= products ? n : null;

    private static string Home(bool sk) =>
        (sk
            ? "<h1>Bylinky z horských lúk</h1><p>Predávame bylinkové čaje, ktoré zbierame ručne na horských lúkach a sušíme v tieni.</p>"
                + "<p>Každý čaj balíme do papierového vrecka a posielame do dvoch pracovných dní kuriérom alebo na výdajné miesto.</p>"
            : "<h1>Bylinky z horských luk</h1><p>Prodáváme bylinné čaje, které sbíráme ručně na horských loukách a sušíme ve stínu.</p>"
                + "<p>Každý čaj balíme do papírového sáčku a posíláme do dvou pracovních dnů kurýrem nebo na výdejní místo.</p>")
        + string.Concat(Enumerable.Range(1, 10).Select(i => $"<a href=\"/p/caj-{i}\">Čaj {i}</a> "));

    private static string Product(bool sk, int n)
    {
        var description = sk
            ? $"Čaj číslo {n} pochádza z horských lúk. Lístky zbierame ručne počas leta. Sušíme ich v tieni na drevených lieskach. "
                + $"Pripravíte ho za päť minút vo vriacej vode. Balenie číslo {n} vystačí na mesiac."
            : $"Čaj číslo {n} pochází z horských luk. Lístky sbíráme ručně během léta. Sušíme je ve stínu na dřevěných lískách. "
                + $"Připravíte ho za pět minut ve vroucí vodě. Balení číslo {n} vystačí na měsíc.";
        var reviews = $"Čaj {n} piju každý večer a chutná mi. Dorazil rychle a dobře zabalený. Objednám znovu, děkuji za čaj číslo {n}. "
            + $"Manželce chutná víc s medem. Vůně je silná a příjemná. Za tu cenu je čaj {n} výborný. Kupuji ho už třetí rok.";
        return $"""<div itemscope itemtype="http://schema.org/Product"><h1 itemprop="name">Čaj {n}</h1><div class="popis"><p>{description}</p></div>"""
            + $"""<p class="cena">Cena: {n},90 {(sk ? "€" : "Kč")}</p><div class="recenzie"><h2>Hodnocení zákazníků</h2><p>{reviews}</p></div></div>""";
    }

    private string Page(bool sk, string path, string main) => $$"""
        <!DOCTYPE html><html lang="{{(sk ? "sk" : "cs")}}"><head><meta charset="utf-8"><title>Bylinky</title>
        <link rel="alternate" hreflang="sk-SK" href="http://{{skHost}}{{path}}">
        <link rel="alternate" hreflang="cs-CZ" href="http://{{czHost}}{{path}}">
        </head><body><header><a href="/">Bylinky</a> <a href="http://{{(sk ? czHost : skHost)}}/">{{(sk ? "Česká verze" : "Slovenská verze")}}</a></header>
        <main>{{main}}</main>
        <footer><p>Bylinky s.r.o.</p><a href="{{(sk ? "/obchodne-podmienky" : "/obchodni-podminky")}}">{{(sk ? "Obchodné podmienky" : "Obchodní podmínky")}}</a></footer></body></html>
        """;
}
