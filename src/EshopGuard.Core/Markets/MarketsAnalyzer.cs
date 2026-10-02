using System.Text.Json;
using EshopGuard.Core.Classify;
using EshopGuard.Core.Crawl;
using EshopGuard.Core.Fix;
using EshopGuard.Core.Languages;
using EshopGuard.Core.Models;
using EshopGuard.Core.Options;
using EshopGuard.Core.Pipeline;
using EshopGuard.Core.Rules;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EshopGuard.Core.Markets;

/// <summary>What to analyze: the site and, from the application, the ticked markets, the switch of the host and pages it knows.</summary>
public sealed record MarketsAnalysisRequest(Uri Site)
{
    /// <summary>Ticked markets (codes of jurisdictions); null takes the preselected ones of the analysis.</summary>
    public IReadOnlyList<string>? ActiveMarkets { get; init; }

    /// <summary>The host allows the market (<c>ref.markets</c>); null allows every market.</summary>
    public Func<string, bool>? HostAllows { get; init; }

    /// <summary>Legal pages the sample already downloaded (fallback of the pick of pages).</summary>
    public IReadOnlyList<Uri> LegalPages { get; init; } = [];

    /// <summary>Root addresses of versions the client left out („túto verziu nekontrolovať“).</summary>
    public IReadOnlyList<string> ExcludedVersions { get; init; } = [];

    /// <summary>Seed of the random sample of the analysis of versions.</summary>
    public int Seed { get; init; } = 1;

    /// <summary>Compare the versions on a sample (false: only places of sale, versions and the plan).</summary>
    public bool AnalyzeVersions { get; init; } = true;
}

/// <summary>Asks whether the calls of the model may cost the estimate (CLI: a question over the limit; worker: the budget).</summary>
public delegate Task<bool> MarketEstimateConfirmation(MarketAnalysisEstimate estimate, CancellationToken ct);

/// <summary>The analysis of the places of sale and of the language versions of a shop (CLI <c>markets</c>, worker of change 8).</summary>
public interface IMarketsAnalyzer
{
    /// <param name="confirm">Asked with the estimate before the first call of the model; null calls without asking.</param>
    Task<MarketsAnalysisResult> AnalyzeAsync(MarketsAnalysisRequest request, MarketEstimateConfirmation? confirm, IProgress<string>? progress, CancellationToken ct);
}

/// <summary>
/// The analysis of the places of sale and of the language versions of one shop (change 7, design: Data Flow): technical signs
/// of the home page and the sitemap, the estimate, the pick of pages, their download, the analysis of the model and the
/// verification of its quotes, the classification, the versions and how they are reached, the plan, the sample of versions and
/// their comparison. Fail-closed: a failure of the model, a missing key or an estimate not confirmed leave only what the
/// structure of the site shows, with the code why; nothing is passed off as found.
/// </summary>
internal sealed class MarketsAnalyzer(
    VersionCrawler crawler,
    VersionAccessProbe probe,
    IMarketModel model,
    ITextLanguageModel languageModel,
    IRuleSetProvider ruleSets,
    PageClassifier classifier,
    IOptions<EshopGuardOptions> options,
    TimeProvider time,
    ILogger<MarketsAnalyzer> logger,
    IShopPagesSource? pagesSource = null,
    IShopLanguageSource? languageSource = null) : IMarketsAnalyzer
{
    public async Task<MarketsAnalysisResult> AnalyzeAsync(
        MarketsAnalysisRequest request, MarketEstimateConfirmation? confirm, IProgress<string>? progress, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var settings = options.Value;
        var markets = settings.Markets;
        var catalog = MarketCatalog.From(await ruleSets.LoadAsync(ct), request.HostAllows);
        var site = new SiteScope(request.Site);
        var home = site.Home;
        var run = new Run();

        progress?.Report("discovery");
        var discovery = await crawler.DiscoverAsync(site, markets.SamplePages, ct);
        run.Warnings.AddRange(discovery.Warnings);
        run.Requests += discovery.Frontier.Counters.Requests;
        var homePage = discovery.HomeBlocked || discovery.Frontier.Stopped ? null : (await crawler.FetchAsync(site, discovery, [home], ct)).FirstOrDefault();
        if (homePage is null)
        {
            run.Codes.Add(MarketCodes.AnalysisFailed);
            var failed = PlacesOfSaleClassifier.Classify(null, catalog, home, []);
            return Result(run, home, failed, null, [], null, null, catalog);
        }

        run.Pages[homePage.Info.Url] = homePage;
        var signals = MarketSignalReader.Read(homePage.Content, discovery.SitemapEntries, home, markets.BrowserTranslationScripts, markets.MaxHomeLinks, homePage.Info.TextNotLoaded);
        var connectorLanguages = languageSource is null ? [] : await languageSource.GetLanguagesAsync(home, ct);

        // The model: a missing key or an estimate not confirmed leave only what the structure shows.
        var unavailable = model.UnavailableReason;
        if (unavailable is not null)
        {
            run.Codes.Add(unavailable);
        }

        var canCall = unavailable != EngineCodes.ModelMissingKey;
        var structure = LanguageVersionFinder.Find(signals, home, catalog, null, connectorLanguages);
        run.Estimate = MarketAnalysisEstimate.Before(SalesPageSelector.Input(signals).Length, structure.Count(v => v.Status != VersionStatus.Unsupported), settings.Rewrite, markets);
        if (canCall && unavailable is null && confirm is not null && !await confirm(run.Estimate, ct))
        {
            canCall = false;
            run.Codes.Add(MarketCodes.AnalysisNotConfirmed);
        }

        VerifiedSales? sales = null;
        string? failure = null;
        IReadOnlyList<SelectedPage> selected = [];
        if (canCall)
        {
            try
            {
                (sales, selected) = await AnalyzeSalesAsync(run, site, discovery, homePage, signals, request, ct, progress);
            }
            catch (Exception ex) when (ex is RewriteApiException or JsonException)
            {
                logger.LogWarning("Analysis of the places of sale of {Site} failed: {Message}", home, ex.Message);
                failure = MarketCodes.AnalysisFailed;
            }
        }

        progress?.Report("versions");
        var excluded = request.ExcludedVersions.ToHashSet(StringComparer.Ordinal);
        var found = LanguageVersionFinder.Find(signals, home, catalog, sales?.LanguageVersions, connectorLanguages)
            .Select(v => !v.IsMain && excluded.Contains(v.BaseUrl) ? v with { Status = VersionStatus.Excluded } : v)
            .ToList();
        var versions = await probe.ProbeAllAsync(found, signals, ct);
        run.Requests += probe.Requests;
        var places = PlacesOfSaleClassifier.Classify(sales, catalog, home, versions, failure);
        var active = request.ActiveMarkets ?? DefaultMarkets(places, catalog, versions);

        VersionComparisonResult? comparison = null;
        VersionSamplePlan? samplePlan = null;
        var counts = new Dictionary<string, VersionCount>();
        var sampled = versions.Where(v => v.Scope is not null && v.SwitchMethod != SwitchMethods.BrowserTranslation
            && (v.IsCheckable || v.Status == VersionStatus.NeedsConfirmation)).ToList();
        if (request.AnalyzeVersions && sampled.Count > 1)
        {
            progress?.Report("sample");
            (comparison, samplePlan) = await CompareVersionsAsync(run, sampled, discovery, selected, canCall && unavailable != EngineCodes.ModelMissingKey, request.Seed, ct);
            foreach (var version in comparison.Versions)
            {
                counts[version.Key] = new VersionCount(version.ProductCount, version.Counted);
            }
        }
        else
        {
            var main = versions.First(v => v.IsMain);
            counts[VersionMarketPlanner.Key(main.Language, main.BaseUrl)] = new VersionCount(ProductCount(discovery.SitemapEntries, main.Scope), true);

            // The sample of one version without a comparison: its mandatory pages and random products (the free sample checks them).
            var mandatory = selected.Select(p => new Uri(p.Url)).Where(u => main.Scope?.Contains(u) ?? UrlTools.IsSameSite(u, home)).DistinctBy(u => u.AbsoluteUri).ToList();
            var products = discovery.SitemapEntries.Where(e => e.ProductHint && (main.Scope?.Contains(e.Url) ?? true)).ToList();
            samplePlan = VersionSamplePlanner.Plan([new VersionSampleInput(Key(main), main.Language ?? "und", true, products, mandatory)], markets.SamplePages, 0, request.Seed);
        }

        var plan = VersionMarketPlanner.Plan(versions, active, catalog, counts);
        return Result(run, home, places, signals, versions, plan, comparison, catalog) with { SalesPages = selected, SamplePlan = samplePlan };
    }

    private async Task<(VerifiedSales Sales, IReadOnlyList<SelectedPage> Selected)> AnalyzeSalesAsync(
        Run run, SiteScope site, DiscoveryResult discovery, ExtractedPageRecord homePage, MarketSignals signals, MarketsAnalysisRequest request,
        CancellationToken ct, IProgress<string>? progress)
    {
        var markets = options.Value.Markets;
        var home = site.Home;
        progress?.Report("pick");
        var legal = request.LegalPages
            .Concat(homePage.Content.Links.Where(l => classifier.IsLegalUrl(l.Url) || classifier.IsLegalText(l.Text)).Select(l => l.Url))
            .Where(u => UrlTools.IsSameSite(u, home))
            .DistinctBy(u => UrlTools.Normalize(u).AbsoluteUri)
            .ToList();
        var connectorPages = pagesSource is null ? [] : await pagesSource.GetSalesPagesAsync(home, ct);
        var selection = await SalesPageSelector.SelectAsync(model, signals, home, connectorPages, legal, markets, ct);
        run.Usage = run.Usage.Add(selection.Call);
        run.Model = selection.Call?.Model;

        progress?.Report("pages");
        var downloaded = await FetchAsync(run, site, discovery, selection.Pages.Select(p => new Uri(p.Url)).ToList(), ct);
        var inputPages = new List<SalesPage> { new(homePage.Info.Url, PlacesOfSaleModel.PageText(homePage.Content), true) };
        inputPages.AddRange(downloaded.Where(p => p.Info.Url != homePage.Info.Url).Select(p => new SalesPage(p.Info.Url, PlacesOfSaleModel.PageText(p.Content), false)));

        progress?.Report("sales");
        var (answer, call) = await PlacesOfSaleModel.AnalyzeAsync(model, PlacesOfSaleModel.Input(signals, inputPages, markets), markets, ct);
        run.Usage = run.Usage.Add(call);
        run.Model ??= call.Model;
        var texts = inputPages.GroupBy(p => UrlTools.Normalize(new Uri(p.Url)).AbsoluteUri).ToDictionary(g => g.Key, g => g.First().Text, StringComparer.Ordinal);
        return (QuoteVerifier.Verify(answer, texts, signals), selection.Pages);
    }

    /// <summary>Pages of the sample of every version, the language of their sentences and the comparison.</summary>
    private async Task<(VersionComparisonResult Result, VersionSamplePlan Plan)> CompareVersionsAsync(
        Run run, IReadOnlyList<LanguageVersionCandidate> versions, DiscoveryResult mainDiscovery, IReadOnlyList<SelectedPage> selected, bool labelLanguages,
        int seed, CancellationToken ct)
    {
        var markets = options.Value.Markets;
        var main = versions.First(v => v.IsMain);
        var discoveries = new Dictionary<string, DiscoveryResult>(StringComparer.OrdinalIgnoreCase) { [Authority(main)] = mainDiscovery };
        var inputs = new List<VersionSampleInput>();
        var mandatorySources = selected.Select(p => run.Pages.GetValueOrDefault(p.Url)).OfType<ExtractedPageRecord>().ToList();
        foreach (var version in versions)
        {
            var host = Authority(version);
            if (!discoveries.TryGetValue(host, out var discovery))
            {
                discovery = discoveries[host] = await crawler.DiscoverAsync(new SiteScope(new Uri(version.BaseUrl)) { Version = version.Scope }, markets.SamplePages, ct);
                run.Requests += discovery.Frontier.Counters.Requests;
            }

            var mandatory = mandatorySources.Select(p => new Uri(p.Info.Url))
                .Concat(mandatorySources.SelectMany(p => p.Content.Alternates).Where(a => LanguageTags.SamePrimary(a.Language, version.Language)).Select(a => a.Url))
                .Where(version.Scope!.Contains)
                .DistinctBy(u => u.AbsoluteUri)
                .ToList();
            var products = discovery.SitemapEntries.Where(e => e.ProductHint && version.Scope.Contains(e.Url)).ToList();
            inputs.Add(new VersionSampleInput(Key(version), version.Language ?? "und", version.IsMain, products, mandatory));
        }

        var plan = VersionSamplePlanner.Plan(inputs, markets.SamplePages, markets.PairedProducts, seed);
        var fetched = new Dictionary<string, List<ExtractedPageRecord>>(StringComparer.Ordinal);
        var others = versions.Where(v => !v.IsMain).ToList();
        if (plan.Pairs.Count == 0 && others.Count > 0 && markets.PairedProducts > 0)
        {
            // The sitemap names no alternates (goodie.sk, 2. 10. 2026): the product pages of the main version name them in
            // their hreflang. Those pages are downloaded first and their alternates replace random products of the other
            // versions, so the comparison sees the same products in both languages; the number of requests stays the same.
            var mainKey = Key(main);
            var mainUrls = plan.Urls.Where(u => u.VersionKey == mainKey && u.Kind == "product").Take(markets.PairedProducts).Select(u => new Uri(u.Url)).ToList();
            var mainPages = await FetchAsync(run, new SiteScope(new Uri(main.BaseUrl)) { Version = main.Scope }, discoveries[Authority(main)], mainUrls, ct);
            fetched[mainKey] = mainPages;
            var found = mainPages
                .SelectMany(page => others.Select(other => (Page: page, Other: other,
                    Alternate: page.Content.Alternates.FirstOrDefault(a => LanguageTags.SamePrimary(a.Language, other.Language) && other.Scope!.Contains(a.Url)))))
                .Where(x => x.Alternate is not null)
                .Select(x => new SamplePair(x.Page.Info.Url, x.Alternate!.Url.AbsoluteUri, Key(x.Other)))
                .ToList();
            plan = VersionSamplePlanner.WithPagePairs(plan, mainKey, found, Math.Max(1, markets.PairedProducts / others.Count));
        }

        var samples = new List<VersionSample>();
        foreach (var version in versions)
        {
            var key = Key(version);
            var urls = plan.Urls.Where(u => u.VersionKey == key).ToList();
            var siteScope = new SiteScope(new Uri(version.BaseUrl)) { Version = version.Scope };
            var already = fetched.GetValueOrDefault(key) ?? [];
            var known = already.Select(p => p.Info.Url).ToHashSet(StringComparer.Ordinal);
            var rest = urls.Select(u => new Uri(u.Url)).Where(u => !known.Contains(UrlTools.Normalize(u).AbsoluteUri) && !known.Contains(u.AbsoluteUri)).ToList();
            var downloaded = already.Concat(await FetchAsync(run, siteScope, discoveries[Authority(version)], rest, ct)).DistinctBy(p => p.Info.Url).ToList();
            var mandatoryUrls = urls.Where(u => u.Kind == "mandatory").Select(u => u.Url).ToHashSet(StringComparer.Ordinal);

            // Products of the sample are the pages planned as products (the sitemap marks them so, the price counts them so),
            // and pages the classifier recognizes as products; a page may lack both JSON-LD and og:type (goodie.sk).
            var plannedProducts = urls.Where(u => u.Kind is "product" or "pair").Select(u => UrlTools.Normalize(new Uri(u.Url)).AbsoluteUri).ToHashSet(StringComparer.Ordinal);
            var sitemap = discoveries[Authority(version)].SitemapEntries;
            samples.Add(new VersionSample(
                key,
                version.Language ?? "und",
                version.IsMain,
                downloaded.Where(p => !mandatoryUrls.Contains(p.Info.Url) && (p.Info.Type == PageType.Product || plannedProducts.Contains(p.Info.Url))).Select(Sample).ToList(),
                downloaded.Where(p => mandatoryUrls.Contains(p.Info.Url)).Select(Sample).ToList(),
                ProductCount(sitemap, version.Scope)));
        }

        if (labelLanguages)
        {
            for (var i = 0; i < samples.Count; i++)
            {
                var sample = samples[i];
                var paired = plan.Pairs.SelectMany(p => new[] { p.MainUrl, p.OtherUrl });
                var fragments = VersionComparer.Fragments(sample, paired, markets.LanguageFragmentsPerVersion, markets.LanguageFragmentMinChars, markets.LanguageFragmentMaxChars);
                TextLanguageResult labels;
                try
                {
                    labels = await languageModel.LabelAsync(fragments, versions[i].DeclaredLanguage ?? versions[i].Language, ct);
                }
                catch (Exception ex) when (ex is RewriteApiException or JsonException)
                {
                    // Without the language the version does not count (version_language_unknown); the failure is reported.
                    logger.LogWarning("Language of the texts of {Version} failed: {Message}", sample.Key, ex.Message);
                    run.Codes.Add(MarketCodes.AnalysisFailed);
                    continue;
                }

                run.Usage = run.Usage.Add(labels.Call);
                samples[i] = sample with
                {
                    PageLanguages = fragments.Where(f => labels.Labels.ContainsKey(f.Id))
                        .GroupBy(f => f.PageUrl)
                        .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(f => labels.Labels[f.Id]).ToList()),
                };
            }
        }

        var result = VersionComparer.Compare(samples, plan.Pairs, markets);
        return (result, plan);
    }

    /// <summary>Downloads the addresses in the scope of the site, those on other hosts with their own robots.txt.</summary>
    private async Task<List<ExtractedPageRecord>> FetchAsync(Run run, SiteScope site, DiscoveryResult discovery, IReadOnlyList<Uri> urls, CancellationToken ct)
    {
        var pages = new List<ExtractedPageRecord>();
        var known = urls.Where(u => run.Pages.ContainsKey(UrlTools.Normalize(u).AbsoluteUri) && site.Version is null).ToList();
        pages.AddRange(known.Select(u => run.Pages[UrlTools.Normalize(u).AbsoluteUri]));
        foreach (var group in urls.Except(known).GroupBy(u => u.GetLeftPart(UriPartial.Authority), StringComparer.OrdinalIgnoreCase))
        {
            var root = new Uri(group.Key + "/");
            var scope = UrlTools.IsSameSite(root, site.Home) ? site : new SiteScope(root);
            var hostDiscovery = ReferenceEquals(scope, site) ? discovery : await crawler.DiscoverAsync(scope, group.Count(), ct);
            var fetched = await crawler.FetchAsync(scope, hostDiscovery, group.ToList(), ct);
            run.Requests += group.Count();
            foreach (var page in fetched)
            {
                if (site.Version is null)
                {
                    run.Pages[page.Info.Url] = page;
                }
                else
                {
                    run.Pages.TryAdd(page.Info.Url + "#" + site.Version.VersionLanguage, page);
                }

                pages.Add(page);
            }
        }

        return pages;
    }

    /// <summary>Product URLs of the sitemap in the scope; null when the sitemap marks no products.</summary>
    private static int? ProductCount(IReadOnlyList<SitemapEntry> sitemap, VersionCrawlScope? scope) =>
        !sitemap.Any(e => e.ProductHint) ? null : sitemap.Count(e => e.ProductHint && (scope?.Contains(e.Url) ?? true));

    /// <summary>The preselected supported countries, otherwise the home market, otherwise the markets that read the main version.</summary>
    private static List<string> DefaultMarkets(PlacesOfSaleResult places, MarketCatalog catalog, IReadOnlyList<LanguageVersionCandidate> versions)
    {
        var preselected = places.Countries.Where(c => c is { Preselected: true, Market: not null }).Select(c => c.Market!).ToList();
        if (preselected.Count > 0)
        {
            return preselected;
        }

        if (places.HomeCountry is { } home && catalog.IsSupportedCountry(home))
        {
            return [catalog.ByCountry(home)!.Code];
        }

        var mainLanguage = versions.First(v => v.IsMain).Language;
        return catalog.Supported.Where(m => m.Reads(mainLanguage)).Select(m => m.Code).ToList();
    }

    private static string Key(LanguageVersionCandidate version) => VersionMarketPlanner.Key(version.Language, version.BaseUrl);

    /// <summary>Scheme, host and port of the version (one robots.txt and sitemap each).</summary>
    private static string Authority(LanguageVersionCandidate version) => new Uri(version.BaseUrl).GetLeftPart(UriPartial.Authority);

    private static SamplePage Sample(ExtractedPageRecord page) =>
        new(page.Info.Url, VersionComparer.Sentences(page.Info.MainText), page.Info.HreflangGroup, page.Info.ProductIds);

    private MarketsAnalysisResult Result(
        Run run, Uri home, PlacesOfSaleResult places, MarketSignals? signals, IReadOnlyList<LanguageVersionCandidate> versions, VersionPlan? plan,
        VersionComparisonResult? comparison, MarketCatalog catalog)
    {
        var compared = comparison?.Versions.ToDictionary(v => v.Key) ?? [];
        var rows = versions.Select(v =>
        {
            var c = compared.GetValueOrDefault(Key(v));
            var count = plan?.Counts.GetValueOrDefault(Key(v));
            return new ShopLanguageRow
            {
                Language = v.Language,
                BaseUrl = v.BaseUrl,
                SwitchMethod = v.SwitchMethod,
                Source = v.Source,
                Status = v.Status,
                IsMain = v.IsMain,
                OwnTextShare = c?.OwnTextShare,
                LanguageShare = c?.LanguageShare ?? new Dictionary<string, double>(),
                Comparison = c is null ? null : Comparison(c),
                Counted = count?.Counted ?? false,
                ProductCount = count?.ProductCount,
                Codes = v.Codes.Concat(c?.Codes ?? []).Distinct().ToList(),
                Warnings = c?.Warnings ?? [],
                Evidence = v.Evidence,
                Scope = v.Scope,
            };
        }).ToList();
        var (summary, notices) = plan is null || versions.Count == 0
            ? (null, (IReadOnlyList<VersionSummary>)[])
            : MarketsSummary.Build(versions, plan, comparison, options.Value.Markets.CountedMinOwnShare);
        var marketRows = places.Countries.Select((c, i) => new ShopMarketRow(
            c.Country, c.Market, c.IsHome, c.Supported ? "suggested" : "unsupported", c.EvidenceLevel, "detected", c.Preselected,
            c.IsHome && places.HomeNeedsConfirmation,
            new MarketEvidence(c.Evidence, c.IsHome ? places.HomeBasis : null, c.RaisedBy, new InternalNote(c.InternalReason, i == 0 ? places.InternalUncertain : "")))).ToList();
        return new MarketsAnalysisResult
        {
            Site = home.AbsoluteUri,
            AnalyzedAt = time.GetUtcNow(),
            Model = run.Model,
            Signals = signals,
            HomeCountry = places.HomeCountry,
            HomeBasis = places.HomeBasis,
            HomeNeedsConfirmation = places.HomeNeedsConfirmation,
            Markets = marketRows,
            RejectedCountries = places.Rejected,
            QuotesVerified = places.QuotesVerified,
            QuotesDropped = places.QuotesDropped,
            Versions = rows,
            Summary = summary,
            Notices = notices,
            Details = versions.Where(v => v.Status != VersionStatus.Unsupported).Select(v =>
            {
                var c = compared.GetValueOrDefault(Key(v));
                return new VersionDetails(v.Language, v.BaseUrl, v.Status, c?.OwnTextShare, c is null ? null : Comparison(c),
                    v.Codes.Concat(c?.Codes ?? []).Distinct().ToList(), c?.Warnings ?? [], c?.UntranslatedExamples ?? []);
            }).ToList(),
            Plan = plan,
            PairingMode = comparison?.PairingMode,
            Pages = run.Pages.Values.DistinctBy(p => (p.Info.Url, p.Info.Language)).Select(p => new PageLanguageRow(p.Info.Url, p.Info.Language, p.Info.HreflangGroup)).ToList(),
            Codes = run.Codes.Concat(places.Codes).Distinct().ToList(),
            Warnings = run.Warnings,
            Usage = run.Usage with { Requests = run.Requests },
            Estimate = run.Estimate,
        };
    }

    private static VersionComparisonSummary Comparison(VersionComparison version) => new(
        version.Pairs.GroupBy(p => p.Kind).ToDictionary(g => g.Key, g => g.Count()),
        version.Pairs.GroupBy(p => p.Kind).SelectMany(g => g.Take(3)).ToList(),
        version.SentenceOverlapShare,
        version.MandatoryPagesDiffer,
        version.MandatoryPagesDifferUrls);

    /// <summary>State of one analysis.</summary>
    private sealed class Run
    {
        public List<string> Codes { get; } = [];

        public List<ScanWarning> Warnings { get; } = [];

        public Dictionary<string, ExtractedPageRecord> Pages { get; } = new(StringComparer.Ordinal);

        public MarketsUsage Usage { get; set; } = MarketsUsage.None;

        public string? Model { get; set; }

        public int Requests { get; set; }

        public MarketAnalysisEstimate? Estimate { get; set; }
    }
}
