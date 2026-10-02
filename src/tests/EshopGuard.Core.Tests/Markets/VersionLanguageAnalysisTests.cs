using System.Globalization;
using System.Text;
using EshopGuard.Core.Crawl;
using EshopGuard.Core.Languages;
using EshopGuard.Core.Markets;
using EshopGuard.Core.Profiles;
using EshopGuard.Core.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace EshopGuard.Core.Tests;

/// <summary>
/// The analysis of versions on a shop like goodie.sk (2. 10. 2026): two domains with the same paths, products marked only in
/// microdata, the other domain named twice (hreflang cs-CZ, a switcher link). Both versions show the same customer reviews in
/// Czech under a product, and some descriptions of the Slovak version are in Czech; the profile of the product template tells
/// the description from the reviews. The texts of the versions are not compared (price for every country, 2. 10. 2026): the
/// model tells the language of the descriptions of 20 random products of each version.
/// </summary>
public sealed class VersionLanguageAnalysisTests
{
    private static readonly string[] BothMarkets = ["sk", "cz"];

    [Fact]
    public async Task TranslatedVersion_HasItsDescriptionsInItsLanguage_AndTheProductsOfBothCountriesCount()
    {
        var fetcher = new PairShopFetcher(copied: false);
        var result = await AnalyzeAsync(fetcher, marketModel: new CzechAwareModel(fetcher.IsCzech), markets: BothMarkets);

        var cz = Assert.Single(result.Versions, v => !v.IsMain);
        Assert.Equal(("cs-cz", "https://pair-shop.cz/"), (cz.Language, cz.BaseUrl));
        Assert.Equal(100, result.SamplePlan!.Urls.Count);
        Assert.Equal((20, 20, 0), (cz.DescriptionLanguages!.LabeledProducts, cz.DescriptionLanguages.TranslatedProducts, cz.DescriptionLanguages.ForeignTextProducts));
        Assert.Equal(1.0, cz.TranslatedShare);
        Assert.Empty(cz.Warnings);
        Assert.Equal(VersionStatus.NeedsConfirmation, cz.Status);

        // Until the client confirms the other domain, Czechia is checked on the main version; both countries count.
        Assert.Equal([("sk", "sk", 60), ("cz", "sk", 60)], result.Plan!.ByMarket.Select(m => (m.Market, m.Language, m.ProductCount ?? 0)));
        Assert.Equal(120, result.Plan.CountedProducts);
        Assert.Equal(VersionCodes.OtherDomainNeedsConfirmation, result.Summary!.Code);
        var byMarket = Assert.Single(result.Notices, n => n.Code == SummaryCodes.VersionsByMarket);
        Assert.Equal(new Dictionary<string, string> { ["sk"] = "sk", ["cz"] = "sk" }, (Dictionary<string, string>)byMarket.Params["markets"]);

        // Confirmed (change 10): Czechia is checked on its own version, with its products from the sample, nothing downloaded.
        var confirmed = VersionMarketPlanner.Plan(
            result.Plan.Versions.Select(v => v.Status == VersionStatus.NeedsConfirmation ? v with { Status = VersionStatus.Active } : v).ToList(),
            BothMarkets, MarketCatalog.From(TestTexts.Catalog), result.Plan.ProductCounts);
        Assert.Equal([("sk", "sk", 60), ("cz", "cs-cz", 60)], confirmed.ByMarket.Select(m => (m.Market, m.Language, m.ProductCount ?? 0)));
        Assert.Equal(120, confirmed.CountedProducts);
    }

    [Fact]
    public async Task VersionCopyingTheTexts_HasUntranslatedDescriptions_AndStillCountsForItsCountry()
    {
        var fetcher = new PairShopFetcher(copied: true, reviews: true);
        var result = await AnalyzeAsync(fetcher, VersionProfileMode.Create, new ProductTemplateModel(), marketModel: new CzechAwareModel(fetcher.IsCzech), markets: BothMarkets);

        var cz = Assert.Single(result.Versions, v => !v.IsMain);
        Assert.Equal(SentenceSources.Description, cz.DescriptionLanguages!.Basis);
        Assert.Equal((20, 0, 20, "sk"), (cz.DescriptionLanguages.LabeledProducts, cz.DescriptionLanguages.TranslatedProducts, cz.DescriptionLanguages.ForeignTextProducts, cz.DescriptionLanguages.ForeignTextLanguage));
        Assert.Equal(0.0, cz.TranslatedShare);
        Assert.Equal([VersionCodes.UntranslatedText], cz.Warnings);
        Assert.Equal(120, result.Plan!.CountedProducts);
        var notice = Assert.Single(result.Notices, n => n.Code == SummaryCodes.VersionsUntranslatedTexts);
        Assert.Equal(("cs-cz", 20, 20, "sk"), ((string)notice.Params["language"], (int)notice.Params["products"], (int)notice.Params["of"], (string)notice.Params["text_language"]));
    }

    [Fact]
    public async Task SharedReviews_WithoutAProfile_TheLanguageRestsOnTheMainText()
    {
        var result = await AnalyzeAsync(new PairShopFetcher(copied: false, reviews: true));

        var cz = Assert.Single(result.Versions, v => !v.IsMain);
        Assert.Equal(SentenceSources.MainText, cz.DescriptionLanguages!.Basis);
        Assert.Equal(0, cz.DescriptionLanguages.DescriptionPages);
        Assert.Empty(result.ProfilesCreated);
    }

    [Fact]
    public async Task WithProfiles_TheLanguageIsToldFromTheDescriptions_WithoutTheReviews()
    {
        var profileModel = new ProductTemplateModel();
        var fetcher = new PairShopFetcher(copied: false, reviews: true);
        var result = await AnalyzeAsync(fetcher, VersionProfileMode.Create, profileModel, marketModel: new CzechAwareModel(fetcher.IsCzech));

        var created = Assert.Single(result.ProfilesCreated);
        Assert.Equal("pair-shop.sk#1", created);
        Assert.Single(profileModel.Requests);
        Assert.Equal(new MarketsUsage(1, 0, 0, 0, 0.05m, 0), result.ProfileUsage);
        Assert.Equal(0.05m, result.Estimate!.ProfilesUsd);
        var cz = Assert.Single(result.Versions, v => !v.IsMain);
        Assert.Equal(SentenceSources.Description, cz.DescriptionLanguages!.Basis);
        Assert.True(cz.DescriptionLanguages.DescriptionPages >= 20, $"pages {cz.DescriptionLanguages.DescriptionPages}");
        Assert.Equal(1.0, cz.TranslatedShare);

        // The Czech reviews under the Slovak descriptions are not taken for the language of the main version.
        var sk = Assert.Single(result.Versions, v => v.IsMain);
        Assert.Equal(SentenceSources.Description, sk.DescriptionLanguages!.Basis);
        Assert.Equal(1.0, sk.TranslatedShare);
        Assert.Empty(sk.Warnings);
    }

    [Fact]
    public async Task StoredProfiles_AreUsed_WithoutCallingTheModel()
    {
        var profileModel = new ProductTemplateModel();
        var store = new InMemoryPageProfileStore();
        await store.AddAsync(new PageProfile { Id = "pair-shop.sk#1", Site = "pair-shop.sk", Regions = ProductTemplateModel.Regions }, TestContext.Current.CancellationToken);

        var result = await AnalyzeAsync(new PairShopFetcher(copied: false, reviews: true), VersionProfileMode.Stored, profileModel, store);

        Assert.Empty(profileModel.Requests);
        Assert.Empty(result.ProfilesCreated);
        Assert.Equal(MarketsUsage.None, result.ProfileUsage);
        var cz = Assert.Single(result.Versions, v => !v.IsMain);
        Assert.Equal(SentenceSources.Description, cz.DescriptionLanguages!.Basis);
    }

    [Fact]
    public async Task ProfilesNotConfirmed_KeepTheMainText_AndSaySo()
    {
        var profileModel = new ProductTemplateModel();
        var asked = new List<MarketAnalysisEstimate>();
        var result = await AnalyzeAsync(new PairShopFetcher(copied: false, reviews: true), VersionProfileMode.Create, profileModel,
            confirm: (estimate, _) =>
            {
                asked.Add(estimate);
                return Task.FromResult(false);
            });

        Assert.Empty(profileModel.Requests);
        Assert.Contains(MarketCodes.ProfilesNotConfirmed, result.Codes);
        Assert.Equal(0.05m, Assert.Single(asked).ProfilesUsd);
        Assert.Equal(SentenceSources.MainText, Assert.Single(result.Versions, v => !v.IsMain).DescriptionLanguages!.Basis);
    }

    [Fact]
    public async Task CzechDescriptionsInTheSlovakVersion_AreUntranslatedTexts_OfTheMainVersion()
    {
        var fetcher = new PairShopFetcher(copied: false, reviews: true, czechInMain: 3);
        var result = await AnalyzeAsync(fetcher, VersionProfileMode.Create, new ProductTemplateModel(), marketModel: new CzechAwareModel(fetcher.IsCzech));

        var sk = Assert.Single(result.Versions, v => v.IsMain);
        var fragments = result.Details.Single(d => d.Language == "sk").LanguageFragments;
        var labeled = fragments.Select(f => f.PageUrl).Distinct().ToList();
        var czech = labeled.Count(u => Number(u) % 3 == 0);
        Assert.Equal(40, fragments.Count);
        Assert.Equal(20, labeled.Count);
        Assert.InRange(czech, 1, labeled.Count - 1);
        Assert.Contains(VersionCodes.UntranslatedText, sk.Warnings);
        Assert.Equal((20, 20 - czech, czech, "cs"),
            (sk.DescriptionLanguages!.LabeledProducts, sk.DescriptionLanguages.TranslatedProducts, sk.DescriptionLanguages.ForeignTextProducts, sk.DescriptionLanguages.ForeignTextLanguage));
        var notice = Assert.Single(result.Notices.Prepend(result.Summary!), n => n.Code == SummaryCodes.VersionsUntranslatedTexts && (string?)n.Params["language"] == "sk");
        Assert.Equal("cs", notice.Params["text_language"]);
        Assert.Equal(czech, notice.Params["products"]);
        Assert.Equal(20, notice.Params["of"]);
        Assert.Equal(Math.Round(czech / 20.0, 2), notice.Params["share"]);
        Assert.All(fragments.Where(f => f.Language == "cs"), f => Assert.Equal(0, Number(f.PageUrl) % 3));

        // The Czech version has Czech descriptions: no warning there.
        var cz = Assert.Single(result.Versions, v => !v.IsMain);
        Assert.DoesNotContain(VersionCodes.UntranslatedText, cz.Warnings);
        Assert.Equal(1.0, cz.TranslatedShare);
    }

    [Fact]
    public async Task WithoutAProductSitemap_ProductsAreFoundByTheStructureOfThePage_AndTheirNumberStaysUnknown()
    {
        // havlikovaapoteka.cz, freshlabels.sk (2. 10. 2026): one sitemap mixes products and categories, its name says nothing.
        var fetcher = new PairShopFetcher(copied: false, flatSitemap: true);
        var result = await AnalyzeAsync(fetcher, marketModel: new CzechAwareModel(fetcher.IsCzech), markets: BothMarkets);

        Assert.All(result.Versions, v =>
        {
            Assert.Null(v.ProductCount);
            Assert.Contains(VersionCodes.ProductCountUnknown, v.Codes);
            Assert.DoesNotContain(VersionCodes.SampleInsufficient, v.Codes);
            Assert.Equal(20, v.DescriptionLanguages!.LabeledProducts);
            Assert.Equal(1.0, v.TranslatedShare);
        });
        var products = result.SamplePlan!.Urls.Where(u => u.Kind == VersionSamplePlanner.Product).ToList();
        Assert.All(products, u => Assert.Contains("/p/produkt-", u.Url, StringComparison.Ordinal));
        Assert.Equal([20, 20], products.GroupBy(u => u.VersionKey).Select(g => g.Count()));
        Assert.True(result.Plan!.ProductCountUnknown);

        // At most markets.product_probe_pages pages of each host, none downloaded twice.
        foreach (var host in new[] { "pair-shop.sk", "pair-shop.cz" })
        {
            var pages = fetcher.Fetched.Where(u => u.Host == host && u.AbsolutePath.Length > 3 && u.AbsolutePath[2] == '/').ToList();
            Assert.InRange(pages.Count, 20, 40);
            Assert.Equal(pages.Count, pages.Distinct().Count());
        }
    }

    [Fact]
    public async Task VersionOnADomainOfAnUnsupportedLanguage_IsUnsupported_AndTakesNoPartOfTheSample()
    {
        var result = await AnalyzeAsync(new PairShopFetcher(copied: false, italian: true));

        var it = Assert.Single(result.Versions, v => v.BaseUrl == "https://pair-shop.it/");
        Assert.Equal(("it", VersionStatus.Unsupported), (it.Language, it.Status));
        Assert.Null(it.Scope);
        Assert.DoesNotContain(result.SamplePlan!.Urls, u => u.Url.StartsWith("https://pair-shop.it/", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Notices.Concat(result.Summary is { } summary ? [summary] : []),
            n => n.Params.Values.OfType<string>().Any(v => v.Contains("pair-shop.it", StringComparison.Ordinal)));
        Assert.Contains(result.Plan!.NotChecked, v => v.BaseUrl == "https://pair-shop.it/" && v.Code == "version_unsupported");
    }

    private static int Number(string url) => int.Parse(url[(url.LastIndexOf('-') + 1)..], CultureInfo.InvariantCulture);

    private static async Task<MarketsAnalysisResult> AnalyzeAsync(
        PairShopFetcher fetcher, VersionProfileMode profiles = VersionProfileMode.None, IProfileModel? profileModel = null, IPageProfileStore? store = null,
        MarketEstimateConfirmation? confirm = null, IMarketModel? marketModel = null, IReadOnlyList<string>? markets = null)
    {
        await using var provider = TestServices.Create(fetcher, register: s =>
        {
            s.AddSingleton(marketModel ?? new MockMarketModel());
            if (profileModel is not null)
            {
                s.AddSingleton(profileModel);
            }

            if (store is not null)
            {
                s.AddSingleton(store);
            }
        });
        return await provider.GetRequiredService<IMarketsAnalyzer>().AnalyzeAsync(
            new MarketsAnalysisRequest(new Uri("https://pair-shop.sk/")) { Profiles = profiles, ActiveMarkets = markets }, confirm, null, TestContext.Current.CancellationToken);
    }

    /// <summary>The profile model: the product template has a description (<c>.popis</c>) and customer reviews (<c>.recenzie</c>).</summary>
    private sealed class ProductTemplateModel : IProfileModel
    {
        public static List<ProfileRegion> Regions { get; } =
        [
            new() { Role = "header", Action = ProfileRegion.Check, Selector = "header" },
            new() { Role = "footer", Action = ProfileRegion.Check, Selector = "footer" },
            new() { Role = "product_title", Action = ProfileRegion.Check, Selector = "h1" },
            new() { Role = "main_description", Action = ProfileRegion.Check, Selector = ".popis" },
            new() { Role = "price", Action = ProfileRegion.Check, Selector = ".cena" },
            new() { Role = "reviews", Action = ProfileRegion.Check, Selector = ".recenzie" },
        ];

        public List<IReadOnlyList<(string Url, string Outline)>> Requests { get; } = [];

        public bool IsAvailable => true;

        public string? UnavailableReason => null;

        public decimal EstimateUsd(IReadOnlyList<string> outlines) => 0.05m;

        public Task<ProfileAnswer> AskAsync(IReadOnlyList<(string Url, string Outline)> samples, CancellationToken ct)
        {
            Requests.Add(samples);
            return Task.FromResult(new ProfileAnswer { Regions = Regions, Model = "fake", CostUsd = 0.05m });
        }
    }

    /// <summary>The fake market model, which knows the language of the sentences of the shop (Czech or Slovak) like the real one would.</summary>
    private sealed class CzechAwareModel(Func<string, bool> isCzech) : IMarketModel
    {
        private readonly MockMarketModel inner = new();

        public string? UnavailableReason => inner.UnavailableReason;

        public async Task<MarketModelResponse> AskAsync(MarketModelRequest request, CancellationToken ct)
        {
            var response = await inner.AskAsync(request, ct);
            if (request.Call != MarketCall.Language)
            {
                return response;
            }

            var texts = request.Input.Split('\n')
                .Select(l => (Line: l, Dot: l.IndexOf(". ", StringComparison.Ordinal)))
                .Where(x => x.Dot > 0 && int.TryParse(x.Line[..x.Dot], out _))
                .ToDictionary(x => int.Parse(x.Line[..x.Dot], CultureInfo.InvariantCulture), x => x.Line[(x.Dot + 2)..]);
            var labels = System.Text.Json.Nodes.JsonNode.Parse(response.Json)!["labels"]!.AsArray();
            foreach (var label in labels)
            {
                label!["language"] = isCzech(texts[label["id"]!.GetValue<int>()]) ? "cs" : "sk";
            }

            return response with { Json = new System.Text.Json.Nodes.JsonObject { ["labels"] = labels.DeepClone() }.ToJsonString() };
        }
    }

    /// <summary>pair-shop.sk and pair-shop.cz with 60 products each, the same paths, hreflang only on the pages.</summary>
    private sealed class PairShopFetcher(bool copied, bool reviews = false, int czechInMain = 0, bool italian = false, bool flatSitemap = false) : IPageFetcher
    {
        private const int Products = 60;

        private const int Categories = 30;

        /// <summary>Every address asked for, in order.</summary>
        public System.Collections.Concurrent.ConcurrentQueue<Uri> Fetched { get; } = new();

        private readonly HashSet<string> czechSentences = Enumerable.Range(1, Products)
            .SelectMany(n => VersionLanguages.Sentences(Czech(n) + "\n" + Reviews(n)))
            .ToHashSet(StringComparer.Ordinal);

        /// <summary>Whether a sentence of the shop is Czech (the fake model knows it).</summary>
        public bool IsCzech(string sentence) => czechSentences.Contains(sentence.Trim());

        public Task<FetchResponse> FetchAsync(Uri url, CancellationToken ct)
        {
            Fetched.Enqueue(url);
            var sk = url.Host == "pair-shop.sk";
            var host = $"https://{url.Host}";
            if (url.Host == "pair-shop.it" && url.AbsolutePath == "/")
            {
                // Linked only from the home page, no hreflang: the language is known only from the page.
                var italianHome = """<!DOCTYPE html><html lang="it"><head><meta charset="utf-8"><title>Erbe</title></head><body><main><p>Negozio di erbe e tè dalle montagne.</p></main></body></html>""";
                return Task.FromResult(new FetchResponse { Url = url, StatusCode = 200, MediaType = "text/html", Charset = "utf-8", Body = Encoding.UTF8.GetBytes(italianHome) });
            }

            string? body = url.AbsolutePath switch
            {
                "/robots.txt" => $"User-agent: *\nAllow: /\n\nSitemap: {host}/sitemap.xml\n",
                "/sitemap.xml" when flatSitemap => """<?xml version="1.0"?><urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">"""
                    + string.Concat(Enumerable.Range(1, Products).Select(i => $"<url><loc>{host}/p/produkt-{i}</loc></url>"))
                    + string.Concat(Enumerable.Range(1, Categories).Select(i => $"<url><loc>{host}/k/kategoria-{i}</loc></url>"))
                    + "</urlset>",
                "/sitemap.xml" => $"""<?xml version="1.0"?><sitemapindex xmlns="http://www.sitemaps.org/schemas/sitemap/0.9"><sitemap><loc>{host}/sitemap-products.xml</loc></sitemap></sitemapindex>""",
                "/sitemap-products.xml" => """<?xml version="1.0"?><urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">"""
                    + string.Concat(Enumerable.Range(1, Products).Select(i => $"<url><loc>{host}/p/produkt-{i}</loc></url>")) + "</urlset>",
                "/" => Page(sk, "/", sk ? "Obchod s bylinkami." : "Obchod s bylinkami a čaji.",
                    "<a href=\"https://pair-shop.cz/\">Česká verze</a>" + (italian ? "<a href=\"https://pair-shop.it/\">Italiano</a>" : "")),
                _ when url.AbsolutePath.StartsWith("/p/produkt-", StringComparison.Ordinal) => Product(sk, url.AbsolutePath),
                _ when url.AbsolutePath.StartsWith("/k/kategoria-", StringComparison.Ordinal) => Page(sk, url.AbsolutePath,
                    $"<h1>Kategória {url.AbsolutePath["/k/kategoria-".Length..]}</h1><p>{(sk ? "Bylinky a čaje z našej ponuky." : "Bylinky a čaje z naší nabídky.")}</p>", ""),
                _ => null,
            };
            var type = url.AbsolutePath.EndsWith(".xml", StringComparison.Ordinal) ? "application/xml" : url.AbsolutePath == "/robots.txt" ? "text/plain" : "text/html";
            return Task.FromResult(body is null
                ? new FetchResponse { Url = url, StatusCode = 404 }
                : new FetchResponse { Url = url, StatusCode = 200, MediaType = type, Charset = "utf-8", Body = Encoding.UTF8.GetBytes(body) });
        }

        private string Product(bool sk, string path)
        {
            var n = int.Parse(path["/p/produkt-".Length..], CultureInfo.InvariantCulture);
            var czechHere = sk ? czechInMain > 0 && n % czechInMain == 0 : !copied || (czechInMain > 0 && n % czechInMain == 0);
            var text = czechHere ? Czech(n) : Slovak(n);
            var shared = reviews ? $"""<div class="recenzie"><h2>Hodnocení zákazníků</h2><p>{Reviews(n)}</p></div>""" : "";
            var product = $"""<div itemscope itemtype="http://schema.org/Product"><h1 itemprop="name">Čaj {n}</h1><div class="popis"><p>{text}</p></div><p class="cena">Cena: {n},90 {(sk ? "€" : "Kč")}</p>{shared}</div>""";
            return Page(sk, path, product, "");
        }

        private static string Slovak(int n) =>
            $"Čaj číslo {n} pochádza z horských lúk. Lístky zbierame ručne počas leta. Sušíme ich v tieni na drevených lieskach. "
            + $"Pripravíte ho za päť minút vo vriacej vode. Chutí aj vychladený s citrónom. Balenie číslo {n} vystačí na mesiac. "
            + $"Skladujte v suchu a chlade. Hodí sa na pokojný večer po práci číslo {n}. Neobsahuje pridaný cukor ani arómy.";

        private static string Czech(int n) =>
            $"Čaj číslo {n} pochází z horských luk. Lístky sbíráme ručně během léta. Sušíme je ve stínu na dřevěných lískách. "
            + $"Připravíte ho za pět minut ve vroucí vodě. Chutná i vychlazený s citronem. Balení číslo {n} vystačí na měsíc. "
            + $"Skladujte v suchu a chladu. Hodí se na klidný večer po práci číslo {n}. Neobsahuje přidaný cukr ani aromata.";

        /// <summary>Customer reviews in Czech, the same on both domains (goodie.sk shows the reviews of both shops).</summary>
        private static string Reviews(int n) =>
            $"Čaj {n} piju každý večer a chutná mi. Dorazil rychle a dobře zabalený. Objednám znovu, děkuji za čaj číslo {n}. "
            + $"Manželce chutná víc s medem. Vůně je silná a příjemná. Za tu cenu je čaj {n} výborný. "
            + $"Balíček vydržel déle, než jsem čekala. Doporučuji všem milovníkům bylinek. Hodnotím čaj číslo {n} pěti hvězdičkami. "
            + $"Jen víčko by mohlo lépe těsnit. Kupuji ho už třetí rok. S čajem {n} jsem spokojený.";

        private static string Page(bool sk, string path, string main, string extra) => $$"""
            <!DOCTYPE html><html lang="{{(sk ? "sk" : "cs")}}"><head><meta charset="utf-8"><title>Bylinky</title>
            <meta property="og:type" content="article">
            <link rel="alternate" hreflang="sk-SK" href="https://pair-shop.sk{{path}}">
            <link rel="alternate" hreflang="cs-CZ" href="https://pair-shop.cz{{path}}">
            </head><body><header><a href="/">Bylinky</a>{{extra}}</header><main>{{main}}</main><footer><p>Bylinky s.r.o.</p></footer></body></html>
            """;
    }
}
