using EshopGuard.Core.Models;
using EshopGuard.Core.Options;
using Microsoft.Extensions.DependencyInjection;

namespace EshopGuard.Core.Tests;

/// <summary>
/// Scans the Slovak fixture e-shop (the primary market) with the mock client: obligations new from 27. 9. 2026
/// (environmental claims, durability and repair claims, harmonised notice) and from 19. 6. 2026 (withdrawal function).
/// </summary>
public class ScanSlovakFixtureTests
{
    private const string ExpectedFile = "expected_findings_sk.json";

    [Fact]
    public async Task Scan_FetchesAllPagesAndLeavesNavigationOut()
    {
        var result = await ScanAsync();
        var expected = ExpectedFindings.Load(ExpectedFile);

        var fetched = result.Pages.ToDictionary(p => new Uri(p.Url).AbsolutePath, p => TextTools.Snake(p.Type));
        Assert.Equal(expected.FetchedPages.OrderBy(p => p.Key), fetched.OrderBy(p => p.Key));
        Assert.Empty(result.Warnings);

        // Menu categories and the breadcrumb trail are navigation; the page heading with the same words stays.
        foreach (var label in new[] { "Ekologická drogéria", "Liečivé huby", "Zelená domácnosť", "Domov", "Drogéria" })
        {
            Assert.DoesNotContain(result.Segments, s => s.Text == label);
        }

        Assert.Contains(result.Segments, s => s.Text == "Ekologický šampón s levanduľou" && s.Sources.Contains(SegmentSource.Main));
    }

    [Fact]
    public async Task Scan_InMockModeFindsPlantedCasesOfNewObligations()
    {
        var result = await ScanAsync();
        var expected = ExpectedFindings.Load(ExpectedFile);

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

        Assert.Equal(["eco", "dur", "ucp", "legal"], result.RuleSets.Select(r => r.Module));

        // The footer links to the closed ODR platform on every page; the harmonised notice is missing everywhere.
        var odr = Assert.Single(result.Findings, f => f.RuleId == "legal_odr_link_outdated");
        Assert.Equal(expected.FetchedPages.Count, odr.Urls.Count);
        var notice = Assert.Single(result.Findings, f => f.RuleId == "legal_harmonized_notice_missing");
        Assert.Equal(FindingBand.Review, notice.Band);
        Assert.Contains(notice.Notes, n => n.Contains("pokladnu", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Scan_ReadsBadgesSeparatelyAndLeavesFiltersOut()
    {
        var result = await ScanAsync();

        // Each badge is its own segment; the filter labels ("Eco 1") are interface, not text.
        Assert.Contains(result.Segments, s => s.Text == "Eco");
        Assert.Contains(result.Segments, s => s.Text == "Doprava zadarmo nad 50 €");
        Assert.DoesNotContain(result.Segments, s => s.Text.StartsWith("Eco Vegan", StringComparison.Ordinal) || s.Text.StartsWith("Eco 1", StringComparison.Ordinal));

        // The "Eco" badge is a label with a term the law names (a violation), not also a generic claim; "Vegan" is a badge
        // to assess (Commission Q&A 15). The same badge on two products is one finding.
        Assert.DoesNotContain(result.Findings, f => f.RuleId == "eco_generic_claim" && f.Text == "Eco");
        Assert.Equal("text", Assert.Single(result.Findings, f => f.RuleId == "eco_label_generic_term" && f.Text == "Eco").Checkability);
        var vegan = Assert.Single(result.Findings, f => f.RuleId == "eco_label_open" && f.Text == "Vegan");
        Assert.Equal("assess", vegan.Checkability);
        Assert.Equal(["/produkt-3.html", "/produkt-9.html"], vegan.Urls.Select(u => new Uri(u).AbsolutePath).Order());
        Assert.DoesNotContain(result.Findings, f => f.Text?.StartsWith("Zloženie:", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task Scan_KeepsOneFindingWhenTitleRepeatsTheHeading()
    {
        var result = await ScanAsync();

        var onPage = result.Findings
            .Where(f => f.RuleId == "eco_generic_claim" && f.Urls.Any(u => new Uri(u).AbsolutePath == "/produkt-1.html"))
            .ToList();
        Assert.Contains(onPage, f => f.Text == "Ekologický šampón s levanduľou" && f.Sources.Contains(SegmentSource.Main));
        Assert.DoesNotContain(onPage, f => f.Sources.All(s => s is SegmentSource.Title or SegmentSource.MetaDescription));
    }

    private static async Task<ScanResult> ScanAsync()
    {
        await using var provider = TestServices.Create(FileSystemPageFetcher.ForSlovakFixture());
        return await provider.GetRequiredService<IEshopGuard>().ScanSiteAsync(
            FileSystemPageFetcher.DefaultBaseUrl, new ScanOptions { Country = "sk" }, ct: TestContext.Current.CancellationToken);
    }
}
