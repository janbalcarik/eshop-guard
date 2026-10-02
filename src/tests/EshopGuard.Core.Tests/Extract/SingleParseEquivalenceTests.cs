using System.Text.Json;
using AngleSharp.Html.Parser;
using EshopGuard.Core.Crawl;
using EshopGuard.Core.Diagnostics;
using EshopGuard.Core.Extract;
using EshopGuard.Core.Profiles;
using Microsoft.Extensions.Logging.Abstractions;

namespace EshopGuard.Core.Tests;

/// <summary>
/// One parse of a page gives the same extraction and the same profile application as the code before change 5, which
/// parsed the page up to five times and removed nodes from the copies (task 4.3); the old code left its results as hashes.
/// </summary>
public sealed class SingleParseEquivalenceTests
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    /// <summary>A profile with skip and check regions that occur on usual e-shop pages.</summary>
    private static readonly PageProfile Profile = new()
    {
        Id = "test#1",
        Site = "test",
        Regions =
        [
            new ProfileRegion { Role = "main_description", Action = ProfileRegion.Check, Selector = "main, article, .product-detail, #content" },
            new ProfileRegion { Role = "cookie_bar", Action = ProfileRegion.Skip, Selector = "[class*=cookie], [id*=cookie]" },
            new ProfileRegion { Role = "newsletter_form", Action = ProfileRegion.Skip, Selector = "form" },
            new ProfileRegion { Role = "cart", Action = ProfileRegion.Skip, Selector = "[class*=cart], [class*=basket]" },
            new ProfileRegion { Role = "navigation", Action = ProfileRegion.Skip, Selector = "footer ul, header ul, aside" },
            new ProfileRegion { Role = "related_products", Action = ProfileRegion.Skip, Selector = "[class*=related], [class*=product-list]" },
        ],
    };

    public static TheoryData<string> FixtureSites => new() { "site", "site-sk" };

    [Theory]
    [MemberData(nameof(FixtureSites))]
    public async Task FixturePages_GiveTheSameResultAsBefore(string site)
    {
        var expected = ReadHashes(Path.Combine(BaselineScenarios.OutputBaselines, "extraction-hashes.txt"), site);

        var pages = await FixturePagesAsync(site);

        Assert.Equal(expected.Count, pages.Count);
        AssertSameAsBefore(pages, expected);
    }

    /// <summary>The same over the local recordings of real e-shops (src/snapshots and src/baselines, outside git).</summary>
    [Theory(Explicit = true)]
    [InlineData("vegis.sk")]
    [InlineData("www.naturfyt.sk")]
    [Trait("Category", "Snapshot")]
    public void SnapshotPages_GiveTheSameResultAsBefore(string snapshot)
    {
        var folder = Path.Combine(SourceRoot(), "snapshots", snapshot);
        var hashes = Path.Combine(SourceRoot(), "baselines", snapshot, "extraction-hashes.txt");
        if (!Directory.Exists(folder) || !File.Exists(hashes))
        {
            Assert.Skip($"Chybí nahrávka {folder} nebo {hashes}.");
        }

        AssertSameAsBefore(ExtractionBenchmark.ReadHtmlPages(folder), ReadHashes(hashes, snapshot));
    }

    [Fact]
    public void Extraction_ParsesThePageOnce()
    {
        var html = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "site-sk", "produkt-3.html"));
        var extractor = new ContentExtractor(NullLogger<ContentExtractor>.Instance);

        var probe = HtmlParserProbe.Start();
        var page = ParsedPage.Parse(new Uri("http://fixture.test/produkt-3.html"), html);
        var content = extractor.Extract(page.Url, page);
        ProfileMatcher.ApplyNonDestructive(page.Document, content, Profile);
        ProfileMatcher.StructureTokens(page.Document.Body!);

        Assert.Equal(1, probe.Value);
    }

    /// <summary>
    /// The extraction and the profile application of every page have the hash the code before change 5 gave
    /// (<c>extraction-hashes.txt</c>, written by it before its multi-parse methods were deleted), and leave the document whole.
    /// </summary>
    private static void AssertSameAsBefore(IReadOnlyList<(Uri Url, string Html)> pages, IReadOnlyDictionary<string, string> expected)
    {
        var extractor = new ContentExtractor(NullLogger<ContentExtractor>.Instance);
        var differences = new List<string>();
        foreach (var (url, html) in pages)
        {
            var page = ParsedPage.Parse(url, html);
            var content = extractor.Extract(url, page);
            var application = ProfileMatcher.ApplyNonDestructive(page.Document, content, Profile);
            if (expected.GetValueOrDefault(url.AbsoluteUri) != Hash(content, application))
            {
                differences.Add($"extrakce nebo profil {url}");
            }

            if (page.Document.DocumentElement.OuterHtml != new HtmlParser().ParseDocument(html).DocumentElement.OuterHtml)
            {
                differences.Add($"dokument změněn {url}");
            }
        }

        Assert.True(differences.Count == 0, $"{differences.Count} z {pages.Count} stránek se liší:\n" + string.Join('\n', differences.Take(20)));
    }

    private static Dictionary<string, string> ReadHashes(string file, string group) =>
        File.ReadLines(file).Select(l => l.Split('\t')).Where(p => p[0] == group).ToDictionary(p => p[1], p => p[2]);

    /// <summary>
    /// Technical signs of markets and language versions added by change 7 (read from the same one parse); the hashes of the
    /// code before change 5 do not have them, so they are left out of the comparison and tested on their own
    /// (<c>MarketSignalReaderTests</c>).
    /// </summary>
    private static readonly HashSet<string> AddedByChange7 =
        [nameof(ExtractedPage.HtmlLang), nameof(ExtractedPage.Alternates), nameof(ExtractedPage.Currencies), nameof(ExtractedPage.ProductIds),
         nameof(ExtractedPage.PhoneNumbers), nameof(ExtractedPage.FooterLinks), nameof(ExtractedPage.ScriptSources), nameof(ExtractedPage.ScriptSwitchElements)];

    private static readonly JsonSerializerOptions WithoutChange7 = new()
    {
        WriteIndented = false,
        TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver
        {
            Modifiers =
            {
                info =>
                {
                    if (info.Type == typeof(ExtractedPage))
                    {
                        foreach (var property in info.Properties.Where(p => AddedByChange7.Contains(p.Name)).ToList())
                        {
                            info.Properties.Remove(property);
                        }
                    }
                },
            },
        },
    };

    private static string Hash(ExtractedPage content, ProfileMatcher.Application application) =>
        TextTools.Sha256(JsonSerializer.Serialize(content, WithoutChange7) + "\n" + JsonSerializer.Serialize(application, WithoutChange7));

    private static async Task<List<(Uri Url, string Html)>> FixturePagesAsync(string site)
    {
        var fetcher = new FileSystemPageFetcher(Path.Combine(AppContext.BaseDirectory, "Fixtures", site), FileSystemPageFetcher.DefaultBaseUrl);
        var pages = new List<(Uri, string)>();
        foreach (var file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures", site), "*.html").Order(StringComparer.Ordinal))
        {
            var url = new Uri(FileSystemPageFetcher.DefaultBaseUrl, Path.GetFileName(file));
            var response = await fetcher.FetchAsync(url, TestContext.Current.CancellationToken);
            pages.Add((url, HtmlDecoding.Decode(response.Body!, response.Charset)));
        }

        return pages;
    }

    private static string SourceRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "EshopGuard.sln")))
            {
                return dir.FullName;
            }
        }

        throw new DirectoryNotFoundException("EshopGuard.sln not found above the test output.");
    }
}
