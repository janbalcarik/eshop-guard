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
/// The comparison of versions on a shop like goodie.sk (2. 10. 2026): the sitemap names no alternates, the product pages do
/// (hreflang), products are marked only in microdata, and the other domain is named twice (hreflang cs-CZ, a switcher link).
/// Both versions show the same customer reviews in Czech under a product, and some descriptions of the Slovak version are in
/// Czech; the profile of the product template tells the description from the reviews.
/// </summary>
public sealed class PagePairsTests
{
    [Fact]
    public async Task TranslatedVersion_IsPairedByTheHreflangOfItsPages_AndCountsWithItsOwnTexts()
    {
        var result = await AnalyzeAsync(copied: false);

        var cz = Assert.Single(result.Versions, v => !v.IsMain);
        Assert.Equal(("cs-cz", "https://pair-shop.cz/"), (cz.Language, cz.BaseUrl));
        Assert.Equal(VersionSamplePlanner.ModeHreflang, result.PairingMode);
        Assert.Equal(20, result.SamplePlan!.Pairs.Count);
        Assert.Equal(100, result.SamplePlan.Urls.Count);
        Assert.True(cz.Comparison!.PairKinds[PairKinds.Translation] >= 20);
        Assert.Equal([PairKinds.Translation], cz.Comparison.PairKinds.Keys);
        Assert.All(cz.Comparison.Examples, e => Assert.True(e.SharedSentenceShare < 0.2, e.MainUrl));
        Assert.True(cz.OwnTextShare > 0.7, $"own {cz.OwnTextShare}");
        Assert.True(cz.Counted);
        Assert.True(result.Versions.Single(v => v.IsMain).OwnTextShare > 0.7);
    }

    [Fact]
    public async Task VersionCopyingTheTexts_HasPairsSharingTheirSentences_AndNoOwnTexts()
    {
        var result = await AnalyzeAsync(copied: true);

        var cz = Assert.Single(result.Versions, v => !v.IsMain);
        Assert.Equal(20, result.SamplePlan!.Pairs.Count);
        Assert.All(cz.Comparison!.Examples, e => Assert.True(e.SharedSentenceShare > 0.6, e.MainUrl));
        Assert.True(cz.OwnTextShare < 0.2, $"own {cz.OwnTextShare}");
        Assert.False(cz.Counted);
    }

    [Fact]
    public async Task SharedReviews_MakeATranslatedVersionLookPartlyCopied_WithoutAProfile()
    {
        var result = await AnalyzeAsync(new PairShopFetcher(copied: false, reviews: true));

        var cz = Assert.Single(result.Versions, v => !v.IsMain);
        Assert.Equal(ComparisonBases.MainText, cz.Comparison!.Basis);
        Assert.Equal(0, cz.Comparison.DescriptionPages);
        Assert.All(cz.Comparison.Examples, e => Assert.True(e.SharedSentenceShare > 0.4, $"{e.MainUrl} {e.SharedSentenceShare}"));
        Assert.True(cz.OwnTextShare < 0.6, $"own {cz.OwnTextShare}");
        Assert.Empty(result.ProfilesCreated);
    }

    [Fact]
    public async Task WithProfiles_OnlyTheDescriptionsAreCompared_AndTheReviewsDoNotCount()
    {
        var profileModel = new ProductTemplateModel();
        var result = await AnalyzeAsync(new PairShopFetcher(copied: false, reviews: true), VersionProfileMode.Create, profileModel);

        var created = Assert.Single(result.ProfilesCreated);
        Assert.Equal("pair-shop.sk#1", created);
        Assert.Single(profileModel.Requests);
        Assert.Equal(new MarketsUsage(1, 0, 0, 0, 0.05m, 0), result.ProfileUsage);
        Assert.Equal(0.05m, result.Estimate!.ProfilesUsd);
        var cz = Assert.Single(result.Versions, v => !v.IsMain);
        Assert.Equal(ComparisonBases.Description, cz.Comparison!.Basis);
        Assert.True(cz.Comparison.DescriptionPages >= 20, $"pages {cz.Comparison.DescriptionPages}");
        Assert.Equal([PairKinds.Translation], cz.Comparison.PairKinds.Keys);
        Assert.All(cz.Comparison.Examples, e => Assert.Equal(0, e.SharedSentenceShare));
        Assert.True(cz.OwnTextShare > 0.95, $"own {cz.OwnTextShare}");
        Assert.Equal(1.0, cz.Comparison.OwnProductShare);
        Assert.Equal(0, cz.Comparison.ForeignTextProducts);
        Assert.True(cz.Counted);
        Assert.Equal(ComparisonBases.Description, result.Versions.Single(v => v.IsMain).Comparison!.Basis);
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
        Assert.Equal(ComparisonBases.Description, cz.Comparison!.Basis);
        Assert.True(cz.OwnTextShare > 0.95, $"own {cz.OwnTextShare}");
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
        Assert.Equal(ComparisonBases.MainText, Assert.Single(result.Versions, v => !v.IsMain).Comparison!.Basis);
    }

    [Fact]
    public async Task CopiedDescriptions_WithProfiles_AreStillNoOwnTexts()
    {
        var result = await AnalyzeAsync(new PairShopFetcher(copied: true, reviews: true), VersionProfileMode.Create, new ProductTemplateModel());

        var cz = Assert.Single(result.Versions, v => !v.IsMain);
        Assert.Equal(ComparisonBases.Description, cz.Comparison!.Basis);
        Assert.Equal([PairKinds.Identical], cz.Comparison.PairKinds.Keys);
        Assert.True(cz.OwnTextShare < 0.05, $"own {cz.OwnTextShare}");
        Assert.Equal(0.0, cz.Comparison.OwnProductShare);
        Assert.False(cz.Counted);
    }

    [Fact]
    public async Task CzechDescriptionsInTheSlovakVersion_AreUntranslatedTexts_OfTheMainVersion()
    {
        var fetcher = new PairShopFetcher(copied: false, reviews: true, czechInMain: 3);
        var result = await AnalyzeAsync(fetcher, VersionProfileMode.Create, new ProductTemplateModel(), marketModel: new CzechAwareModel(fetcher.IsCzech));

        var sk = Assert.Single(result.Versions, v => v.IsMain);
        var pairedNumbers = result.SamplePlan!.Pairs.Select(p => Number(p.MainUrl)).ToList();
        var czechPaired = pairedNumbers.Count(n => n % 3 == 0);
        Assert.InRange(czechPaired, 1, pairedNumbers.Count - 1);
        Assert.Contains(VersionCodes.UntranslatedText, sk.Warnings);
        Assert.Equal((pairedNumbers.Count, czechPaired, "cs"), (sk.Comparison!.LabeledProducts, sk.Comparison.ForeignTextProducts, sk.Comparison.ForeignTextLanguage));
        Assert.Null(sk.Comparison.OwnProductShare);
        var notice = Assert.Single(result.Notices, n => n.Code == SummaryCodes.VersionsUntranslatedTexts && (string?)n.Params["language"] == "sk");
        Assert.Equal("cs", notice.Params["text_language"]);
        Assert.Equal(czechPaired, notice.Params["products"]);
        Assert.Equal(pairedNumbers.Count, notice.Params["of"]);
        Assert.Equal(Math.Round(czechPaired / (double)pairedNumbers.Count, 2), notice.Params["share"]);

        // A product with the Czech text in both versions is the same text, not a text of the Czech version.
        var cz = Assert.Single(result.Versions, v => !v.IsMain);
        Assert.DoesNotContain(VersionCodes.UntranslatedText, cz.Warnings);
        var kinds = cz.Comparison!.PairKinds;
        Assert.Equal([PairKinds.Identical, PairKinds.Translation], kinds.Keys.Order());
        Assert.Equal(Math.Round(kinds[PairKinds.Translation] / (double)kinds.Values.Sum(), 3), cz.Comparison.OwnProductShare);
    }

    private static int Number(string url) => int.Parse(url[(url.LastIndexOf('-') + 1)..], CultureInfo.InvariantCulture);

    private static Task<MarketsAnalysisResult> AnalyzeAsync(bool copied) => AnalyzeAsync(new PairShopFetcher(copied));

    private static async Task<MarketsAnalysisResult> AnalyzeAsync(
        PairShopFetcher fetcher, VersionProfileMode profiles = VersionProfileMode.None, IProfileModel? profileModel = null, IPageProfileStore? store = null,
        MarketEstimateConfirmation? confirm = null, IMarketModel? marketModel = null)
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
            new MarketsAnalysisRequest(new Uri("https://pair-shop.sk/")) { Profiles = profiles }, confirm, null, TestContext.Current.CancellationToken);
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
    private sealed class PairShopFetcher(bool copied, bool reviews = false, int czechInMain = 0) : IPageFetcher
    {
        private const int Products = 60;

        private readonly HashSet<string> czechSentences = Enumerable.Range(1, Products)
            .SelectMany(n => VersionComparer.Sentences(Czech(n) + "\n" + Reviews(n)))
            .ToHashSet(StringComparer.Ordinal);

        /// <summary>Whether a sentence of the shop is Czech (the fake model knows it).</summary>
        public bool IsCzech(string sentence) => czechSentences.Contains(sentence.Trim());

        public Task<FetchResponse> FetchAsync(Uri url, CancellationToken ct)
        {
            var sk = url.Host == "pair-shop.sk";
            var host = $"https://{url.Host}";
            string? body = url.AbsolutePath switch
            {
                "/robots.txt" => $"User-agent: *\nAllow: /\n\nSitemap: {host}/sitemap.xml\n",
                "/sitemap.xml" => $"""<?xml version="1.0"?><sitemapindex xmlns="http://www.sitemaps.org/schemas/sitemap/0.9"><sitemap><loc>{host}/sitemap-products.xml</loc></sitemap></sitemapindex>""",
                "/sitemap-products.xml" => """<?xml version="1.0"?><urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">"""
                    + string.Concat(Enumerable.Range(1, Products).Select(i => $"<url><loc>{host}/p/produkt-{i}</loc></url>")) + "</urlset>",
                "/" => Page(sk, "/", sk ? "Obchod s bylinkami." : "Obchod s bylinkami a čaji.", "<a href=\"https://pair-shop.cz/\">Česká verze</a>"),
                _ when url.AbsolutePath.StartsWith("/p/produkt-", StringComparison.Ordinal) => Product(sk, url.AbsolutePath),
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
