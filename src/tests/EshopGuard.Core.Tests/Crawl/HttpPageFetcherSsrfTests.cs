using System.Collections.Concurrent;
using System.Net;
using EshopGuard.Core.Crawl;
using EshopGuard.Core.Options;
using Microsoft.Extensions.DependencyInjection;

namespace EshopGuard.Core.Tests;

/// <summary>
/// The real HTTP fetcher never connects to an internal address (tasks 6.2, 6.3 and 6.6): the host is resolved once by
/// <see cref="IHostAddressResolver"/>, any blocked address stops the request, and only ports 80 and 443 are allowed unless
/// <see cref="CrawlOptions.AllowPrivateNetwork"/> is set for a local test shop.
/// </summary>
public sealed class HttpPageFetcherSsrfTests
{
    [Theory]
    [InlineData("http://intranet.shop.test/", "10.0.0.5")]
    [InlineData("http://metadata.shop.test/latest/meta-data/", "169.254.169.254")]
    [InlineData("https://dual.shop.test/", "93.184.216.34,::1")]
    [InlineData("http://169.254.169.254/latest/meta-data/", "169.254.169.254")]
    [InlineData("http://[::ffff:10.0.0.1]/", "::ffff:10.0.0.1")]
    public async Task InternalAddress_IsBlockedWithoutConnection(string url, string addresses)
    {
        var resolver = new FakeResolver(addresses.Split(',').Select(IPAddress.Parse).ToArray());
        await using var provider = Create(resolver, allowPrivateNetwork: false);

        var response = await provider.GetRequiredService<IPageFetcher>().FetchAsync(new Uri(url), TestContext.Current.CancellationToken);

        Assert.Equal(SsrfGuard.Error, response.Error);
        Assert.Equal(0, response.StatusCode);
        Assert.NotEmpty(resolver.Hosts);
    }

    [Theory]
    [InlineData("https://shop.test:8443/")]
    [InlineData("http://shop.test:8000/")]
    [InlineData("https://jmeno:heslo@shop.test/")]
    public async Task OtherPortOrCredentials_AreRefusedBeforeDns(string url)
    {
        var resolver = new FakeResolver([IPAddress.Parse("93.184.216.34")]);
        await using var provider = Create(resolver, allowPrivateNetwork: false);

        var response = await provider.GetRequiredService<IPageFetcher>().FetchAsync(new Uri(url), TestContext.Current.CancellationToken);

        Assert.Equal(SsrfGuard.Error, response.Error);
        Assert.Empty(resolver.Hosts);
    }

    [Fact]
    public async Task LocalShop_IsDownloadedOnlyWithAllowPrivateNetwork()
    {
        await using var server = new LocalHttpServer(_ => new LocalHttpServer.Answer(200, "<html><body><p>Ahoj</p></body></html>"));
        var url = new Uri($"http://shop.test:{server.Port}/");
        var resolver = new FakeResolver([IPAddress.Loopback]);

        await using (var blocked = Create(resolver, allowPrivateNetwork: false))
        {
            var refused = await blocked.GetRequiredService<IPageFetcher>().FetchAsync(url, TestContext.Current.CancellationToken);
            Assert.Equal(SsrfGuard.Error, refused.Error);
        }

        Assert.Empty(server.Requests);
        await using var allowed = Create(resolver, allowPrivateNetwork: true);
        var response = await allowed.GetRequiredService<IPageFetcher>().FetchAsync(url, TestContext.Current.CancellationToken);

        Assert.True(response.IsSuccess, response.Error);
        Assert.Contains("Ahoj", System.Text.Encoding.UTF8.GetString(response.Body!));
        Assert.Single(server.Requests);
    }

    [Fact]
    public async Task EveryRequest_IdentifiesItselfAsEshopGuard()
    {
        // CLAUDE.md: User-Agent EshopGuard/0.1 (task 4.4 of change 8; the worker refuses any other, StartupChecks).
        await using var server = new LocalHttpServer(_ => new LocalHttpServer.Answer(200, "<html><body><p>Ahoj</p></body></html>"));
        await using var provider = Create(new FakeResolver([IPAddress.Loopback]), allowPrivateNetwork: true);

        await provider.GetRequiredService<IPageFetcher>().FetchAsync(new Uri($"http://shop.test:{server.Port}/"), TestContext.Current.CancellationToken);

        Assert.StartsWith("EshopGuard/0.1", server.Requests.Single().Headers["User-Agent"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task LocalShopOnDefaultPort_IsBlockedByAddress()
    {
        // The port is allowed, the address is not: the check after DNS stops it.
        var resolver = new FakeResolver([IPAddress.Loopback]);
        await using var provider = Create(resolver, allowPrivateNetwork: false);

        var response = await provider.GetRequiredService<IPageFetcher>().FetchAsync(new Uri("http://localhost/"), TestContext.Current.CancellationToken);

        Assert.Equal(SsrfGuard.Error, response.Error);
        Assert.Equal(["localhost"], resolver.Hosts);
    }

    [Theory]
    [InlineData("http://10.0.0.5/")]
    [InlineData("http://127.0.0.1:8000/")]
    public async Task Scan_OfAnInternalSite_FailsWithSsrfBlocked(string site)
    {
        var resolver = new FakeResolver([IPAddress.Parse("10.0.0.5")]);
        await using var provider = Create(resolver, allowPrivateNetwork: false);

        var error = await Assert.ThrowsAsync<SsrfBlockedException>(() => provider.GetRequiredService<IEshopGuard>()
            .ScanSiteAsync(new Uri(site), new ScanOptions(), ct: TestContext.Current.CancellationToken));

        Assert.StartsWith(SsrfGuard.Error, error.Message, StringComparison.Ordinal);
    }

    private static ServiceProvider Create(IHostAddressResolver resolver, bool allowPrivateNetwork)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(resolver);
        services.AddEshopGuard(options =>
        {
            options.Crawl.RequestsPerSecond = 0;
            options.Crawl.TimeoutSeconds = 5;
            options.Crawl.AllowPrivateNetwork = allowPrivateNetwork;
            options.Jev.UseMock = true;
            options.Rewrite.UseMock = true;
            options.Rules.Directory = TestServices.RulesDirectory;
            options.Rules.LabelsFile = TestServices.LabelsFile;
            options.Rules.LegalRequirementsFile = TestServices.LegalRequirementsFile;
            options.Rules.SieveFile = TestServices.SieveFile;
            options.Rewrite.PromptFile = TestServices.RewritePromptFile;
            options.Profiles.Enabled = false;
        });
        return services.BuildServiceProvider();
    }

    /// <summary>DNS of the test: every host has the given addresses; the asked hosts are recorded.</summary>
    internal sealed class FakeResolver(IPAddress[] addresses) : IHostAddressResolver
    {
        private readonly ConcurrentQueue<string> _hosts = new();

        public IReadOnlyList<string> Hosts => [.. _hosts];

        public Task<IPAddress[]> ResolveAsync(string host, CancellationToken ct)
        {
            _hosts.Enqueue(host);
            return Task.FromResult(IPAddress.TryParse(host.Trim('[', ']'), out var literal) ? [literal] : addresses);
        }
    }
}
