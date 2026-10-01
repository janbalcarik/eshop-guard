using System.Text.Json;
using AngleSharp.Html.Parser;
using EshopGuard.Core.Extract;
using EshopGuard.Core.Options;
using EshopGuard.Core.Report;
using Microsoft.Extensions.DependencyInjection;

namespace EshopGuard.Core.Tests;

/// <summary>
/// Pages whose text the site renders with JavaScript are reported as not loaded, never as pages without findings.
/// </summary>
public class TextNotLoadedTests
{
    private const string Paragraphs =
        "<p>Ručne liata sviečka zo sójového vosku s vôňou levandule a bergamotu. Sviečku zapaľujte na rovnom povrchu.</p>"
        + "<p>Knôt je bavlnený, dóza je sklenená a dá sa znova použiť. Sviečku nenechávajte horieť bez dozoru.</p>"
        + "<p>Balíky odosielame do dvoch pracovných dní. Pri objednávke nad 40 € je doprava zadarmo.</p>";

    [Theory]
    [InlineData("<div id=\"__next\"></div><script id=\"__NEXT_DATA__\" type=\"application/json\">{\"props\":{}}</script>", "Next.js")]
    [InlineData("<noscript>You need to enable JavaScript to run this app.</noscript><div id=\"root\"></div>", RenderCheck.NoscriptMessage)]
    [InlineData("<div id=\"app\"></div><script>window.__INITIAL_STATE__={\"cart\":[]}</script>", RenderCheck.AppState)]
    [InlineData("<app-root></app-root><script src=\"/main.js\"></script>", "Angular")]
    [InlineData("<div id=\"root\"></div><script src=\"/bundle.js\"></script>", RenderCheck.EmptyAppRoot)]
    public void Inspect_RecognizesClientRenderedShells(string body, string expectedApp)
    {
        var result = Inspect($"<html><head><title>Sviečka</title></head><body>{body}</body></html>");

        Assert.True(result.VisibleChars < 60, $"{result.VisibleChars} characters");
        Assert.Equal(expectedApp, result.ScriptApp);
    }

    [Fact]
    public void Inspect_CountsServerRenderedTextEvenWithFrameworkMarkers()
    {
        var result = Inspect($"<html><body><div id=\"__next\">{Paragraphs}</div><script id=\"__NEXT_DATA__\" type=\"application/json\">{{}}</script></body></html>");

        Assert.True(result.VisibleChars >= 200, $"{result.VisibleChars} characters");
        Assert.Equal("Next.js", result.ScriptApp);
    }

    [Fact]
    public void Inspect_LeavesScriptsStylesAndTrackingOutOfTheCount()
    {
        var script = new string('x', 5000);
        var result = Inspect("<html><body><style>p{color:red}</style><script>var a='" + script + "';</script>"
            + "<noscript><iframe src=\"https://www.googletagmanager.com/ns.html?id=GTM-1\"></iframe></noscript>"
            + "<template><p>Šablona</p></template><p>Krátky text.</p></body></html>");

        Assert.Equal("Krátky text.".Length, result.VisibleChars);
        Assert.Null(result.ScriptApp);
    }

    [Fact]
    public async Task Scan_ReportsPagesWhoseTextWasNotLoaded()
    {
        var root = CreateSite(("index.html", Page("Úvod", Paragraphs)), ("obchodne-podmienky.html", Page("Obchodné podmienky", Paragraphs)),
            ("sviecka.html", Shell("Sviečka Vodnár")));
        try
        {
            var fetcher = new FileSystemPageFetcher(root, FileSystemPageFetcher.DefaultBaseUrl);
            await using var provider = TestServices.Create(fetcher);
            var result = await provider.GetRequiredService<IEshopGuard>()
                .ScanSiteAsync(FileSystemPageFetcher.DefaultBaseUrl, new ScanOptions { Country = "sk" }, ct: TestContext.Current.CancellationToken);

            var shell = Assert.Single(result.Pages, p => p.TextNotLoaded);
            Assert.EndsWith("/sviecka.html", shell.Url, StringComparison.Ordinal);
            Assert.Equal("Next.js", shell.ScriptApp);
            Assert.Equal(1, result.Stats.PagesTextNotLoaded);
            var warning = Assert.Single(TestTexts.Warnings(result.Warnings), w => w.Contains("skoro žádný čitelný text", StringComparison.Ordinal));
            Assert.StartsWith("1 z 3 stažených stránek", warning, StringComparison.Ordinal);
            Assert.DoesNotContain(TestTexts.Warnings(result.Warnings), w => w.Contains("z velké části", StringComparison.Ordinal));

            var report = MarkdownReportWriter.Render(result, TestTexts.Renderer, "cs");
            Assert.Contains("**Pozor: text 1 z 3 stránek se nenačetl**", report);
            Assert.Contains($"  - {shell.Url} (", report);
            Assert.Contains("aplikace Next.js", report);

            var output = Directory.CreateTempSubdirectory("EshopGuard-").FullName;
            try
            {
                await new PagesJsonlWriter().WriteAsync(result, output, TestContext.Current.CancellationToken);
                var lines = await File.ReadAllLinesAsync(Path.Combine(output, "pages.jsonl"), TestContext.Current.CancellationToken);
                using var json = JsonDocument.Parse(lines.Single(l => l.Contains("sviecka", StringComparison.Ordinal)));
                Assert.True(json.RootElement.GetProperty("text_not_loaded").GetBoolean());
                Assert.Equal("Next.js", json.RootElement.GetProperty("script_app").GetString());
            }
            finally
            {
                Directory.Delete(output, recursive: true);
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Scan_SaysTheResultIsIncompleteWhenMostPagesAreNotLoaded()
    {
        var root = CreateSite(("index.html", Shell("Úvod")), ("obchodne-podmienky.html", Page("Obchodné podmienky", Paragraphs)),
            ("sviecka.html", Shell("Sviečka Vodnár")));
        try
        {
            var fetcher = new FileSystemPageFetcher(root, FileSystemPageFetcher.DefaultBaseUrl);
            await using var provider = TestServices.Create(fetcher);
            var result = await provider.GetRequiredService<IEshopGuard>()
                .ScanSiteAsync(FileSystemPageFetcher.DefaultBaseUrl, new ScanOptions { Country = "sk" }, ct: TestContext.Current.CancellationToken);

            Assert.Equal(2, result.Stats.PagesTextNotLoaded);
            Assert.Contains(TestTexts.Warnings(result.Warnings), w => w.StartsWith("Web je z velké části vykreslovaný JavaScriptem", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Warnings_AreEmptyWhenEveryPageWasLoaded() =>
        Assert.Empty(Pipeline.RulesStep.NotLoadedWarnings(0, 10));

    private static RenderCheck.Result Inspect(string html) => RenderCheck.Inspect(new HtmlParser().ParseDocument(html));

    private static string Page(string title, string body) =>
        $"<!doctype html><html lang=\"sk\"><head><meta charset=\"utf-8\"><title>{title}</title></head><body><main><h1>{title}</h1>{body}</main></body></html>";

    private static string Shell(string title) =>
        $"<!doctype html><html lang=\"sk\"><head><meta charset=\"utf-8\"><title>{title}</title></head>"
        + "<body><div id=\"__next\"></div><script id=\"__NEXT_DATA__\" type=\"application/json\">{\"props\":{}}</script>"
        + "<script src=\"/_next/static/main.js\"></script></body></html>";

    private static string CreateSite(params (string Name, string Html)[] pages)
    {
        var root = Directory.CreateTempSubdirectory("EshopGuard-js-").FullName;
        foreach (var (name, html) in pages)
        {
            File.WriteAllText(Path.Combine(root, name), html);
        }

        var urls = string.Concat(pages.Select(p => $"<url><loc>{FileSystemPageFetcher.DefaultBaseUrl}{(p.Name == "index.html" ? "" : p.Name)}</loc></url>"));
        File.WriteAllText(Path.Combine(root, "sitemap.xml"),
            $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">{urls}</urlset>");
        File.WriteAllText(Path.Combine(root, "robots.txt"), $"User-agent: *\nAllow: /\nSitemap: {FileSystemPageFetcher.DefaultBaseUrl}sitemap.xml\n");
        return root;
    }
}
