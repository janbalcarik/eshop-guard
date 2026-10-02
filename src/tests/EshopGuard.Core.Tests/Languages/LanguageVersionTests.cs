using EshopGuard.Core.Languages;
using EshopGuard.Core.Markets;
using EshopGuard.Core.Pipeline;
using Microsoft.Extensions.DependencyInjection;

namespace EshopGuard.Core.Tests;

/// <summary>Finding, trying and crawling language versions over the fixture shops of <c>Fixtures/versions</c> (change 7, group 4).</summary>
public sealed class LanguageVersionTests
{
    private static readonly MarketCatalog Catalog = MarketCatalog.From(TestTexts.Catalog);

    [Fact]
    public async Task Finder_VersionInAPath()
    {
        var versions = LanguageVersionFinder.Find(await SignalsAsync("path-shop.cz"), Site("path-shop.cz"), Catalog);

        Assert.Equal(
            [("cs", "https://path-shop.cz/", SwitchMethods.Path, VersionSources.Main, VersionStatus.Active),
             ("sk", "https://path-shop.cz/sk/", SwitchMethods.Path, VersionSources.Hreflang, VersionStatus.Active)],
            versions.Select(v => (v.Language, v.BaseUrl, v.SwitchMethod, v.Source, v.Status)));
    }

    [Fact]
    public async Task Finder_VersionOnAnotherDomainWaitsForConfirmation()
    {
        var versions = LanguageVersionFinder.Find(await SignalsAsync("domain-shop.cz"), Site("domain-shop.cz"), Catalog);

        var sk = versions.Single(v => !v.IsMain);
        Assert.Equal(("sk", SwitchMethods.Domain, VersionStatus.NeedsConfirmation), (sk.Language, sk.SwitchMethod, sk.Status));
    }

    [Fact]
    public void Finder_ModelOnlyWhenTheStructureShowsNothing()
    {
        var signals = new MarketSignals { Site = "https://shop.test/", HtmlLang = "cs" };
        VersionAnswer[] model = [new VersionAnswer("sk", "https://shop.test/?lang=sk", "query", new ModelQuote("Slovenská verzia", "https://shop.test/"))];

        var versions = LanguageVersionFinder.Find(signals, new Uri("https://shop.test/"), Catalog, model);
        var withHreflang = LanguageVersionFinder.Find(
            new MarketSignals { Site = "https://shop.test/", HtmlLang = "cs", Hreflang = [new HreflangSignal("sk", "https://shop.test/sk/", "page")] },
            new Uri("https://shop.test/"), Catalog, model);

        var sk = versions.Single(v => !v.IsMain);
        Assert.Equal(("sk", VersionSources.Llm, SwitchMethods.Query), (sk.Language, sk.Source, sk.SwitchMethod));
        Assert.DoesNotContain(withHreflang, v => v.Source == VersionSources.Llm);
    }

    [Fact]
    public void Finder_SameAddressAndLanguage_IsOneVersion()
    {
        // goodie.sk (2. 10. 2026): hreflang cs-CZ to www.goodie.cz and a switcher to the same domain without a language.
        var signals = new MarketSignals
        {
            Site = "https://www.goodie.sk/", HtmlLang = "sk",
            Hreflang = [new HreflangSignal("sk-SK", "https://www.goodie.sk/", "page"), new HreflangSignal("cs-CZ", "https://www.goodie.cz/", "page")],
            SwitcherCandidates = [new SwitcherCandidate("https://www.goodie.cz/", "domain", null)],
        };

        var versions = LanguageVersionFinder.Find(signals, new Uri("https://www.goodie.sk/"), Catalog);

        var cz = Assert.Single(versions, v => !v.IsMain);
        Assert.Equal(("cs-cz", "https://www.goodie.cz/"), (cz.Language, cz.BaseUrl));
        Assert.Equal(2, cz.Evidence.Count);
    }

    [Fact]
    public void Finder_PolishVersionIsUnsupported()
    {
        var signals = new MarketSignals
        {
            Site = "https://shop.cz/", HtmlLang = "cs",
            Hreflang = [new HreflangSignal("sk", "https://shop.cz/sk/", "page"), new HreflangSignal("pl", "https://shop.cz/pl/", "page")],
        };

        var versions = LanguageVersionFinder.Find(signals, new Uri("https://shop.cz/"), Catalog);

        Assert.Equal(VersionStatus.Unsupported, versions.Single(v => v.Language == "pl").Status);
        Assert.Equal(VersionStatus.Active, versions.Single(v => v.Language == "sk").Status);
    }

    [Fact]
    public void Finder_ConnectorLanguages()
    {
        var versions = LanguageVersionFinder.Find(new MarketSignals { Site = "https://shop.cz/", HtmlLang = "cs" }, new Uri("https://shop.cz/"), Catalog,
            connector: [new ConnectorLanguage("sk", "https://shop.cz/sk/")]);

        Assert.Contains(versions, v => v is { Language: "sk", Source: VersionSources.Connector, SwitchMethod: SwitchMethods.Path });
    }

    [Theory]
    [InlineData("path-shop.cz", "sk", VersionStatus.Active, SwitchMethods.Path, null)]
    [InlineData("domain-shop.cz", "sk", VersionStatus.NeedsConfirmation, SwitchMethods.Domain, null)]
    [InlineData("cookie-shop.cz", "sk", VersionStatus.Active, SwitchMethods.Cookie, null)]
    [InlineData("accept-shop.cz", "sk", VersionStatus.Active, SwitchMethods.AcceptLanguage, null)]
    [InlineData("script-shop.cz", "sk", VersionStatus.NeedsBrowser, SwitchMethods.Script, VersionCodes.NeedsBrowser)]
    [InlineData("widget-shop.cz", "sk", VersionStatus.Active, SwitchMethods.BrowserTranslation, VersionCodes.BrowserTranslation)]
    [InlineData("redirect-shop.cz", "sk", VersionStatus.Mismatch, SwitchMethods.Path, VersionCodes.LanguageMismatch)]
    [InlineData("spa-shop.cz", "sk", VersionStatus.Active, SwitchMethods.Path, VersionCodes.TextNotLoaded)]
    public async Task Probe_EveryFixtureShop(string host, string language, string status, string method, string? code)
    {
        var fetcher = MultiHostFileSystemPageFetcher.ForVersions();
        await using var provider = TestServices.Create(fetcher);
        var signals = await SignalsAsync(host);

        var versions = await provider.GetRequiredService<VersionAccessProbe>()
            .ProbeAllAsync(LanguageVersionFinder.Find(signals, Site(host), Catalog), signals, TestContext.Current.CancellationToken);

        var version = versions.Single(v => !v.IsMain && v.Language == language);
        Assert.Equal((status, method), (version.Status, version.SwitchMethod));
        if (code is null)
        {
            Assert.Empty(version.Codes);
        }
        else
        {
            Assert.Contains(code, version.Codes);
        }

        var checkable = status == VersionStatus.Active && method != SwitchMethods.BrowserTranslation;
        Assert.Equal(checkable, version.IsCheckable);
        Assert.NotNull(versions.Single(v => v.IsMain).Scope);
    }

    [Fact]
    public async Task PathShop_VersionsDoNotMix()
    {
        var fetcher = MultiHostFileSystemPageFetcher.ForVersions();
        await using var provider = TestServices.Create(fetcher);
        var ct = TestContext.Current.CancellationToken;
        var signals = await SignalsAsync("path-shop.cz");
        var versions = await provider.GetRequiredService<VersionAccessProbe>().ProbeAllAsync(LanguageVersionFinder.Find(signals, Site("path-shop.cz"), Catalog), signals, ct);
        var crawler = provider.GetRequiredService<VersionCrawler>();

        var cs = await crawler.CrawlAsync(new SiteScope(Site("path-shop.cz")) { Version = versions.Single(v => v.IsMain).Scope }, 100, ct);
        var sk = await crawler.CrawlAsync(new SiteScope(new Uri("https://path-shop.cz/sk/")) { Version = versions.Single(v => v.Language == "sk").Scope }, 100, ct);

        Assert.Equal(16, cs.Pages.Count);
        Assert.DoesNotContain(cs.Pages, p => p.Info.Url.Contains("/sk/", StringComparison.Ordinal));
        Assert.All(cs.Pages, p => Assert.Equal("cs", p.Info.Language));
        Assert.Equal(16, sk.Pages.Count);
        Assert.All(sk.Pages, p => Assert.StartsWith("https://path-shop.cz/sk/", p.Info.Url, StringComparison.Ordinal));
        Assert.All(sk.Pages, p => Assert.Equal("sk", p.Info.Language));
        Assert.All(cs.Discovery.SitemapEntries.Where(e => e.ProductHint), e => Assert.Equal(2, e.Alternates.Count));
    }

    [Fact]
    public async Task CookieShop_OnlyTheSlovakCrawlSendsTheCookie()
    {
        var fetcher = MultiHostFileSystemPageFetcher.ForVersions();
        await using var provider = TestServices.Create(fetcher);
        var ct = TestContext.Current.CancellationToken;
        var signals = await SignalsAsync("cookie-shop.cz");
        var versions = await provider.GetRequiredService<VersionAccessProbe>().ProbeAllAsync(LanguageVersionFinder.Find(signals, Site("cookie-shop.cz"), Catalog), signals, ct);
        var crawler = provider.GetRequiredService<VersionCrawler>();
        var skScope = versions.Single(v => v.Language == "sk").Scope!;
        fetcher.Requests.Clear();

        var sk = await crawler.CrawlAsync(new SiteScope(Site("cookie-shop.cz")) { Version = skScope }, 50, ct);
        var skRequests = fetcher.Requests.ToList();
        fetcher.Requests.Clear();
        var cs = await crawler.CrawlAsync(new SiteScope(Site("cookie-shop.cz")) { Version = versions.Single(v => v.IsMain).Scope }, 50, ct);
        var csRequests = fetcher.Requests.ToList();

        Assert.All(skRequests.Where(r => r.Url.AbsolutePath.EndsWith(".html", StringComparison.Ordinal) || r.Url.AbsolutePath == "/"), r => Assert.Equal("lang=sk", r.Cookie));
        Assert.All(csRequests, r => Assert.Null(r.Cookie));
        Assert.All(sk.Pages, p => Assert.Equal("sk", p.Info.Language));
        Assert.Contains(sk.Pages, p => p.Content.Title!.Contains("dielňa", StringComparison.Ordinal));
        Assert.All(cs.Pages, p => Assert.DoesNotContain("dielňa", p.Content.Title, StringComparison.Ordinal));
        Assert.DoesNotContain(cs.Pages, p => p.Info.Url.Contains("lang=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TwoScopesOfTheSameDomain_RunTogetherWithTheirOwnCookies()
    {
        var fetcher = MultiHostFileSystemPageFetcher.ForVersions();
        await using var provider = TestServices.Create(fetcher);
        var ct = TestContext.Current.CancellationToken;
        var crawler = provider.GetRequiredService<VersionCrawler>();
        var site = Site("cookie-shop.cz");
        var sk = VersionCrawlScope.ForRoot(site, "sk") with
        {
            Cookies = new Dictionary<string, string> { ["lang"] = "sk" },
            ExcludedQuery = [new QueryParameter("lang", VersionCrawlScope.AnyValue)],
        };
        var cs = VersionCrawlScope.ForRoot(site, "cs") with { ExcludedQuery = [new QueryParameter("lang", VersionCrawlScope.AnyValue)] };

        var results = await Task.WhenAll(
            crawler.CrawlAsync(new SiteScope(site) { Version = sk }, 50, ct),
            crawler.CrawlAsync(new SiteScope(site) { Version = cs }, 50, ct),
            crawler.CrawlAsync(new SiteScope(site) { Version = sk }, 50, ct));

        Assert.All(results[0].Pages.Concat(results[2].Pages), p => Assert.Contains("dielňa", p.Content.Title, StringComparison.Ordinal));
        Assert.All(results[1].Pages, p => Assert.Contains("dílna", p.Content.Title, StringComparison.Ordinal));
    }

    [Fact]
    public async Task HttpFetcher_SendsTheCookiesOfTheRequestOnly()
    {
        var headers = new List<(string? Cookie, string? Language)>();
        var handler = new RecordingHandler(headers);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpClient(Crawl.HttpPageFetcher.HttpClientName, c => c.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "cs,sk;q=0.9"))
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        services.AddOptions<Options.EshopGuardOptions>().Configure(o => o.Crawl.AllowPrivateNetwork = true);
        await using var provider = services.BuildServiceProvider();
        var fetcher = ActivatorUtilities.CreateInstance<Crawl.HttpPageFetcher>(provider);
        var ct = TestContext.Current.CancellationToken;

        var first = await fetcher.FetchAsync(new Crawl.FetchRequest(new Uri("https://shop.cz/?lang=sk")), ct);
        await fetcher.FetchAsync(new Crawl.FetchRequest(new Uri("https://shop.cz/")) { Cookies = new Dictionary<string, string> { ["lang"] = "sk" }, AcceptLanguage = "sk" }, ct);
        await fetcher.FetchAsync(new Crawl.FetchRequest(new Uri("https://shop.cz/")), ct);

        Assert.Equal(["lang=sk; Path=/"], first.SetCookies);
        Assert.Equal([(null, "cs,sk;q=0.9"), ("lang=sk", "sk"), (null, "cs,sk;q=0.9")], headers.Select(h => (h.Cookie, h.Language?.Replace(" ", "", StringComparison.Ordinal))));
    }

    internal static Uri Site(string host) => new($"https://{host}/");

    internal static async Task<MarketSignals> SignalsAsync(string host) =>
        MarketSignalReader.Read(await MarketSignalReaderTests.ExtractAsync($"https://{host}/"), [], Site(host), ["weglot", "gtranslate"], 300);

    private sealed class RecordingHandler(List<(string? Cookie, string? Language)> headers) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            headers.Add((request.Headers.TryGetValues("Cookie", out var c) ? string.Join("; ", c) : null,
                request.Headers.TryGetValues("Accept-Language", out var l) ? string.Join(",", l) : null));
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent("<html lang=\"sk\"><body>x</body></html>", System.Text.Encoding.UTF8, "text/html") };
            if (request.RequestUri!.Query.Contains("lang=sk", StringComparison.Ordinal))
            {
                response.Headers.TryAddWithoutValidation("Set-Cookie", "lang=sk; Path=/");
            }

            return Task.FromResult(response);
        }
    }
}
