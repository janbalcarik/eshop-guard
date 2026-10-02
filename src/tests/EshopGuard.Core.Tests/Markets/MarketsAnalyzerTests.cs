using System.Text.Json;
using EshopGuard.Core.Fix;
using EshopGuard.Core.Languages;
using EshopGuard.Core.Markets;
using Microsoft.Extensions.DependencyInjection;

namespace EshopGuard.Core.Tests;

/// <summary>The whole analysis over the fixture shops with fake models (change 7, tasks 6.1 and 6.2).</summary>
public sealed class MarketsAnalyzerTests
{
    private const string PathShopPick = """{"urls":["https://path-shop.cz/doprava.html","https://path-shop.cz/obchodni-podminky.html","https://path-shop.cz/neexistuje.html"],"reason":"doprava a podmínky"}""";

    private const string PathShopSales = """
        {"home_country":"CZ",
         "home_evidence":[{"quote":"se sídlem Bylinková 12, 586 01 Jihlava, Česká republika","source":"https://path-shop.cz/obchodni-podminky.html"}],
         "countries":[
           {"country":"CZ","evidence_level":"strong","evidence":[{"quote":"Doprava na Slovensko přes Zásilkovnu stojí 4,90 EUR, do České republiky 89 Kč.","source":"https://path-shop.cz/doprava.html"}],"reason":"ceny v Kč"},
           {"country":"SK","evidence_level":"delivery","evidence":[{"quote":"Zboží doručujeme po celé České republice a na Slovensko.","source":"https://path-shop.cz/doprava.html"}],"reason":"doručení na Slovensko"},
           {"country":"HU","evidence_level":"delivery","evidence":[{"quote":"Szállítás Magyarországra","source":"https://path-shop.cz/doprava.html"}],"reason":"vymyšleno"}],
         "eu_wide_delivery":{"stated":true,"quote":"Na vyžádání zboží zašleme i do dalších zemí Evropské unie.","source":"https://path-shop.cz/doprava.html"},
         "delivery_terms":{"found":true,"quote":"Zboží doručujeme po celé České republice a na Slovensko.","source":"https://path-shop.cz/doprava.html"},
         "language_versions":[],
         "uncertain":"INTERNÍ POZNÁMKA MODELU"}
        """;

    [Fact]
    public async Task PathShop_PlacesOfSaleVersionsPlanAndSummary()
    {
        var result = await AnalyzeAsync("path-shop.cz", new MockMarketModel(new Dictionary<MarketCall, string> { [MarketCall.Pick] = PathShopPick, [MarketCall.Sales] = PathShopSales }));

        Assert.Equal(("CZ", HomeBases.Quote), (result.HomeCountry, result.HomeBasis));
        Assert.Equal(["CZ", "SK"], result.Markets.Select(m => m.CountryCode));
        Assert.All(result.Markets, m => Assert.True(m.Preselected));
        Assert.Equal(("strong", "version=sk"), (result.Markets[1].EvidenceLevel, result.Markets[1].Evidence.RaisedBy));
        Assert.Contains(result.RejectedCountries, r => r.Country == "HU");
        Assert.Equal(
            [("https://path-shop.cz/doprava.html", "model"), ("https://path-shop.cz/obchodni-podminky.html", "model"), ("https://path-shop.cz/kontakt.html", "legal")],
            result.SalesPages.Select(p => (p.Url, p.Source)));
        Assert.Contains(EshopGuard.Core.Rules.EngineCodes.ModelMock, result.Codes);
        Assert.Contains(MarketCodes.QuotesDropped, result.Codes);

        Assert.Equal([("cs", true), ("sk", true)], result.Versions.Select(v => (v.Language, v.Counted)));
        var sk = result.Versions.Single(v => v.Language == "sk");
        Assert.Equal(12, sk.Comparison!.PairKinds[PairKinds.Translation]);
        Assert.True(sk.OwnTextShare > 0.9);
        Assert.Equal(12, sk.ProductCount);
        Assert.Equal("cs:cz+sk sk:sk+cz", string.Join(' ', result.Plan!.Checked.Select(v => $"{v.Language}:{string.Join('+', v.Jurisdictions)}")));
        Assert.Equal(24, result.Plan.CountedProducts);
        Assert.Equal(SummaryCodes.VersionsBothOwnTexts, result.Summary!.Code);
        Assert.Equal(["cs", "sk"], (string[])result.Summary.Params["languages"]);
        Assert.Equal("hreflang", result.PairingMode);
        Assert.Contains(result.Pages, p => p.Url == "https://path-shop.cz/sk/produkt-3.html" && p.Language == "sk" && p.HreflangGroup is not null);
        Assert.True(result.Usage.Calls >= 4);
    }

    [Fact]
    public async Task DomainShop_VersionOnAnotherDomainWaitsForConfirmation()
    {
        var result = await AnalyzeAsync("domain-shop.cz", new MockMarketModel());

        Assert.Equal(VersionCodes.OtherDomainNeedsConfirmation, result.Summary!.Code);
        Assert.Equal("domain-shop.sk", result.Summary.Params["domain"]);
        var sk = result.Versions.Single(v => v.Language == "sk");
        Assert.Equal((VersionStatus.NeedsConfirmation, false), (sk.Status, sk.Counted));
        Assert.Contains(result.Notices, n => n.Code == VersionCodes.SampleInsufficient);
        Assert.Equal(("CZ", HomeBases.DomainTld, true), (result.HomeCountry, result.HomeBasis, result.HomeNeedsConfirmation));
        Assert.DoesNotContain(result.Plan!.Checked, v => v.Language == "sk");
    }

    [Fact]
    public async Task FailureOfTheModel_LeavesOnlyTheStructure()
    {
        var model = new ScriptedModel(null) { Throw = new RewriteApiException("OpenAI vrátil HTTP 500.", 500, isFatal: false) };

        var result = await AnalyzeAsync("path-shop.cz", model);

        Assert.Contains(MarketCodes.AnalysisFailed, result.Codes);
        Assert.Equal(("CZ", HomeBases.DomainTld), (result.HomeCountry, result.HomeBasis));
        Assert.Equal(["CZ"], result.Markets.Select(m => m.CountryCode));
        Assert.Equal(2, result.Versions.Count);
    }

    [Fact]
    public async Task MissingKey_NoCallAndTheVersionsOfTheStructure()
    {
        var model = new ScriptedModel(EshopGuard.Core.Rules.EngineCodes.ModelMissingKey);

        var result = await AnalyzeAsync("path-shop.cz", model);

        Assert.Equal(0, model.Calls);
        Assert.Contains(EshopGuard.Core.Rules.EngineCodes.ModelMissingKey, result.Codes);
        Assert.Equal(["cs", "sk"], result.Versions.Select(v => v.Language));
        Assert.All(result.Versions.Where(v => !v.IsMain), v => Assert.False(v.Counted));
        Assert.Equal(0m, result.Usage.CostUsd);
    }

    [Fact]
    public async Task EstimateNotConfirmed_NoCall()
    {
        var model = new ScriptedModel(null);
        MarketAnalysisEstimate? asked = null;

        var result = await AnalyzeAsync("path-shop.cz", model, (estimate, _) =>
        {
            asked = estimate;
            return Task.FromResult(false);
        });

        Assert.Equal(0, model.Calls);
        Assert.NotNull(asked);
        Assert.True(asked!.CostUsd > 0);
        Assert.Contains(MarketCodes.AnalysisNotConfirmed, result.Codes);
        Assert.NotNull(result.Signals);
    }

    [Fact]
    public async Task Result_HoldsNoSentenceForTheClient_AndTheModelTextOnlyInInternal()
    {
        var result = await AnalyzeAsync("path-shop.cz", new MockMarketModel(new Dictionary<MarketCall, string> { [MarketCall.Pick] = PathShopPick, [MarketCall.Sales] = PathShopSales }));

        var json = JsonSerializer.SerializeToNode(result, Pipeline.PipelineJson.Options)!;
        var paths = new List<string>();
        Find(json, "", "INTERNÍ POZNÁMKA MODELU", paths);
        Find(json, "", "ceny v Kč", paths);

        Assert.NotEmpty(paths);
        Assert.All(paths, p => Assert.Contains(".internal.", p, StringComparison.Ordinal));
        Assert.Equal(1, json["schema_version"]!.GetValue<int>());
    }

    private static void Find(System.Text.Json.Nodes.JsonNode? node, string path, string text, List<string> found)
    {
        switch (node)
        {
            case System.Text.Json.Nodes.JsonObject obj:
                foreach (var (key, value) in obj)
                {
                    Find(value, path + "." + key, text, found);
                }

                break;
            case System.Text.Json.Nodes.JsonArray array:
                for (var i = 0; i < array.Count; i++)
                {
                    Find(array[i], $"{path}[{i}]", text, found);
                }

                break;
            case System.Text.Json.Nodes.JsonValue value when value.TryGetValue<string>(out var s) && s.Contains(text, StringComparison.Ordinal):
                found.Add(path + ".");
                break;
        }
    }

    internal static async Task<MarketsAnalysisResult> AnalyzeAsync(string host, IMarketModel model, MarketEstimateConfirmation? confirm = null)
    {
        await using var provider = TestServices.Create(MultiHostFileSystemPageFetcher.ForVersions(), register: s => s.AddSingleton(model));
        return await provider.GetRequiredService<IMarketsAnalyzer>().AnalyzeAsync(
            new MarketsAnalysisRequest(new Uri($"https://{host}/")), confirm, null, TestContext.Current.CancellationToken);
    }

    /// <summary>A model that is "real" (no mock code) or has no key, counts calls and may fail.</summary>
    private sealed class ScriptedModel(string? unavailable) : IMarketModel
    {
        public int Calls { get; private set; }

        public RewriteApiException? Throw { get; init; }

        public string? UnavailableReason => unavailable;

        public Task<MarketModelResponse> AskAsync(MarketModelRequest request, CancellationToken ct)
        {
            Calls++;
            if (Throw is not null)
            {
                throw Throw;
            }

            return new MockMarketModel().AskAsync(request, ct);
        }
    }
}
