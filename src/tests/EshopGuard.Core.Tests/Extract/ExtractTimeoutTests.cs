using EshopGuard.Core.Extract;
using EshopGuard.Core.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace EshopGuard.Core.Tests;

/// <summary>
/// A page whose extraction takes longer than <c>crawl.extract_timeout_seconds</c> is not read and never passes for a
/// checked page: it is listed with its reason, in a warning and in the report (task 4.4).
/// </summary>
public sealed class ExtractTimeoutTests
{
    [Fact]
    public async Task SlowPage_IsNotProcessed_AndReported()
    {
        var extractor = new SlowExtractor("/produkt-3.html", TimeSpan.FromSeconds(4));
        await using var provider = TestServices.Create(FileSystemPageFetcher.ForSlovakFixture(), o => o.Crawl.ExtractTimeoutSeconds = 1,
            register: s => s.AddSingleton<IPageExtractor>(extractor));

        var result = await provider.GetRequiredService<IEshopGuard>()
            .ScanSiteAsync(FileSystemPageFetcher.DefaultBaseUrl, new ScanOptions(), ct: TestContext.Current.CancellationToken);
        var outputs = await OutputNormalizer.WriteAsync(provider, result);

        var page = Assert.Single(result.NotProcessedPages);
        Assert.Equal("http://fixture.test/produkt-3.html", page.Url);
        Assert.Equal("extract_timeout", page.Reason);
        Assert.DoesNotContain(result.Pages, p => p.Url == page.Url);
        Assert.DoesNotContain(result.Segments, s => s.Urls.Contains(page.Url));
        Assert.Contains(TestTexts.Warnings(result.Warnings), w => w.Contains("časovém limitu", StringComparison.Ordinal));
        Assert.Contains("  - http://fixture.test/produkt-3.html", outputs["report.md"], StringComparison.Ordinal);
        Assert.NotEmpty(result.Pages);
    }

    /// <summary>The real extractor, slow on one page.</summary>
    private sealed class SlowExtractor(string slowPath, TimeSpan delay) : IPageExtractor
    {
        private readonly ContentExtractor _inner = new(NullLogger<ContentExtractor>.Instance);

        public ExtractedPage Extract(Uri url, ParsedPage page)
        {
            if (url.AbsolutePath == slowPath)
            {
                Thread.Sleep(delay);
            }

            return _inner.Extract(url, page);
        }
    }
}
