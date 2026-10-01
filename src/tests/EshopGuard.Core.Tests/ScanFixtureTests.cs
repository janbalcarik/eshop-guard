using System.Text.Json;
using EshopGuard.Core.Crawl;
using EshopGuard.Core.Models;
using EshopGuard.Core.Options;
using EshopGuard.Core.Report;
using Microsoft.Extensions.DependencyInjection;

namespace EshopGuard.Core.Tests;

/// <summary>
/// Scans the fixture e-shop without network and checks crawling and segmentation (milestone M1).
/// </summary>
public class ScanFixtureTests
{
    [Fact]
    public async Task Scan_FindsAllPagesExceptCart()
    {
        var fetcher = FileSystemPageFetcher.ForFixture();
        var result = await ScanAsync(fetcher);
        var expected = ExpectedFindings.Load();

        var fetched = result.Pages.ToDictionary(p => new Uri(p.Url).AbsolutePath, p => TextTools.Snake(p.Type));
        Assert.Equal(expected.FetchedPages.OrderBy(p => p.Key), fetched.OrderBy(p => p.Key));
        Assert.DoesNotContain(fetcher.Requested, u => u.AbsolutePath.StartsWith("/kosik", StringComparison.Ordinal));
        Assert.Equal(expected.NotFetched, result.RobotsBlockedUrls.Select(u => new Uri(u).AbsolutePath));
        Assert.Equal(0, result.Stats.PagesFailed);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task Scan_MarksFooterAsBoilerplateEvaluatedOnce()
    {
        var result = await ScanAsync(FileSystemPageFetcher.ForFixture());
        var expected = ExpectedFindings.Load();

        var footer = Assert.Single(result.Segments, s => s.Text == expected.BoilerplateText);
        Assert.True(footer.Boilerplate);
        Assert.Equal(result.Pages.Count, footer.Occurrences);
        Assert.Equal([SegmentSource.Chrome], footer.Sources);
        Assert.DoesNotContain(result.Segments, s => s.Boilerplate && s.Sources.Contains(SegmentSource.Main));
    }

    [Fact]
    public async Task Scan_BuildsSentencesWithContextFromAllPageTypes()
    {
        var result = await ScanAsync(FileSystemPageFetcher.ForFixture());
        var expected = ExpectedFindings.Load();

        foreach (var finding in expected.Findings)
        {
            var segment = Assert.Single(result.Segments, s => s.Kind == SegmentKind.Sentence && s.Text == finding.Text);
            Assert.Contains(segment.Urls, u => new Uri(u).AbsolutePath == finding.Path);
        }

        var claim = Assert.Single(result.Segments, s => s.Text == "Tento šampon je ekologický a šetrný k přírodě.");
        Assert.StartsWith("Balení obsahuje 250 ml a vydrží přibližně 2 měsíce.", claim.ContextAfter);

        // The eco module also reads legal pages, and titles and JSON-LD descriptions are segmented too.
        Assert.Contains(result.Segments, s => s.Kind == SegmentKind.Sentence && s.PageTypes.Contains(PageType.Legal));
        Assert.Contains(result.Segments, s => s.Sources.Contains(SegmentSource.Title));
        Assert.Contains(result.Segments, s => s.Sources.Contains(SegmentSource.JsonLd));
        Assert.Contains(result.Segments, s => s.Text == "BIO jablečný mošt z certifikovaného ekologického zemědělství.");
    }

    [Fact]
    public async Task Scan_SplitsLegalPagesIntoParagraphsWithHeadings()
    {
        var result = await ScanAsync(FileSystemPageFetcher.ForFixture());

        var paragraphs = result.Segments.Where(s => s.Kind == SegmentKind.LegalParagraph).ToList();
        Assert.Contains(paragraphs, p => p.Text.StartsWith("3. Odstoupení od smlouvy\n", StringComparison.Ordinal) && p.Text.Contains("do 14 dnů"));
        Assert.Contains(paragraphs, p => p.Text.StartsWith("Lhůty\n", StringComparison.Ordinal));
        Assert.All(paragraphs, p => Assert.Equal([PageType.Legal], p.PageTypes));
        Assert.All(paragraphs, p => Assert.True(p.Text.Length <= 1500));
    }

    [Fact]
    public async Task Scan_RespectsPageAndProductLimitsButAlwaysTakesLegalPages()
    {
        var result = await ScanAsync(FileSystemPageFetcher.ForFixture(), new ScanOptions { Country = "cz", MaxPages = 5, SampleProducts = 2 });

        Assert.Equal(5, result.Pages.Count);
        Assert.Equal(PageType.Home, result.Pages[0].Type);
        Assert.Equal(2, result.Pages.Count(p => p.Type == PageType.Legal));
        Assert.Equal(2, result.Pages.Count(p => p.Type == PageType.Product && p.IncludedInAnalysis));
        Assert.True(result.Stats.PagesOverLimit > 0);
    }

    [Fact]
    public async Task Scan_FollowsLinksWhenSitemapAndRobotsAreMissing()
    {
        var fetcher = new MissingFilesFetcher(FileSystemPageFetcher.ForFixture(), "/sitemap.xml", "/robots.txt");

        var result = await ScanAsync(fetcher);

        Assert.Equal(ExpectedFindings.Load().FetchedPages.Count, result.Pages.Count);
        Assert.DoesNotContain(result.Pages, p => p.Url.Contains("kosik", StringComparison.Ordinal));
        Assert.True(result.Stats.PagesExcludedByFilter >= 1);
    }

    [Fact]
    public async Task Scan_WritesSegmentsCsv()
    {
        var result = await ScanAsync(FileSystemPageFetcher.ForFixture());
        var directory = Directory.CreateTempSubdirectory("EshopGuard-").FullName;
        try
        {
            await new SegmentsCsvWriter().WriteAsync(result, directory, TestContext.Current.CancellationToken);

            var lines = File.ReadAllLines(Path.Combine(directory, "segments.csv"));
            Assert.StartsWith("segment_hash,kind,text,context_before,context_after,sources,page_types,boilerplate,occurrences,urls,", lines[0]);
            // The footer sentence is on every fetched page.
            var pages = ExpectedFindings.Load().FetchedPages.Count;
            Assert.Contains(lines, l => l.Contains("Rodinný e-shop od roku 2010.", StringComparison.Ordinal) && l.Contains(",chrome,", StringComparison.Ordinal) && l.Contains($",true,{pages},", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Scan_InMockModeFindsPlantedFindingsAndWritesAllOutputs()
    {
        var fetcher = FileSystemPageFetcher.ForFixture();
        await using var provider = TestServices.Create(fetcher);
        var result = await provider.GetRequiredService<IEshopGuard>()
            .ScanSiteAsync(FileSystemPageFetcher.DefaultBaseUrl, new ScanOptions { Country = "cz" }, ct: TestContext.Current.CancellationToken);
        var expected = ExpectedFindings.Load();

        // The mock only proves the pipeline: planted texts reach the rules and clean pages stay clean.
        foreach (var planted in expected.Findings)
        {
            Assert.Contains(result.Findings, f => f.RuleId == planted.RuleId && f.Text == planted.Text
                && f.Urls.Any(u => new Uri(u).AbsolutePath == planted.Path));
        }

        Assert.Equal(expected.SiteFindings.Order(), result.Findings.Where(f => f.Scope == "site").Select(f => f.RuleId).Order());
        foreach (var clean in expected.PagesWithoutFindings)
        {
            Assert.DoesNotContain(result.Findings, f => f.Scope == "segment" && f.Urls.Any(u => new Uri(u).AbsolutePath == clean));
        }

        Assert.Equal("mock", result.JevModel);
        // In Czechia only what ČOI fines and mandatory information: environmental claims and durability are Slovak only.
        Assert.Equal(["ucp", "legal"], result.RuleSets.Select(r => r.Module));
        Assert.True(result.Stats.JevCalls > 0);
        Assert.Contains(result.ImagesForReview, i => i.FileName == "mydlo-eco-obal.jpg");

        var directory = Directory.CreateTempSubdirectory("EshopGuard-").FullName;
        try
        {
            foreach (var writer in provider.GetServices<IReportWriter>())
            {
                await writer.WriteAsync(result, directory, TestContext.Current.CancellationToken);
            }

            Assert.Equal(["findings.csv", "findings.json", "pages.jsonl", "report.md", "segments.csv", "sieve.csv"], Directory.GetFiles(directory).Select(Path.GetFileName).Order());

            var report = await File.ReadAllTextAsync(Path.Combine(directory, "report.md"), TestContext.Current.CancellationToken);
            Assert.Contains("## Souhrn", report);
            Assert.Contains("Za hodnocení 5 hvězdičkami vám vrátíme 100 Kč.", report);
            Assert.Contains("## Co nebylo zkontrolováno", report);
            Assert.Contains("falešným klientem", report);

            using var json = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(directory, "findings.json"), TestContext.Current.CancellationToken));
            Assert.Equal(result.Findings.Count, json.RootElement.GetArrayLength());
            Assert.True(json.RootElement[0].TryGetProperty("question_probs", out _));

            var segmentsHeader = File.ReadLines(Path.Combine(directory, "segments.csv")).First();
            Assert.Contains(",ucp_reviews_verified_claim,", segmentsHeader);
            Assert.DoesNotContain(",eco_claim,", segmentsHeader);
            Assert.EndsWith(",legal_adr,legal_complaints,legal_withdrawal,legal_withdrawal_form,legal_withdrawal_online_option,legal_withdrawal_button_location", segmentsHeader);

            var findingsHeader = File.ReadLines(Path.Combine(directory, "findings.csv")).First();
            Assert.EndsWith(",human_label,note", findingsHeader);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Scan_SkipsEvaluationWhenEstimateIsNotConfirmed()
    {
        JevCallEstimate? estimate = null;
        var options = new ScanOptions
        {
            Country = "cz",
            ConfirmJevCalls = (e, _) =>
            {
                estimate = e;
                return Task.FromResult(false);
            },
        };

        var result = await ScanAsync(FileSystemPageFetcher.ForFixture(), options);

        Assert.NotNull(estimate);
        // In Czechia sentences go to one sentence module (ucp), legal paragraphs to the legal module; the estimate
        // also counts the sieve and is an upper bound, because the sieve then leaves some sentences out.
        Assert.True(estimate.SieveCalls > 0);
        Assert.True(estimate.UpperBound);
        Assert.Equal(result.Segments.Count + estimate.SieveCalls, estimate.Calls);
        Assert.True(estimate.IsMock);
        Assert.False(estimate.RequiresConfirmation);
        Assert.True(result.EvaluationSkipped);
        Assert.Empty(result.Findings);
        Assert.Equal(0, result.Stats.JevCalls);
        Assert.NotEmpty(result.Segments);
    }

    private static async Task<ScanResult> ScanAsync(IPageFetcher fetcher, ScanOptions? options = null)
    {
        await using var provider = TestServices.Create(fetcher);
        var guard = provider.GetRequiredService<IEshopGuard>();
        return await guard.ScanSiteAsync(FileSystemPageFetcher.DefaultBaseUrl, options ?? new ScanOptions { Country = "cz" }, ct: TestContext.Current.CancellationToken);
    }

    private sealed class MissingFilesFetcher(IPageFetcher inner, params string[] missingPaths) : IPageFetcher
    {
        public Task<FetchResponse> FetchAsync(Uri url, CancellationToken ct) =>
            missingPaths.Contains(url.AbsolutePath)
                ? Task.FromResult(new FetchResponse { Url = url, StatusCode = 404 })
                : inner.FetchAsync(url, ct);
    }
}

internal sealed record ExpectedFinding(string Path, string RuleId, string Text);

internal sealed record ExpectedFindings(
    List<ExpectedFinding> Findings,
    List<string> SiteFindings,
    List<string> PagesWithoutFindings,
    Dictionary<string, string> FetchedPages,
    List<string> NotFetched,
    string BoilerplateText)
{
    public static ExpectedFindings Load(string file = "expected_findings.json")
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", file);
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
        return JsonSerializer.Deserialize<ExpectedFindings>(File.ReadAllText(path), options)
            ?? throw new InvalidOperationException("expected_findings.json is empty");
    }
}
