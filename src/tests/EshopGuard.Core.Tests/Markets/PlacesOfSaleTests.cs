using System.Net;
using System.Text;
using System.Text.Json;
using EshopGuard.Core.Fix;
using EshopGuard.Core.Languages;
using EshopGuard.Core.Markets;
using EshopGuard.Core.Options;
using Microsoft.Extensions.Logging;

namespace EshopGuard.Core.Tests;

/// <summary>Places of sale: the call of the model, the pick of pages, verified quotes, strength of evidence, home country (change 7, group 3).</summary>
public sealed class PlacesOfSaleTests
{
    private static readonly MarketCatalog Catalog = MarketCatalog.From(TestTexts.Catalog);

    private static readonly MarketSignals Signals = new()
    {
        Site = "https://shop.sk/",
        HtmlLang = "sk",
        Hreflang = [new HreflangSignal("cs", "https://shop.sk/cz/", "page")],
        Currencies = ["EUR"],
        Tld = "sk",
        HomeLinks = [new LinkInfo("Doprava", "https://shop.sk/doprava"), new LinkInfo("Kontakt", "https://shop.sk/kontakt"), new LinkInfo("Blog", "https://shop.sk/blog")],
        FooterLinks = [new LinkInfo("Obchodné podmienky", "https://shop.sk/obchodne-podmienky"), new LinkInfo("O nás", "https://shop.sk/o-nas"), new LinkInfo("Facebook", "https://facebook.com/shop")],
    };

    private static readonly Dictionary<string, string> Pages = new()
    {
        ["https://shop.sk/doprava"] = "Doprava\nTovar zasielame aj do zahraničia.\nDoprava do Českej republiky stojí 5,90 €.\nDoručujeme do celej EÚ.",
        ["https://shop.sk/obchodne-podmienky"] = "Predávajúci: Shop s.r.o., Hlavná 1, 811 01 Bratislava, Slovenská republika.",
    };

    [Fact]
    public async Task OpenAiClient_SendsTheReasoningEffortOfTheRequest_AndNeverLogsTheKey()
    {
        var bodies = new List<string>();
        var answer = """{"status":"completed","model":"gpt-6.1-sol","output":[{"type":"message","content":[{"type":"output_text","text":"{\"urls\":[],\"reason\":\"\"}"}]}],"usage":{"input_tokens":100,"output_tokens":10}}""";
        var handler = new Handler(async request =>
        {
            bodies.Add(await request.Content!.ReadAsStringAsync());
            return bodies.Count == 3
                ? new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("""{"error":"bad"}""") }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(answer, Encoding.UTF8, "application/json") };
        });
        var logger = new ListLogger<OpenAiRewriteClient>();
        var client = new OpenAiRewriteClient(new Factory(handler),
            Microsoft.Extensions.Options.Options.Create(new RewriteOptions { ApiKey = "sk-secret-key", ReasoningEffort = "medium", MaxRetries = 1 }), logger);
        var request = new RewriteRequest { SharedPart = "i", PagePart = "p", PromptVersion = "v", Schema = MarketPrompts.PickSchema, ReasoningEffort = "low" };
        var ct = TestContext.Current.CancellationToken;

        await client.RewriteAsync(request, ct);
        await client.RewriteAsync(new RewriteRequest { SharedPart = "i", PagePart = "p", PromptVersion = "v", Schema = MarketPrompts.PickSchema }, ct);
        await Assert.ThrowsAsync<RewriteApiException>(() => client.RewriteAsync(request, ct));

        Assert.Equal("low", Effort(bodies[0]));
        Assert.Equal("medium", Effort(bodies[1]));
        Assert.DoesNotContain(logger.Messages, m => m.Contains("sk-secret-key", StringComparison.Ordinal));
        Assert.DoesNotContain(bodies, b => b.Contains("sk-secret-key", StringComparison.Ordinal));

        static string Effort(string body) => JsonDocument.Parse(body).RootElement.GetProperty("reasoning").GetProperty("effort").GetString()!;
    }

    [Fact]
    public void Estimate_IsCharactersBy3Point2AtTheRewritePrices()
    {
        var rewrite = new RewriteOptions { InputUsdPerMillion = 2.00m, OutputUsdPerMillion = 10.00m };
        var markets = new MarketsOptions();

        var pick = MarketAnalysisEstimate.Pick(3_200, rewrite, markets);
        var sales = MarketAnalysisEstimate.Sales(38_000, rewrite, markets);

        var pickInput = (long)Math.Ceiling((MarketPrompts.PickInstructions.Length + 3_200) / 3.2);
        Assert.Equal((1, pickInput, 800L), (pick.Calls, pick.InputTokens, pick.OutputTokens));
        Assert.Equal(pickInput * 2.00m / 1_000_000m + 800 * 10.00m / 1_000_000m, pick.CostUsd);
        var salesInput = (long)Math.Ceiling((MarketPrompts.SalesInstructions.Length + 2_000 + 38_000) / 3.2);
        Assert.Equal((salesInput, 4_000L), (sales.InputTokens, sales.OutputTokens));
        Assert.InRange(sales.CostUsd, 0.03m, 0.08m);
        var before = MarketAnalysisEstimate.Before(3_200, versions: 2, rewrite, markets);
        Assert.Equal(4, before.Calls);
    }

    [Fact]
    public void Selector_UsesAtMost4UrlsOfTheList()
    {
        string[] answer =
        [
            "https://shop.sk/doprava", "https://shop.sk/vymyslena-stranka", "https://shop.sk/obchodne-podmienky",
            "https://shop.sk/kontakt", "https://shop.sk/o-nas", "https://shop.sk/blog",
        ];

        var (picked, rejected) = SalesPageSelector.Validate(answer, Signals, new Uri("https://shop.sk/"), 4);

        Assert.Equal(["https://shop.sk/doprava", "https://shop.sk/obchodne-podmienky", "https://shop.sk/kontakt", "https://shop.sk/o-nas"], picked);
        Assert.Contains("https://shop.sk/vymyslena-stranka", rejected);
        Assert.Contains("https://shop.sk/blog", rejected);
    }

    [Fact]
    public void Selector_FillsUpWithLegalPagesOfTheSample()
    {
        // As on vegis.sk: the model picked nothing about delivery from the home page; the sample downloaded the terms.
        var pages = SalesPageSelector.WithFallback(["https://shop.sk/kontakt"], [], [new Uri("https://shop.sk/obchodne-podmienky"), new Uri("https://shop.sk/kontakt")], 4);

        Assert.Equal([("https://shop.sk/kontakt", "model"), ("https://shop.sk/obchodne-podmienky", "legal")], pages.Select(p => (p.Url, p.Source)));
    }

    [Fact]
    public void Selector_NeverOffersALinkOfAnotherSite()
    {
        var (picked, _) = SalesPageSelector.Validate(["https://facebook.com/shop"], Signals, new Uri("https://shop.sk/"), 4);

        Assert.Empty(picked);
    }

    [Fact]
    public void Verifier_QuoteOnItsPage_IsKept()
    {
        var answer = Answer(new CountryAnswer("CZ", "delivery", [new ModelQuote("„Doprava do Českej   republiky stojí 5,90 €.“", "https://shop.sk/doprava")], "price"));

        var verified = QuoteVerifier.Verify(answer, Pages, Signals);

        var country = Assert.Single(verified.Countries);
        var quote = Assert.Single(country.Evidence);
        Assert.Equal(("https://shop.sk/doprava", false), (quote.Source, quote.SourceCorrected));
        Assert.Equal((1, 0), (verified.QuotesVerified, verified.QuotesDropped));
    }

    [Fact]
    public void Verifier_MadeUpQuote_IsDroppedAndTheCountryRejected()
    {
        var answer = Answer(new CountryAnswer("HU", "delivery", [new ModelQuote("Szállítás Magyarországra 3 nap.", "https://shop.sk/doprava")], "made up"));

        var verified = QuoteVerifier.Verify(answer, Pages, Signals);

        Assert.Empty(verified.Countries);
        Assert.Equal(new RejectedCountry("HU", QuoteVerifier.NoVerifiedEvidence), Assert.Single(verified.Rejected));
        Assert.Equal(1, verified.QuotesDropped);
    }

    [Fact]
    public void Verifier_QuoteOnAnotherPage_GetsTheCorrectedSource()
    {
        var answer = Answer(new CountryAnswer("CZ", "delivery", [new ModelQuote("Doprava do Českej republiky stojí 5,90 €.", "https://shop.sk/obchodne-podmienky")], ""));

        var quote = Assert.Single(Assert.Single(QuoteVerifier.Verify(answer, Pages, Signals).Countries).Evidence);

        Assert.Equal(("https://shop.sk/doprava", true), (quote.Source, quote.SourceCorrected));
    }

    [Fact]
    public void Verifier_SignalMustExist()
    {
        var answer = Answer(
            new CountryAnswer("PL", "strong", [new ModelQuote("signal: hreflang=pl", "signal")], ""),
            new CountryAnswer("CZ", "strong", [new ModelQuote("hreflang=cs", "signal")], ""));

        var verified = QuoteVerifier.Verify(answer, Pages, Signals);

        Assert.Equal(["CZ"], verified.Countries.Select(c => c.Country));
        Assert.Contains(verified.Rejected, r => r.Country == "PL");
    }

    [Fact]
    public void Classifier_TableOfShippingPrices_IsDeliveryNeverStrong()
    {
        // As freshlabels: a table of shipping prices to 26 countries, own domains for SK and CZ.
        var table = new[] { "AT", "BE", "BG", "HR", "CY", "DK", "EE", "FI", "FR", "DE", "GR", "HU", "IE", "IT", "LV", "LT", "LU", "MT", "NL", "PL", "PT", "RO", "SI", "ES", "SE", "CZ" };
        var sales = new VerifiedSales
        {
            Countries = table.Select(c => new VerifiedCountry(c, "delivery", [new VerifiedQuote($"{c} 9,90 €", "https://freshlabels.sk/doprava")], "")).ToList(),
            DeliveryTerms = new VerifiedQuote("Doprava", "https://freshlabels.sk/doprava"),
        };
        LanguageVersionCandidate[] versions =
        [
            Version("sk", "https://freshlabels.sk/", SwitchMethods.Path, isMain: true),
            Version("cs", "https://freshlabels.cz/", SwitchMethods.Domain),
        ];

        var result = PlacesOfSaleClassifier.Classify(sales with { HomeCountry = null }, Catalog, new Uri("https://freshlabels.sk/"), versions);

        Assert.Equal(EvidenceLevels.Strong, Country(result, "SK").EvidenceLevel);
        Assert.Equal(EvidenceLevels.Strong, Country(result, "CZ").EvidenceLevel);
        Assert.True(Country(result, "CZ").Preselected);
        Assert.Equal(2, result.Countries.Count(c => c.EvidenceLevel == EvidenceLevels.Strong));
        Assert.Equal(EvidenceLevels.Delivery, Country(result, "AT").EvidenceLevel);
        var poland = Country(result, "PL");
        Assert.False(poland.Supported);
        Assert.False(poland.Preselected);
    }

    [Fact]
    public void Classifier_GeneralDeliveryToTheEu_IsGenericAndNotPreselected()
    {
        var euWide = new VerifiedQuote("Doručujeme do celej EÚ.", "https://shop.sk/doprava");
        var sales = new VerifiedSales
        {
            HomeCountry = "SK",
            HomeEvidence = [new VerifiedQuote("Hlavná 1, 811 01 Bratislava", "https://shop.sk/obchodne-podmienky")],
            Countries = [new VerifiedCountry("CZ", "delivery", [euWide], "")],
            EuWideDelivery = euWide,
            DeliveryTerms = euWide,
        };

        var cz = Country(PlacesOfSaleClassifier.Classify(sales, Catalog, new Uri("https://shop.sk/"), []), "CZ");

        Assert.Equal(EvidenceLevels.Generic, cz.EvidenceLevel);
        Assert.False(cz.Preselected);
        Assert.True(cz.Supported);
    }

    [Fact]
    public void Classifier_OwnVersionRaisesTheStrength()
    {
        var sales = new VerifiedSales
        {
            Countries = [new VerifiedCountry("SK", "delivery", [new VerifiedQuote("Doprava na Slovensko 4,90 EUR", "https://shop.cz/doprava")], "")],
            DeliveryTerms = new VerifiedQuote("Doprava na Slovensko 4,90 EUR", "https://shop.cz/doprava"),
        };

        var sk = Country(PlacesOfSaleClassifier.Classify(sales, Catalog, new Uri("https://shop.cz/"), [Version("sk", "https://shop.cz/sk/", SwitchMethods.Path)]), "SK");

        Assert.Equal((EvidenceLevels.Strong, "version=sk"), (sk.EvidenceLevel, sk.RaisedBy));
        Assert.True(sk.Preselected);
    }

    [Fact]
    public void Classifier_VersionWaitingForConfirmation_DoesNotRaise()
    {
        var sales = new VerifiedSales
        {
            Countries = [new VerifiedCountry("SK", "delivery", [new VerifiedQuote("Doprava na Slovensko", "https://goodie.cz/doprava")], "")],
            DeliveryTerms = new VerifiedQuote("Doprava na Slovensko", "https://goodie.cz/doprava"),
        };
        var pending = Version("sk", "https://goodie.sk/", SwitchMethods.Domain) with { Status = VersionStatus.NeedsConfirmation };

        Assert.Equal(EvidenceLevels.Delivery, Country(PlacesOfSaleClassifier.Classify(sales, Catalog, new Uri("https://goodie.cz/"), [pending]), "SK").EvidenceLevel);
    }

    [Fact]
    public void Home_SlovakCompanyOnACzechDomain()
    {
        // As panakeia.cz: the address of the operator is Slovak.
        var sales = new VerifiedSales
        {
            HomeCountry = "SK",
            HomeEvidence = [new VerifiedQuote("Panakeia s.r.o., Mlynská 10, 040 01 Košice", "https://panakeia.cz/kontakt")],
            DeliveryTerms = new VerifiedQuote("Doprava", "https://panakeia.cz/doprava"),
        };

        var result = PlacesOfSaleClassifier.Classify(sales, Catalog, new Uri("https://panakeia.cz/"), []);

        Assert.Equal(("SK", HomeBases.Quote, false), (result.HomeCountry, result.HomeBasis, result.HomeNeedsConfirmation));
        Assert.Equal("https://panakeia.cz/kontakt", Assert.Single(Country(result, "SK").Evidence).Source);
        Assert.True(Country(result, "SK").IsHome);
    }

    [Fact]
    public void Home_FromTheDomainToConfirm()
    {
        // As goodie.cz: no address of the operator on the picked pages.
        var result = PlacesOfSaleClassifier.Classify(new VerifiedSales(), Catalog, new Uri("https://www.goodie.cz/"), []);

        Assert.Equal(("CZ", HomeBases.DomainTld, true), (result.HomeCountry, result.HomeBasis, result.HomeNeedsConfirmation));
        Assert.Contains(MarketCodes.HomeCountryFromDomain, result.Codes);
        Assert.Contains(MarketCodes.DeliveryTermsNotFound, result.Codes);
    }

    [Fact]
    public void Home_DomainWithoutACountry()
    {
        var result = PlacesOfSaleClassifier.Classify(new VerifiedSales(), Catalog, new Uri("https://shop.com/"), []);

        Assert.Null(result.HomeCountry);
        Assert.Contains(MarketCodes.HomeCountryUnknown, result.Codes);
        Assert.Empty(result.Countries);
    }

    [Fact]
    public void Reevaluate_PolandBecomesSupportedWhenItsMarketIsAdded()
    {
        var sales = new VerifiedSales
        {
            Countries = [new VerifiedCountry("PL", "strong", [new VerifiedQuote("Ceny w PLN", "https://shop.sk/pl/")], "")],
            DeliveryTerms = new VerifiedQuote("Doprava", "https://shop.sk/doprava"),
        };
        var result = PlacesOfSaleClassifier.Classify(sales, Catalog, new Uri("https://shop.sk/"), []);
        Assert.False(Country(result, "PL").Supported);

        // A line of data and a rule set for Poland, no code: the stored result is evaluated again without a download.
        var jurisdictions = TestTexts.Catalog.Jurisdictions.Items.ToDictionary(j => j.Key, j => j.Value);
        jurisdictions["pl"] = new Rules.JurisdictionInfo { LawLanguage = "pl", Country = "PL", Language = "pl", ReadableLanguages = ["pl"], Tlds = ["pl"] };
        var rules = new Rules.RuleCatalog
        {
            Jurisdictions = new Rules.JurisdictionRegistry(jurisdictions),
            RuleSets = [.. TestTexts.Catalog.RuleSets, new Rules.RuleSet { SourceFile = "legal_pl.yaml", Module = "legal", Jurisdictions = ["pl"] }],
        };

        var poland = Country(PlacesOfSaleClassifier.Reevaluate(result, MarketCatalog.From(rules)), "PL");

        Assert.Equal((true, true, "pl"), (poland.Supported, poland.Preselected, poland.Market));
    }

    [Fact]
    public void Diff_FindsNewCountriesWithStrongEvidence()
    {
        var before = PlacesOfSaleClassifier.Classify(new VerifiedSales { HomeCountry = "SK", HomeEvidence = [new VerifiedQuote("a", "u")] }, Catalog, new Uri("https://shop.sk/"), []);
        var after = PlacesOfSaleClassifier.Classify(new VerifiedSales
        {
            HomeCountry = "SK",
            HomeEvidence = [new VerifiedQuote("a", "u")],
            Countries = [new VerifiedCountry("CZ", "strong", [new VerifiedQuote("Ceny v Kč", "u")], ""), new VerifiedCountry("AT", "delivery", [new VerifiedQuote("AT 9 €", "u")], "")],
        }, Catalog, new Uri("https://shop.sk/"), []);

        Assert.Equal(["CZ"], PlacesOfSaleDiff.NewStrongCountries(before, after).Select(c => c.Country));
    }

    internal static LanguageVersionCandidate Version(string language, string url, string method, bool isMain = false) => new()
    {
        Language = language, BaseUrl = url, SwitchMethod = method, Source = isMain ? VersionSources.Main : VersionSources.Hreflang, IsMain = isMain,
    };

    private static CountryEvidence Country(PlacesOfSaleResult result, string country) => result.Countries.Single(c => c.Country == country);

    private static SalesAnswer Answer(params CountryAnswer[] countries) => new() { Countries = countries };

    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => respond(request);
    }

    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
}

/// <summary>A logger that keeps the formatted messages and exceptions.</summary>
internal sealed class ListLogger<T> : ILogger<T>
{
    public List<string> Messages { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        Messages.Add(formatter(state, exception) + (exception is null ? "" : " " + exception));
}
