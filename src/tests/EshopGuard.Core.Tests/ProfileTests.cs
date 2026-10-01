using AngleSharp.Html.Parser;
using EshopGuard.Core.Extract;
using EshopGuard.Core.Models;
using EshopGuard.Core.Options;
using EshopGuard.Core.Profiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace EshopGuard.Core.Tests;

/// <summary>
/// Profiles of page templates: a page uses the stored profile that fits it, pages of a new template get a profile of
/// their own, and a profile only leaves interface text out of the check, never the main text.
/// </summary>
public class ProfileTests
{
    private const string Cookie = "Tento web používa súbory cookie na analýzu návštevnosti.";
    private const string NewsletterLabel = "Váš e-mail pre zasielanie noviniek";
    private const string NewsletterHeading = "Odoberajte novinky a akcie";
    private const string Company = "Bylinkovo s. r. o., Hlavná 1, 811 01 Bratislava";

    private static string Frame(string main) =>
        "<!doctype html><html lang=\"sk\"><head><meta charset=\"utf-8\"><title>Bylinkovo</title></head><body>"
        + "<header class=\"site-header\"><div class=\"contact\">Zákaznícka linka 0900 123 456, po–pi 8–16 h</div>"
        + "<nav class=\"menu\"><ul><li><a href=\"/kozmetika\">Kozmetika</a></li><li><a href=\"/caje\">Čaje</a></li><li><a href=\"/blog\">Blog</a></li></ul></nav></header>"
        + $"<div class=\"cookie-bar\">{Cookie}</div>"
        + main
        + $"<footer class=\"site-footer\"><p>{Company}</p><div class=\"newsletter\"><h3>{NewsletterHeading}</h3>"
        + $"<form><label class=\"field\">{NewsletterLabel}</label><input type=\"email\"></form></div></footer></body></html>";

    private static string Paragraphs(string subject, int count) =>
        string.Concat(Enumerable.Range(1, count).Select(i =>
            $"<p>{subject} – odsek {i}. Text popisuje použitie, zloženie a skladovanie výrobku, ako ho obchod uvádza zákazníkom na svojej stránke, a pokračuje ďalšími podrobnosťami o balení, pôvode surovín a odporúčanom dávkovaní pre dospelých.</p>"));

    private static string Product(int n) => Frame(
        $"<main><div class=\"product-detail\"><h1>Krém s ceramidmi {n}</h1><div class=\"flags\"><span class=\"flag\">Eco</span></div>"
        + $"<div class=\"description\">{Paragraphs($"Krém {n} hydratuje pleť", 6)}</div></div>"
        + "<div class=\"related\">" + string.Concat(Enumerable.Range(1, 3).Select(i => $"<div class=\"tile\"><a href=\"/p{i + 10}\">Mydlo {i}</a> 4,90 €</div>")) + "</div></main>");

    private static string Post(int n) => Frame(
        $"<main><article class=\"post\"><h1>Článok o bylinkách {n}</h1><div class=\"post-body\">{Paragraphs($"Bylinka {n} sa zbiera v lete", 6)}</div></article>"
        + "<aside class=\"post-sidebar\"><p>Autorka píše o bylinkách už desať rokov.</p><div class=\"share-buttons\">Zdieľať na Facebooku</div></aside></main>");

    private static readonly string Home = Frame("<main class=\"home\"><div class=\"banner\"><h1>Prírodná kozmetika z overených zdrojov</h1>"
        + "<p>Vyberáme kozmetiku a čaje od malých výrobcov zo Slovenska a Česka, ktorým rozumieme a ktorých výrobky sami používame.</p></div></main>");

    [Fact]
    public void Outline_KeepsStructureAndCutsLongRepeats()
    {
        var html = "<html><head><title>x</title><script>var a = 1;</script></head><body><ul class=\"menu\">"
            + string.Concat(Enumerable.Range(1, 10).Select(i => $"<li class=\"menu-item-{i}\"><a href=\"https://shop.example/c{i}\">Kategória {i}</a></li>"))
            + "</ul><p>" + new string('x', 200) + "</p></body></html>";

        var outline = PageOutline.Build(new HtmlParser().ParseDocument(html), 10_000);

        Assert.DoesNotContain("var a", outline, StringComparison.Ordinal);
        Assert.Contains("href=\"/c1\"", outline, StringComparison.Ordinal);
        Assert.Contains("Kategória 3", outline, StringComparison.Ordinal);
        Assert.DoesNotContain("Kategória 4", outline, StringComparison.Ordinal);
        Assert.Contains("more similar siblings follow", outline, StringComparison.Ordinal);
        Assert.Contains(new string('x', 70) + "…", outline, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownShare_IsZeroForTheOwnTemplateAndHighForAnother()
    {
        var profile = ProfileFor(products: true, posts: false, home: false);
        var product = new HtmlParser().ParseDocument(Product(1)).Body!;
        var post = new HtmlParser().ParseDocument(Post(1)).Body!;

        Assert.Equal(0, ProfileMatcher.UnknownShare(product, profile), 3);
        Assert.True(ProfileMatcher.UnknownShare(post, profile) > 0.5);
        Assert.True(ProfileMatcher.Similarity(ProfileMatcher.StructureTokens(product), ProfileMatcher.StructureTokens(new HtmlParser().ParseDocument(Product(2)).Body!)) > 0.9);
        Assert.True(ProfileMatcher.Similarity(ProfileMatcher.StructureTokens(product), ProfileMatcher.StructureTokens(post)) < 0.75);
    }

    [Fact]
    public void Apply_LeavesOutInterfaceTextButNeverTheMainText()
    {
        var html = Product(1);
        var content = Extract(html);
        var application = ProfileMatcher.ApplyNonDestructive(new HtmlParser().ParseDocument(html), content, ProfileFor(products: true, posts: false, home: false));

        var skipped = application.Content.ProfileSkippedBlocks.Select(b => b.Text).ToList();
        var remaining = Remaining(application.Content);
        Assert.Contains(Cookie, skipped);
        Assert.Contains(NewsletterLabel, skipped);
        Assert.DoesNotContain(Cookie, remaining);
        Assert.Contains(NewsletterHeading, remaining);
        Assert.Contains(remaining, t => t.Contains(Company, StringComparison.Ordinal));
        Assert.Equal(content.MainBlocks, application.Content.MainBlocks);
        Assert.Empty(application.Blocked);
    }

    [Fact]
    public void Apply_DoesNotUseASkipRegionThatContainsCheckedText()
    {
        var html = Product(1);
        var content = Extract(html);
        var wrong = new PageProfile
        {
            Id = "t#1",
            Site = "t",
            Regions =
            [
                Region("product_box", ProfileRegion.Check, ".product-detail"),
                // A selector that matches something else on this page: the whole main element with the product inside.
                Region("product_listing", ProfileRegion.Skip, "main"),
                Region("cookie_bar", ProfileRegion.Skip, ".cookie-bar"),
            ],
        };

        var application = ProfileMatcher.ApplyNonDestructive(new HtmlParser().ParseDocument(html), content, wrong);

        Assert.Contains(application.Blocked, b => b.Selector == "main" && b.Reason == ProfileMatcher.BlockReason.ContainsCheckedRegion);
        Assert.Contains(Cookie, application.Content.ProfileSkippedBlocks.Select(b => b.Text));
        Assert.Contains(Remaining(application.Content).Concat(content.MainBlocks.Select(b => b.Text)), t => t == "Eco");
    }

    [Fact]
    public void Apply_DoesNotUseASkipRegionWithTheMainText()
    {
        var html = Post(1);
        var content = Extract(html);
        var wrong = new PageProfile
        {
            Id = "t#1",
            Site = "t",
            Regions = [Region("related_products", ProfileRegion.Skip, "article")],
        };

        var application = ProfileMatcher.ApplyNonDestructive(new HtmlParser().ParseDocument(html), content, wrong);

        Assert.Equal(ProfileMatcher.BlockReason.ContainsMainText, Assert.Single(application.Blocked).Reason);
        Assert.Empty(application.Content.ProfileSkippedBlocks);
    }

    [Fact]
    public async Task Scan_WritesAProfileStoresItAndAddsOneForANewTemplate()
    {
        var root = Directory.CreateTempSubdirectory("EshopGuard-profiles-").FullName;
        try
        {
            var model = new FakeProfileModel();
            var cache = Path.Combine(root, "cache.sqlite");
            WriteSite(root, products: 3, posts: 0);

            var first = await ScanAsync(root, model, cache);

            Assert.Equal(1, first.Stats.ProfilesCreated);
            var sample = Assert.Single(model.Requests);
            Assert.Equal(3, sample.Count);
            Assert.All(first.Pages, p => Assert.Equal("fixture.test#1", p.ProfileId));
            // The fallback heuristic reads the home page with the cookie bar as main text, and main text is never left out.
            Assert.Equal(ExtractionMethod.Fallback, first.Pages.Single(p => p.Type == PageType.Home).Extraction);
            Assert.All(first.Segments.Where(s => s.Text == Cookie), s => Assert.Equal(["http://fixture.test/"], s.Urls));
            Assert.DoesNotContain(first.Segments, s => s.Text == NewsletterLabel);
            Assert.Contains(first.Segments, s => s.Text == NewsletterHeading);
            Assert.Contains(first.Segments, s => s.Text == "Eco");
            Assert.Contains(first.Pages, p => p.ProfileSkippedText.Split('\n').Contains(Cookie));
            Assert.Contains(first.Profiles, u => u.Profile.Id == "fixture.test#1" && u.CreatedInThisScan && u.SkippedCharsByRole.ContainsKey("cookie_bar"));

            // A blog section appears: the products keep their stored profile, the posts get a profile of their own.
            WriteSite(root, products: 3, posts: 3);
            var second = await ScanAsync(root, model, cache);

            Assert.Equal(2, model.Requests.Count);
            Assert.All(model.Requests[1], s => Assert.Contains("/clanok", s.Url, StringComparison.Ordinal));
            Assert.Equal(1, second.Stats.ProfilesCreated);
            Assert.Equal(2, second.Stats.ProfilesUsed);
            Assert.All(second.Pages.Where(p => p.Url.Contains("/produkt", StringComparison.Ordinal)), p => Assert.Equal("fixture.test#1", p.ProfileId));
            Assert.All(second.Pages.Where(p => p.Url.Contains("/clanok", StringComparison.Ordinal)), p => Assert.Equal("fixture.test#2", p.ProfileId));
            Assert.DoesNotContain(second.Segments, s => s.Text == "Zdieľať na Facebooku");
            Assert.Contains(second.Segments, s => s.Text == "Autorka píše o bylinkách už desať rokov.");

            // Everything is stored: a third scan writes nothing.
            var third = await ScanAsync(root, model, cache);
            Assert.Equal(2, model.Requests.Count);
            Assert.Equal(0, third.Stats.ProfilesCreated);
            Assert.Equal(0, third.Stats.PagesWithoutProfile);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Scan_WithoutConfirmation_CallsNoModel()
    {
        var root = Directory.CreateTempSubdirectory("EshopGuard-profiles-").FullName;
        try
        {
            var model = new FakeProfileModel();
            WriteSite(root, products: 3, posts: 0);
            JevCallEstimate? seen = null;

            var result = await ScanAsync(root, model, Path.Combine(root, "cache.sqlite"), (estimate, _) =>
            {
                seen = estimate;
                return Task.FromResult(false);
            });

            Assert.Empty(model.Requests);
            Assert.NotNull(seen);
            Assert.Equal(1, seen.ProfileTemplates);
            Assert.True(seen.ProfilesWillRun);
            Assert.True(result.EvaluationSkipped);
            Assert.All(result.Pages, p => Assert.Null(p.ProfileId));
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Scan_WithoutModel_ChecksPagesWhole()
    {
        var root = Directory.CreateTempSubdirectory("EshopGuard-profiles-").FullName;
        try
        {
            var model = new FakeProfileModel { Unavailable = "chybí klíč OpenAI (OPENAI_API_KEY)" };
            WriteSite(root, products: 3, posts: 0);

            var result = await ScanAsync(root, model, Path.Combine(root, "cache.sqlite"));

            Assert.Empty(model.Requests);
            Assert.Equal(1, result.Stats.ProfilesPlanned);
            Assert.Equal(0, result.Stats.ProfilesCreated);
            Assert.All(result.Pages, p => Assert.Null(p.ProfileId));
            Assert.Contains(result.Segments, s => s.Text == Cookie);
            Assert.False(File.Exists(Path.Combine(root, "cache.sqlite")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static async Task<ScanResult> ScanAsync(string root, FakeProfileModel model, string cache,
        Func<JevCallEstimate, CancellationToken, Task<bool>>? confirm = null)
    {
        var fetcher = new FileSystemPageFetcher(root, FileSystemPageFetcher.DefaultBaseUrl);
        await using var provider = TestServices.Create(fetcher, o => o.Cache.Path = cache, register: s => s.AddSingleton<IProfileModel>(model));
        return await provider.GetRequiredService<IEshopGuard>().ScanSiteAsync(
            FileSystemPageFetcher.DefaultBaseUrl,
            new ScanOptions { Country = "sk", UseSieve = false, ConfirmJevCalls = confirm },
            ct: TestContext.Current.CancellationToken);
    }

    private static void WriteSite(string root, int products, int posts)
    {
        var urls = new List<string>();
        File.WriteAllText(Path.Combine(root, "index.html"), Home);
        for (var i = 1; i <= products; i++)
        {
            Directory.CreateDirectory(Path.Combine(root, "produkt"));
            File.WriteAllText(Path.Combine(root, "produkt", $"{i}.html"), Product(i));
            urls.Add($"produkt/{i}.html");
        }

        for (var i = 1; i <= posts; i++)
        {
            Directory.CreateDirectory(Path.Combine(root, "clanok"));
            File.WriteAllText(Path.Combine(root, "clanok", $"{i}.html"), Post(i));
            urls.Add($"clanok/{i}.html");
        }

        File.WriteAllText(Path.Combine(root, "robots.txt"), "User-agent: *\nAllow: /\nSitemap: {{base_url}}/sitemap.xml\n");
        File.WriteAllText(Path.Combine(root, "sitemap.xml"),
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?><urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">"
            + string.Concat(urls.Select(u => $"<url><loc>{{{{base_url}}}}/{u}</loc></url>")) + "</urlset>");
    }

    private static ExtractedPage Extract(string html) =>
        new ContentExtractor(NullLogger<ContentExtractor>.Instance).Extract(new Uri("http://fixture.test/produkt/1.html"), html);

    private static List<string> Remaining(ExtractedPage content) =>
        content.ChromeRegions.SelectMany(r => r).Concat(content.RestBlocks).Select(b => b.Text).ToList();

    private static ProfileRegion Region(string role, string action, string selector) =>
        new() { Role = role, Action = action, Selector = selector };

    /// <summary>The regions a model would return for outlines that show the given templates.</summary>
    private static PageProfile ProfileFor(bool products, bool posts, bool home) => new()
    {
        Id = "t#1",
        Site = "t",
        Regions = FakeProfileModel.Regions(products, posts, home),
    };

    /// <summary>Answers from the outlines it gets: the frame always, and the regions of every template it sees.</summary>
    private sealed class FakeProfileModel : IProfileModel
    {
        public List<IReadOnlyList<(string Url, string Outline)>> Requests { get; } = [];

        public string? Unavailable { get; init; }

        public bool IsAvailable => Unavailable is null;

        public string? UnavailableReason => Unavailable;

        public decimal EstimateUsd(IReadOnlyList<string> outlines) => 0.05m;

        public Task<ProfileAnswer> AskAsync(IReadOnlyList<(string Url, string Outline)> samples, CancellationToken ct)
        {
            Requests.Add(samples);
            bool Sees(string marker) => samples.Any(s => s.Outline.Contains(marker, StringComparison.Ordinal));
            return Task.FromResult(new ProfileAnswer
            {
                Regions = Regions(Sees("product-detail"), Sees("class=\"post\""), Sees("banner")),
                Model = "fake",
                CostUsd = 0.05m,
            });
        }

        public static List<ProfileRegion> Regions(bool products, bool posts, bool home)
        {
            var regions = new List<ProfileRegion>
            {
                Region("header", ProfileRegion.Check, ".site-header .contact"),
                Region("navigation", ProfileRegion.Skip, ".menu"),
                Region("cookie_bar", ProfileRegion.Skip, ".cookie-bar"),
                Region("footer", ProfileRegion.Check, ".site-footer"),
                Region("newsletter_form", ProfileRegion.Skip, ".newsletter form"),
            };
            if (products)
            {
                regions.Add(Region("product_box", ProfileRegion.Check, ".product-detail"));
                regions.Add(Region("related_products", ProfileRegion.Skip, ".related"));
            }

            if (posts)
            {
                regions.Add(Region("main_description", ProfileRegion.Check, ".post"));
                regions.Add(Region("other", ProfileRegion.Check, ".post-sidebar"));
                regions.Add(Region("social_share", ProfileRegion.Skip, ".share-buttons"));
            }

            if (home)
            {
                regions.Add(Region("banner", ProfileRegion.Check, ".banner"));
            }

            return regions;
        }
    }
}
