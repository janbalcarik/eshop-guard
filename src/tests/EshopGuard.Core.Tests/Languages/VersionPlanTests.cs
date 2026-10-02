using EshopGuard.Core.Extract;
using EshopGuard.Core.Languages;
using EshopGuard.Core.Markets;
using EshopGuard.Core.Options;
using EshopGuard.Core.Pipeline;

namespace EshopGuard.Core.Tests;

/// <summary>Which version is checked for which market, the products for the price, the sample and the language of the descriptions (change 7, groups 5 and 8).</summary>
public sealed class VersionPlanTests
{
    private static readonly MarketCatalog Catalog = MarketCatalog.From(TestTexts.Catalog);

    private static readonly LanguageVersionCandidate Cs = PlacesOfSaleTests.Version("cs", "https://shop.cz/", SwitchMethods.Path, isMain: true);
    private static readonly LanguageVersionCandidate Sk = PlacesOfSaleTests.Version("sk", "https://shop.cz/sk/", SwitchMethods.Path);
    private static readonly LanguageVersionCandidate Pl = PlacesOfSaleTests.Version("pl", "https://shop.cz/pl/", SwitchMethods.Path) with { Status = VersionStatus.Unsupported };

    public static TheoryData<string, string[], string> Cases => new()
    {
        // versions, ticked markets, checked versions with their jurisdictions
        { "cs,sk", ["sk", "cz"], "cs:cz+sk sk:sk+cz" },
        { "cs,sk", ["sk"], "sk:sk" },
        { "cs,sk", ["cz"], "cs:cz" },
        { "cs", ["sk", "cz"], "cs:sk+cz" },
        { "cs,sk,pl", ["sk", "cz"], "cs:cz+sk sk:sk+cz" },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Plan_ChecksTheVersionOfEveryMarketOtherwiseTheMainOne(string versions, string[] markets, string expected)
    {
        var plan = VersionMarketPlanner.Plan(Versions(versions), markets, Catalog);

        Assert.Equal(expected, string.Join(' ', plan.Checked.Select(v => $"{v.Language}:{string.Join('+', v.Jurisdictions)}")));
    }

    [Fact]
    public void Plan_ListsVersionsThatAreNotChecked()
    {
        var plan = VersionMarketPlanner.Plan(Versions("cs,sk,pl"), ["sk", "cz"], Catalog);

        Assert.Equal([("pl", "version_unsupported")], plan.NotChecked.Select(v => (v.Language, v.Code)));
    }

    [Fact]
    public void Recalculate_UntickingCzechiaDropsTheCzechVersionWithoutDownload()
    {
        var counts = new Dictionary<string, int?>
        {
            [VersionMarketPlanner.Key("cs", Cs.BaseUrl)] = 1543,
            [VersionMarketPlanner.Key("sk", Sk.BaseUrl)] = 1537,
        };
        var plan = VersionMarketPlanner.Plan([Cs, Sk], ["sk", "cz"], Catalog, counts);

        var recalculated = plan.Recalculate(["sk"], Catalog);

        Assert.Equal(3080, plan.CountedProducts);
        Assert.Equal(["sk:sk"], recalculated.Checked.Select(v => $"{v.Language}:{string.Join('+', v.Jurisdictions)}"));
        Assert.Equal(1537, recalculated.CountedProducts);
        Assert.Contains(recalculated.NotChecked, v => v is { Language: "cs", Code: VersionCodes.NotNeeded });
    }

    [Fact]
    public void Plan_VersionExcludedByTheClient_FallsBackToTheMainVersion()
    {
        var plan = VersionMarketPlanner.Plan([Cs, Sk with { Status = VersionStatus.Excluded }], ["sk"], Catalog);

        Assert.Equal(["cs:sk"], plan.Checked.Select(v => $"{v.Language}:{string.Join('+', v.Jurisdictions)}"));
        Assert.Contains(plan.NotChecked, v => v is { Language: "sk", Code: "version_excluded" });
    }

    [Fact]
    public void Plan_CountsTheProductsOfEveryCountry_OneVersionForTwoCountriesTwice()
    {
        var counts = new Dictionary<string, int?> { [VersionMarketPlanner.Key("cs", Cs.BaseUrl)] = 1200 };

        var plan = VersionMarketPlanner.Plan([Cs], ["sk", "cz"], Catalog, counts);

        Assert.Equal(2400, plan.CountedProducts);
        Assert.False(plan.ProductCountUnknown);
        Assert.Equal([("sk", "cs", 1200), ("cz", "cs", 1200)], plan.ByMarket.Select(m => (m.Market, m.Language, m.ProductCount ?? 0)));
        Assert.Equal(["sk", "cz"], Assert.Single(plan.Checked).Markets);
    }

    [Fact]
    public void Plan_TwoVersions_GiveTheProductsOfEachCountry()
    {
        var counts = new Dictionary<string, int?>
        {
            [VersionMarketPlanner.Key("cs", Cs.BaseUrl)] = 1543,
            [VersionMarketPlanner.Key("sk", Sk.BaseUrl)] = 1537,
        };

        var plan = VersionMarketPlanner.Plan([Cs, Sk], ["sk", "cz"], Catalog, counts);

        Assert.Equal([("sk", "sk", 1537), ("cz", "cs", 1543)], plan.ByMarket.Select(m => (m.Market, m.Language, m.ProductCount ?? 0)));
        Assert.Equal(3080, plan.CountedProducts);
        Assert.Equal([["cz"], ["sk"]], plan.Checked.Select(v => v.Markets));
    }

    [Fact]
    public void Plan_UnknownProductsOfOneCountry_LeaveTheSumUnknown()
    {
        var counts = new Dictionary<string, int?>
        {
            [VersionMarketPlanner.Key("cs", Cs.BaseUrl)] = 1543,
            [VersionMarketPlanner.Key("sk", Sk.BaseUrl)] = null,
        };

        var plan = VersionMarketPlanner.Plan([Cs, Sk], ["sk", "cz"], Catalog, counts);

        Assert.True(plan.ProductCountUnknown);
        Assert.Null(plan.ByMarket.Single(m => m.Market == "sk").ProductCount);
        Assert.False(plan.Recalculate(["cz"], Catalog).ProductCountUnknown);
    }

    [Fact]
    public void Sample_MandatoryPagesAndRandomProductsOfEachVersion_WithinTheBudget_RepeatedWithTheSeed()
    {
        var cs = Input("cs|https://shop.cz/", "cs", true, "https://shop.cz/p{0}", 80);
        var sk = Input("sk|https://shop.cz/sk/", "sk", false, "https://shop.cz/sk/p{0}", 80);

        var plan = VersionSamplePlanner.Plan([cs, sk], 100, seed: 7);
        var again = VersionSamplePlanner.Plan([cs, sk], 100, seed: 7);
        var other = VersionSamplePlanner.Plan([cs, sk], 100, seed: 8);

        Assert.Equal(100, plan.Urls.Count);
        Assert.Equal([VersionSamplePlanner.Mandatory, VersionSamplePlanner.Product], plan.Urls.Select(u => u.Kind).Distinct().Order());
        Assert.Contains(plan.Urls, u => u is { Url: "https://shop.cz/obchodni-podminky", Kind: VersionSamplePlanner.Mandatory });
        Assert.Contains(plan.Urls, u => u is { Url: "https://shop.cz/sk/obchodne-podmienky", Kind: VersionSamplePlanner.Mandatory });
        Assert.Equal(49, plan.Urls.Count(u => u is { VersionKey: "sk|https://shop.cz/sk/", Kind: VersionSamplePlanner.Product }));
        Assert.Equal(plan.Urls, again.Urls);
        Assert.NotEqual(plan.Urls.Select(u => u.Url), other.Urls.Select(u => u.Url));
        Assert.Equal(plan.Urls.Count, plan.Urls.Select(u => u.Url).Distinct().Count());
    }

    [Fact]
    public void Language_TranslatedVersion_HasAllItsProductsInItsLanguage()
    {
        var sk = Sample("sk", false, 20, i => Slovak(i), _ => ["sk", "sk"]);

        var result = VersionLanguages.Analyze([sk], new MarketsOptions()).Single();

        Assert.Equal((20, 20, 0), (result.LabeledProducts, result.TranslatedProducts, result.ForeignTextProducts));
        Assert.Equal(1.0, result.TranslatedShare);
        Assert.Empty(result.Warnings);
        Assert.Empty(result.Codes);
    }

    [Fact]
    public void Language_CzechDescriptionsOnTheSlovakVersion_AreAWarning()
    {
        var sk = Sample("sk", false, 20, i => i < 3 ? Czech(i) : Slovak(i), i => i < 3 ? ["cs", "cs"] : ["sk", "sk"]);

        var result = VersionLanguages.Analyze([sk], new MarketsOptions()).Single();

        Assert.Equal((20, 17, 3, "cs"), (result.LabeledProducts, result.TranslatedProducts, result.ForeignTextProducts, result.ForeignTextLanguage));
        Assert.Equal(0.85, result.TranslatedShare);
        Assert.Equal([VersionCodes.UntranslatedText], result.Warnings);
        Assert.Equal(["https://shop.cz/sk/p0", "https://shop.cz/sk/p1", "https://shop.cz/sk/p2"], result.UntranslatedExamples);
    }

    [Fact]
    public void Language_SmallSampleOrUnknownProducts_AreSaid()
    {
        var sk = Sample("sk", false, 4, i => Slovak(i), _ => ["sk", "sk"]) with { ProductCount = null };

        var result = VersionLanguages.Analyze([sk], new MarketsOptions()).Single();

        Assert.Equal([VersionCodes.SampleInsufficient, VersionCodes.ProductCountUnknown], result.Codes);
    }

    [Fact]
    public void Language_WithoutLabels_IsUnknown()
    {
        var sk = Sample("sk", false, 20, i => Slovak(i), _ => []);

        var result = VersionLanguages.Analyze([sk], new MarketsOptions()).Single();

        Assert.Contains(VersionCodes.LanguageUnknown, result.Codes);
        Assert.Null(result.TranslatedShare);
    }

    [Fact]
    public void ProductIsForeign_OnlyWithMoreThanHalfOfItsSentencesInAnotherLanguage()
    {
        // goodie.sk, 2. 10. 2026: one Czech sentence of two in a translated description is not a Czech description.
        var pages = Enumerable.Range(0, 4).Select(i => new SamplePage($"https://shop.sk/p{i}", VersionLanguages.Sentences(Slovak(i)))).ToList();
        var labels = new Dictionary<string, IReadOnlyList<string>>
        {
            [pages[0].Url] = ["cs", "sk"],
            [pages[1].Url] = ["cs", "cs"],
            [pages[2].Url] = ["sk", "sk"],
            [pages[3].Url] = ["cs", "cs", "sk"],
        };
        var sk = new VersionSample("sk", "sk", true, pages, 4) { PageLanguages = labels };

        var result = VersionLanguages.Analyze([sk], new MarketsOptions()).Single();

        Assert.Equal((4, 1, 2, "cs"), (result.LabeledProducts, result.TranslatedProducts, result.ForeignTextProducts, result.ForeignTextLanguage));
        Assert.Equal([pages[1].Url, pages[3].Url], result.UntranslatedExamples);
    }

    [Fact]
    public void Fragments_TakeSentencesOfTheRightLength_FromTheFirstProductsInTurn()
    {
        var pages = Enumerable.Range(0, 5).Select(i => new SamplePage($"https://shop.cz/sk/p{i}", ["Krátka.", new string('x', 400), .. VersionLanguages.Sentences(Slovak(i))])).ToList();
        var version = new VersionSample("sk", "sk", false, pages, 5);

        var fragments = VersionLanguages.Fragments(version, products: 3, count: 7, minChars: 30, maxChars: 300);

        Assert.Equal(7, fragments.Count);
        Assert.Equal(["https://shop.cz/sk/p0", "https://shop.cz/sk/p1", "https://shop.cz/sk/p2"], fragments.Select(f => f.PageUrl).Distinct());
        Assert.Equal("https://shop.cz/sk/p1", fragments[1].PageUrl);
        Assert.All(fragments, f => Assert.InRange(f.Text.Length, 30, 300));
        Assert.Equal([1, 2, 3, 4, 5, 6, 7], fragments.Select(f => f.Id));
    }

    private static string Czech(int i) =>
        $"Výrobek číslo {i} vyrábíme ručně z bylin, které pěstujeme na vlastní zahradě. Balení vydrží přibližně {i + 2} měsíce při každodenním používání. Před použitím si přečtěte složení na obalu.";

    private static string Slovak(int i) =>
        $"Výrobok číslo {i} vyrábame ručne z byliniek, ktoré pestujeme na vlastnej záhrade. Balenie vydrží približne {i + 2} mesiace pri každodennom používaní. Pred použitím si prečítajte zloženie na obale.";

    private static VersionSample Sample(string language, bool isMain, int count, Func<int, string> text, Func<int, string[]> labels)
    {
        var pages = Enumerable.Range(0, count).Select(i => new SamplePage($"https://shop.cz/sk/p{i}", VersionLanguages.Sentences(text(i)))).ToList();
        return new VersionSample(language, language, isMain, pages, 120)
        {
            PageLanguages = pages.Select((p, i) => (p.Url, Labels: (IReadOnlyList<string>)labels(i))).Where(x => x.Labels.Count > 0).ToDictionary(x => x.Url, x => x.Labels),
        };
    }

    private static VersionSampleInput Input(string key, string language, bool isMain, string pattern, int count) => new(
        key, language, isMain,
        Enumerable.Range(0, count).Select(i => new SitemapEntry(new Uri(string.Format(System.Globalization.CultureInfo.InvariantCulture, pattern, i)), null, true)).ToList(),
        isMain ? [new Uri("https://shop.cz/obchodni-podminky")] : [new Uri("https://shop.cz/sk/obchodne-podmienky")]);

    private static List<LanguageVersionCandidate> Versions(string languages) =>
        languages.Split(',').Select(l => l switch { "cs" => Cs, "sk" => Sk, _ => Pl }).ToList();
}
