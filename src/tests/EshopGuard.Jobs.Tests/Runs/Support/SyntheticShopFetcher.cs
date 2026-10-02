using System.Globalization;
using System.Text;
using EshopGuard.Core.Crawl;

namespace EshopGuard.Jobs.Tests.Runs.Support;

/// <summary>
/// A generated Slovak e-shop of any size: a home page, <c>products</c> product pages (each with its own texts, a price and,
/// optionally, a sentence shared by all of them), the legal pages in the footer, robots.txt and a sitemap of the products.
/// Answers like a fixture (no network); counts requests.
/// </summary>
internal sealed class SyntheticShopFetcher(Uri baseUrl, int products, string? sharedSentence = null) : IPageFetcher
{
    private static readonly string[] Words =
    [
        "bylinkový", "čaj", "šampón", "mydlo", "krém", "olej", "sviečka", "kanvica", "pohár", "uterák", "taška", "kefa", "hrebeň",
        "levanduľa", "mäta", "rumanček", "nechtík", "harmanček", "citrón", "pomaranč", "škorica", "vanilka", "med", "vosk",
    ];

    private int _requests;

    public int Requests => Volatile.Read(ref _requests);

    public Task<FetchResponse> FetchAsync(Uri url, CancellationToken ct)
    {
        Interlocked.Increment(ref _requests);
        var path = url.AbsolutePath;
        var (status, type, body) = path switch
        {
            "/robots.txt" => (200, "text/plain", $"User-agent: *\nDisallow: /kosik\n\nSitemap: {Base}/sitemap.xml\n"),
            "/sitemap.xml" => (200, "application/xml", Sitemap()),
            "/" => (200, "text/html", Page("Bylinková dielňa", Home())),
            "/obchodne-podmienky.html" => (200, "text/html", Page("Obchodné podmienky", Terms())),
            "/reklamacny-poriadok.html" => (200, "text/html", Page("Reklamačný poriadok",
                "<p>Reklamáciu môžete uplatniť do 24 mesiacov od prevzatia tovaru.</p><p>Reklamáciu vybavíme do 30 dní.</p>")),
            _ when Product(path) is { } n => (200, "text/html", Page(Name(n), ProductBody(n))),
            _ => (404, "text/html", "<html><body>Nenájdené</body></html>"),
        };
        return Task.FromResult(new FetchResponse
        {
            Url = url,
            StatusCode = status,
            MediaType = type,
            Charset = "utf-8",
            Body = Encoding.UTF8.GetBytes(body),
        });
    }

    private string Base => baseUrl.GetLeftPart(UriPartial.Authority);

    private int? Product(string path) =>
        path.StartsWith("/p-", StringComparison.Ordinal) && path.EndsWith(".html", StringComparison.Ordinal)
        && int.TryParse(path[3..^5], NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n >= 1 && n <= products ? n : null;

    private static string Name(int n) => $"{Cap(Words[n % Words.Length])} {Words[(n / Words.Length) % Words.Length]} č. {n}";

    private static string Cap(string word) => char.ToUpperInvariant(word[0]) + word[1..];

    private string Sitemap()
    {
        var text = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\n");
        text.Append(CultureInfo.InvariantCulture, $"  <url><loc>{Base}/</loc></url>\n");
        for (var n = 1; n <= products; n++)
        {
            text.Append(CultureInfo.InvariantCulture, $"  <url><loc>{Base}/p-{n}.html</loc></url>\n");
        }

        return text.Append("</urlset>\n").ToString();
    }

    private string Home()
    {
        var links = new StringBuilder("<p>Ručne vyrábaná kozmetika a doplnky do domácnosti z malej dielne.</p><ul>");
        for (var n = 1; n <= Math.Min(products, 20); n++)
        {
            links.Append(CultureInfo.InvariantCulture, $"<li><a href=\"/p-{n}.html\">{Name(n)}</a></li>");
        }

        return links.Append("</ul>").ToString();
    }

    private static string Terms() =>
        "<h2>Odstúpenie od zmluvy</h2><p>Spotrebiteľ môže odstúpiť od zmluvy bez udania dôvodu do 14 dní od prevzatia tovaru.</p>"
        + "<p>Predávajúci vráti platbu do 14 dní od doručenia oznámenia o odstúpení.</p>"
        + "<h2>Doprava</h2><p>Tovar doručujeme kuriérom do troch pracovných dní.</p>";

    private string ProductBody(int n)
    {
        var a = Words[n % Words.Length];
        var b = Words[(n * 7) % Words.Length];
        var c = Words[(n * 13) % Words.Length];
        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture, $"<p>Výrobok číslo {n} obsahuje {a} a {b}, balenie má {100 + n} ml.</p>");
        text.Append(CultureInfo.InvariantCulture, $"<p>Odporúčame ho používať ráno aj večer, pri izbovej teplote vydrží {n % 12 + 3} mesiacov.</p>");
        text.Append(CultureInfo.InvariantCulture, $"<p>Vôňa pripomína {c} a hodí sa ako darček pre každého, kto má rád {a}.</p>");
        if (sharedSentence is not null)
        {
            text.Append(CultureInfo.InvariantCulture, $"<p>{sharedSentence}</p>");
        }

        text.Append(CultureInfo.InvariantCulture, $"<p class=\"price\">Cena: {5 + (n % 40)},90 €</p>");
        return text.ToString();
    }

    private static string Page(string title, string main) =>
        $$"""
        <!DOCTYPE html>
        <html lang="sk">
        <head><meta charset="utf-8"><title>{{title}} | Bylinková dielňa</title></head>
        <body>
        <header><a href="/">Bylinková dielňa</a><ul role="menubar"><li role="none"><a role="menuitem" href="/">Úvod</a></li></ul></header>
        <main><h1>{{title}}</h1>{{main}}</main>
        <footer><p>Rodinná dielňa od roku 2012.</p>
        <p><a href="/obchodne-podmienky.html">Obchodné podmienky</a> · <a href="/reklamacny-poriadok.html">Reklamačný poriadok</a></p></footer>
        </body>
        </html>
        """;
}
