using System.Globalization;
using System.Text;
using EshopGuard.Core.Crawl;

namespace EshopGuard.Jobs.Tests.Runs.Support;

/// <summary>
/// A shop on one domain with three language versions by path: Slovak (main, <c>/</c>), Czech (<c>/cz/</c>) and Polish
/// (<c>/pl/</c>, a market the product does not support). Every page names the three versions in hreflang; products are
/// marked in microdata, each version has its terms in the footer, and one product sitemap lists the products of all three.
/// Counts the requests of every version.
/// </summary>
internal sealed class TrilingualShopFetcher(Uri baseUrl, int products = 60) : IPageFetcher
{
    private static readonly (string Prefix, string Lang, string Hreflang, string Terms, string TermsTitle, string Text)[] Versions =
    [
        ("/", "sk", "sk-SK", "obchodne-podmienky", "Obchodné podmienky", "Spotrebiteľ môže odstúpiť od zmluvy bez udania dôvodu do 14 dní od prevzatia tovaru."),
        ("/cz/", "cs", "cs-CZ", "obchodni-podminky", "Obchodní podmínky", "Spotřebitel může odstoupit od smlouvy bez udání důvodu do 14 dnů od převzetí zboží."),
        ("/pl/", "pl", "pl-PL", "regulamin", "Regulamin", "Konsument może odstąpić od umowy bez podania przyczyny w terminie 14 dni od otrzymania towaru."),
    ];

    private readonly System.Collections.Concurrent.ConcurrentQueue<Uri> _fetched = new();

    /// <summary>Every address asked for.</summary>
    public IReadOnlyCollection<Uri> Fetched => _fetched;

    public Task<FetchResponse> FetchAsync(Uri url, CancellationToken ct)
    {
        _fetched.Enqueue(url);
        var path = url.AbsolutePath;
        var version = Versions.Where(v => path.StartsWith(v.Prefix, StringComparison.Ordinal)).OrderByDescending(v => v.Prefix.Length).First();
        var rest = path[version.Prefix.Length..];
        var (status, type, body) = path switch
        {
            "/robots.txt" => (200, "text/plain", $"User-agent: *\nAllow: /\n\nSitemap: {Base}/sitemap.xml\n"),
            "/sitemap.xml" => (200, "application/xml", $"""<?xml version="1.0"?><sitemapindex xmlns="http://www.sitemaps.org/schemas/sitemap/0.9"><sitemap><loc>{Base}/sitemap-products.xml</loc></sitemap></sitemapindex>"""),
            "/sitemap-products.xml" => (200, "application/xml", """<?xml version="1.0"?><urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">"""
                + string.Concat(Versions.SelectMany(v => Enumerable.Range(1, products).Select(i => $"<url><loc>{Base}{v.Prefix}p/caj-{i}</loc></url>"))) + "</urlset>"),
            _ when rest.Length == 0 => (200, "text/html", Page(version, "", $"<h1>Bylinky {version.Lang}</h1><p>{Home(version.Lang)}</p>")),
            _ when rest == version.Terms => (200, "text/html", Page(version, "terms", $"<h1>{version.TermsTitle}</h1><p>{version.Text}</p>")),
            _ when Number(rest) is { } n => (200, "text/html", Page(version, $"p/caj-{n}", Product(version.Lang, n))),
            _ => (404, "text/html", "<html><body>404</body></html>"),
        };
        return Task.FromResult(new FetchResponse { Url = url, StatusCode = status, MediaType = type, Charset = "utf-8", Body = Encoding.UTF8.GetBytes(body) });
    }

    private string Base => baseUrl.GetLeftPart(UriPartial.Authority);

    private int? Number(string rest) =>
        rest.StartsWith("p/caj-", StringComparison.Ordinal) && int.TryParse(rest[6..], NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n >= 1 && n <= products ? n : null;

    /// <summary>The footer of a version (pages need some visible text, <c>crawl.min_page_text_chars</c>).</summary>
    private static string About(string lang) => lang switch
    {
        "sk" => "Bylinky balíme do papierových vrecúšok a posielame do dvoch pracovných dní kuriérom alebo na výdajné miesto. Pri objednávke nad 40 € je doprava zdarma, inak stojí 3,90 €.",
        "cs" => "Bylinky balíme do papírových sáčků a posíláme do dvou pracovních dnů kurýrem nebo na výdejní místo. Při objednávce nad 1 000 Kč je doprava zdarma, jinak stojí 99 Kč.",
        _ => "Zioła pakujemy do papierowych torebek i wysyłamy w ciągu dwóch dni roboczych kurierem lub do punktu odbioru. Przy zamówieniu powyżej 200 zł dostawa jest bezpłatna.",
    };

    private static string Home(string lang) => lang switch
    {
        "sk" => "Predávame bylinkové čaje, ktoré zbierame ručne na horských lúkach a sušíme v tieni.",
        "cs" => "Prodáváme bylinné čaje, které sbíráme ručně na horských loukách a sušíme ve stínu.",
        _ => "Sprzedajemy herbaty ziołowe, które zbieramy ręcznie na górskich łąkach i suszymy w cieniu.",
    };

    private static string Product(string lang, int n)
    {
        var description = lang switch
        {
            "sk" => $"Čaj číslo {n} pochádza z horských lúk. Lístky zbierame ručne počas leta. Balenie číslo {n} vystačí na mesiac.",
            "cs" => $"Čaj číslo {n} pochází z horských luk. Lístky sbíráme ručně během léta. Balení číslo {n} vystačí na měsíc.",
            _ => $"Herbata numer {n} pochodzi z górskich łąk. Liście zbieramy ręcznie latem. Opakowanie numer {n} wystarcza na miesiąc.",
        };
        return $"""<div itemscope itemtype="http://schema.org/Product"><h1 itemprop="name">Čaj {n}</h1><p>{description}</p><p>Cena: {n},90 €</p></div>""";
    }

    /// <summary>A page of a version with the hreflang of its counterparts (<paramref name="key"/>: <c>""</c> home, <c>terms</c>, a product path).</summary>
    private string Page((string Prefix, string Lang, string Hreflang, string Terms, string TermsTitle, string Text) version, string key, string main)
    {
        var alternates = string.Concat(Versions.Select(v => $"""<link rel="alternate" hreflang="{v.Hreflang}" href="{Base}{v.Prefix}{(key == "terms" ? v.Terms : key)}">"""));
        var switcher = string.Concat(Versions.Select(v => $"""<a href="{v.Prefix}" hreflang="{v.Lang}">{v.Lang.ToUpperInvariant()}</a> """));
        return $"""
            <!DOCTYPE html><html lang="{version.Lang}"><head><meta charset="utf-8"><title>Bylinky</title>{alternates}</head>
            <body><header><a href="{version.Prefix}">Bylinky</a> {switcher}</header><main>{main}</main>
            <footer><p>{About(version.Lang)}</p><a href="{version.Prefix}{version.Terms}">{version.TermsTitle}</a></footer></body></html>
            """;
    }
}
