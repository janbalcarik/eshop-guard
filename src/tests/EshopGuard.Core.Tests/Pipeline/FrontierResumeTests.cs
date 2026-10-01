using System.Text.Json;
using EshopGuard.Core.Models;
using EshopGuard.Core.Options;
using EshopGuard.Core.Pipeline;
using Microsoft.Extensions.DependencyInjection;

namespace EshopGuard.Core.Tests;

/// <summary>
/// The crawl goes on after a break (task 5.6): the state of the frontier is saved as JSON after every batch of 3 pages,
/// and a new fetch step of a new container continues from it. The pages are the same as without breaks and no URL is
/// downloaded twice; batches inside one scan do not change the findings.
/// </summary>
public sealed class FrontierResumeTests
{
    [Fact]
    public async Task Crawl_ResumedFromJsonAfterEveryBatch_GivesTheSamePages()
    {
        var uninterrupted = await ScanAsync(new ScanOptions());

        var fetcher = FileSystemPageFetcher.ForSlovakFixture();
        var site = new SiteScope(FileSystemPageFetcher.DefaultBaseUrl);
        string robotsJson, frontierJson, paceJson;
        await using (var provider = TestServices.Create(fetcher))
        {
            var discovery = await provider.GetRequiredService<DiscoveryStep>()
                .DiscoverAsync(new DiscoveryInput(site, new CrawlLimits(200, 100, [], [], null)), null, TestContext.Current.CancellationToken);
            robotsJson = PipelineJson.Serialize(discovery.Robots);
            frontierJson = PipelineJson.Serialize(discovery.Frontier);
            paceJson = PipelineJson.Serialize(discovery.Pace);
        }

        var pages = new List<PageInfo>();
        var batches = 0;
        while (true)
        {
            batches++;
            await using var provider = TestServices.Create(fetcher);
            var input = new FetchBatchInput(
                site,
                JsonSerializer.Deserialize<RobotsSnapshot>(robotsJson, PipelineJson.Options)!,
                PipelineJson.Deserialize<UrlFrontierState>(frontierJson),
                JsonSerializer.Deserialize<PaceState>(paceJson, PipelineJson.Options)!,
                MaxPages: 3,
                MaxDuration: TimeSpan.FromMinutes(5),
                ExtractInline: true);
            var result = await provider.GetRequiredService<FetchStep>().FetchBatchAsync(input, null, TestContext.Current.CancellationToken);
            pages.AddRange(result.Pages.Where(p => p.Extract is { Status: ExtractionStatus.Ok }).Select(p => p.Extract!.Info));
            frontierJson = PipelineJson.Serialize(result.Frontier);
            paceJson = PipelineJson.Serialize(result.Pace);
            if (result.FrontierExhausted)
            {
                break;
            }
        }

        Assert.True(batches > 3, $"{batches} batches");
        Assert.Equal(Describe(uninterrupted.Pages), Describe(pages));
        var downloaded = fetcher.Requested.Select(u => u.AbsoluteUri).ToList();
        Assert.Equal(downloaded.Distinct().Count(), downloaded.Count);
    }

    [Fact]
    public async Task Scan_InBatchesOfThreePages_GivesTheSameOutputs()
    {
        await using var whole = TestServices.Create(FileSystemPageFetcher.ForSlovakFixture());
        var expected = await OutputNormalizer.WriteAsync(whole, await whole.GetRequiredService<IEshopGuard>()
            .ScanSiteAsync(FileSystemPageFetcher.DefaultBaseUrl, new ScanOptions(), ct: TestContext.Current.CancellationToken));

        await using var batched = TestServices.Create(FileSystemPageFetcher.ForSlovakFixture(), o => o.Crawl.FetchBatchMaxPages = 3);
        var actual = await OutputNormalizer.WriteAsync(batched, await batched.GetRequiredService<IEshopGuard>()
            .ScanSiteAsync(FileSystemPageFetcher.DefaultBaseUrl, new ScanOptions(), ct: TestContext.Current.CancellationToken));

        var differences = OutputNormalizer.Differences(expected, actual);
        Assert.True(differences.Length == 0, differences);
    }

    private static async Task<ScanResult> ScanAsync(ScanOptions options)
    {
        await using var provider = TestServices.Create(FileSystemPageFetcher.ForSlovakFixture());
        return await provider.GetRequiredService<IEshopGuard>().ScanSiteAsync(FileSystemPageFetcher.DefaultBaseUrl, options, ct: TestContext.Current.CancellationToken);
    }

    /// <summary>The pages as the crawl leaves them (the counts of segments come later).</summary>
    private static List<string> Describe(IEnumerable<PageInfo> pages) =>
        pages.Select(p =>
        {
            var node = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(p))!.AsObject();
            node.Remove(nameof(PageInfo.SentenceCount));
            node.Remove(nameof(PageInfo.ParagraphCount));
            return node.ToJsonString();
        }).ToList();
}
