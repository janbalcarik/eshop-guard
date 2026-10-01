using EshopGuard.Core.Crawl;
using EshopGuard.Core.Options;
using Microsoft.Extensions.DependencyInjection;

namespace EshopGuard.Core.Tests;

/// <summary>
/// A page whose redirect leads into an internal network (the fetcher refuses it with <c>ssrf_blocked</c>) is never shown
/// as checked: it is listed in the result, in a warning and in the report (task 6.4).
/// </summary>
public sealed class ScanSsrfTests
{
    [Fact]
    public async Task RedirectIntoInternalNetwork_IsListedAsNotChecked()
    {
        var inner = FileSystemPageFetcher.ForSlovakFixture();
        var fetcher = new RedirectingFetcher(inner);
        await using var provider = TestServices.Create(fetcher);

        var result = await provider.GetRequiredService<IEshopGuard>()
            .ScanSiteAsync(FileSystemPageFetcher.DefaultBaseUrl, new ScanOptions(), ct: TestContext.Current.CancellationToken);
        var report = await OutputNormalizer.WriteAsync(provider, result);

        Assert.Equal(["http://fixture.test/interni"], result.BlockedUrls);
        Assert.DoesNotContain(result.Pages, p => p.Url.Contains("produkt-2", StringComparison.Ordinal) || p.Url.Contains("interni", StringComparison.Ordinal));
        Assert.Contains(result.Warnings, w => w.Contains("SSRF", StringComparison.Ordinal));
        Assert.Contains("- Adresy, které vedou do vnitřní nebo místní sítě, se nestáhly (ochrana proti SSRF) (1):\n  - http://fixture.test/interni", report["report.md"], StringComparison.Ordinal);
        Assert.Equal(0, result.Stats.PagesFailed);
    }

    /// <summary>produkt-2 redirects within the site to /interni, which the fetcher refuses as an internal address.</summary>
    private sealed class RedirectingFetcher(IPageFetcher inner) : IPageFetcher
    {
        public Task<FetchResponse> FetchAsync(Uri url, CancellationToken ct) => url.AbsolutePath switch
        {
            "/produkt-2.html" => Task.FromResult(new FetchResponse { Url = url, StatusCode = 301, RedirectLocation = new Uri(url, "/interni") }),
            "/interni" => Task.FromResult(new FetchResponse { Url = url, Error = SsrfGuard.Error }),
            _ => inner.FetchAsync(url, ct),
        };
    }
}
