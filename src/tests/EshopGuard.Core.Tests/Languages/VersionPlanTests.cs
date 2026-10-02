using EshopGuard.Core.Extract;
using EshopGuard.Core.Languages;
using EshopGuard.Core.Markets;
using EshopGuard.Core.Options;
using EshopGuard.Core.Pipeline;

namespace EshopGuard.Core.Tests;

/// <summary>Which version is checked for which market, the sample and the comparison of versions (change 7, group 5).</summary>
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
        var counts = new Dictionary<string, VersionCount>
        {
            [VersionMarketPlanner.Key("cs", Cs.BaseUrl)] = new(120, true),
            [VersionMarketPlanner.Key("sk", Sk.BaseUrl)] = new(118, true),
        };
        var plan = VersionMarketPlanner.Plan([Cs, Sk], ["sk", "cz"], Catalog, counts);

        var recalculated = plan.Recalculate(["sk"], Catalog);

        Assert.Equal(238, plan.CountedProducts);
        Assert.Equal(["sk:sk"], recalculated.Checked.Select(v => $"{v.Language}:{string.Join('+', v.Jurisdictions)}"));
        Assert.Equal(118, recalculated.CountedProducts);
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
    public void Plan_SameTextsCountOnce()
    {
        var counts = new Dictionary<string, VersionCount>
        {
            [VersionMarketPlanner.Key("cs", Cs.BaseUrl)] = new(120, true),
            [VersionMarketPlanner.Key("sk", Sk.BaseUrl)] = new(120, false),
        };

        Assert.Equal(120, VersionMarketPlanner.Plan([Cs, Sk], ["sk", "cz"], Catalog, counts).CountedProducts);
    }

    [Fact]
    public void Sample_StaysWithinTheBudgetAndRepeatsWithTheSeed()
    {
        var cs = Input("cs|https://shop.cz/", "cs", true, "https://shop.cz/p{0}", "sk", "https://shop.cz/sk/p{0}", 80);
        var sk = Input("sk|https://shop.cz/sk/", "sk", false, "https://shop.cz/sk/p{0}", "cs", "https://shop.cz/p{0}", 80);

        var plan = VersionSamplePlanner.Plan([cs, sk], 100, 20, seed: 7);
        var again = VersionSamplePlanner.Plan([cs, sk], 100, 20, seed: 7);
        var other = VersionSamplePlanner.Plan([cs, sk], 100, 20, seed: 8);

        Assert.True(plan.Urls.Count <= 100);
        Assert.Equal(100, plan.Urls.Count);
        Assert.Equal(20, plan.Pairs.Count);
        Assert.Equal(VersionSamplePlanner.ModeHreflang, plan.PairingMode);
        Assert.Contains(plan.Urls, u => u is { Url: "https://shop.cz/obchodni-podminky", Kind: "mandatory" });
        Assert.Equal(plan.Urls, again.Urls);
        Assert.NotEqual(plan.Urls.Select(u => u.Url), other.Urls.Select(u => u.Url));
        Assert.Equal(plan.Urls.Count, plan.Urls.Select(u => u.Url).Distinct().Count());
    }

    [Fact]
    public void Compare_FaithfulTranslation_Counts()
    {
        var (cs, sk, pairs) = Samples(10, i => Czech(i), i => Slovak(i), "sk");

        var sk2 = Comparison(cs, sk, pairs);

        Assert.All(sk2.Pairs, p => Assert.Equal(PairKinds.Translation, p.Kind));
        Assert.True(sk2.OwnTextShare > 0.20);
        Assert.True(sk2.Counted);
        Assert.Empty(sk2.Codes);
    }

    [Fact]
    public void Compare_OnlyTheMenuIsTranslated_DoesNotCount()
    {
        var (cs, sk, pairs) = Samples(10, i => Czech(i), i => Czech(i), "sk");

        var result = Comparison(cs, sk, pairs);

        Assert.All(result.Pairs, p => Assert.Equal(PairKinds.Identical, p.Kind));
        Assert.True(result.OwnTextShare < 0.20);
        Assert.False(result.Counted);
    }

    [Fact]
    public void Compare_CzechTextOnTheSlovakVersion_IsAWarning()
    {
        var (cs, sk, pairs) = Samples(10, i => Czech(i), i => i < 2 ? Czech(i) + " Novinka." : Slovak(i), "sk", untranslated: 2);

        var result = Comparison(cs, sk, pairs);

        Assert.Equal(2, result.Pairs.Count(p => p.Kind == PairKinds.Untranslated));
        Assert.Equal([VersionCodes.UntranslatedText], result.Warnings);
        Assert.Equal(2, result.UntranslatedExamples.Count);
    }

    [Fact]
    public void Compare_SmallSample_DoesNotCount()
    {
        var (cs, sk, pairs) = Samples(4, i => Czech(i), i => Slovak(i), "sk");

        var result = Comparison(cs, sk, pairs);

        Assert.False(result.Counted);
        Assert.Contains(VersionCodes.SampleInsufficient, result.Codes);
    }

    [Fact]
    public void Compare_UnknownProductCount_DoesNotCount()
    {
        var (cs, sk, pairs) = Samples(10, i => Czech(i), i => Slovak(i), "sk");

        var result = VersionComparer.Compare([cs, sk with { ProductCount = null }], pairs, new MarketsOptions()).Versions.Single(v => !v.IsMain);

        Assert.False(result.Counted);
        Assert.Contains(VersionCodes.ProductCountUnknown, result.Codes);
    }

    [Theory]
    [InlineData(0.19, false)]
    [InlineData(0.21, true)]
    public void Compare_ThresholdOfOwnTexts(double share, bool counted)
    {
        // 100 sentences of the Slovak version, the given share of them its own.
        var own = (int)Math.Round(share * 100);
        var common = Enumerable.Range(0, 100 - own).Select(i => $"Společná věta číslo {i} je v obou verzích stejná.").ToList();
        var mainPages = Enumerable.Range(0, 10).Select(p => new SamplePage($"https://shop.cz/p{p}", common.Skip(p * 10).Take(10).ToList(), null, [])).ToList();
        var otherSentences = common.Concat(Enumerable.Range(0, own).Select(i => $"Vlastná veta číslo {i} je len na slovenskej verzii.")).ToList();
        var chunk = (int)Math.Ceiling(otherSentences.Count / 10.0);
        var otherPages = Enumerable.Range(0, 10).Select(p => new SamplePage($"https://shop.cz/sk/p{p}", otherSentences.Skip(p * chunk).Take(chunk).ToList(), null, [])).ToList();
        var cs = new VersionSample("cs", "cs", true, mainPages, [], 100);
        var sk = new VersionSample("sk", "sk", false, otherPages, [], 100) { PageLanguages = otherPages.ToDictionary(p => p.Url, _ => (IReadOnlyList<string>)["sk"]) };

        var result = VersionComparer.Compare([cs, sk], [], new MarketsOptions()).Versions.Single(v => !v.IsMain);

        Assert.Equal(share, result.OwnTextShare, 2);
        Assert.Equal(counted, result.Counted);
        Assert.NotNull(result.SentenceOverlapShare);
    }

    [Fact]
    public void Fragments_TakeSentencesOfTheRightLengthFromPairedPagesFirst()
    {
        var pages = Enumerable.Range(0, 5).Select(i => new SamplePage($"https://shop.cz/sk/p{i}", [Slovak(i), "Krátka.", new string('x', 400)], null, [])).ToList();
        var version = new VersionSample("sk", "sk", false, pages, [], 5);

        var fragments = VersionComparer.Fragments(version, ["https://shop.cz/sk/p3"], 3, 30, 300);

        Assert.Equal(3, fragments.Count);
        Assert.Equal("https://shop.cz/sk/p3", fragments[0].PageUrl);
        Assert.All(fragments, f => Assert.InRange(f.Text.Length, 30, 300));
        Assert.Equal([1, 2, 3], fragments.Select(f => f.Id));
    }

    private static string Czech(int i) =>
        $"Výrobek číslo {i} vyrábíme ručně z bylin, které pěstujeme na vlastní zahradě. Balení vydrží přibližně {i + 2} měsíce při každodenním používání. Před použitím si přečtěte složení na obalu.";

    private static string Slovak(int i) =>
        $"Výrobok číslo {i} vyrábame ručne z byliniek, ktoré pestujeme na vlastnej záhrade. Balenie vydrží približne {i + 2} mesiace pri každodennom používaní. Pred použitím si prečítajte zloženie na obale.";

    private static (VersionSample Cs, VersionSample Sk, List<SamplePair> Pairs) Samples(int count, Func<int, string> czech, Func<int, string> slovak, string skLanguage, int untranslated = 0)
    {
        var mainPages = Enumerable.Range(0, count).Select(i => new SamplePage($"https://shop.cz/p{i}", VersionComparer.Sentences(czech(i)), null, [])).ToList();
        var otherPages = Enumerable.Range(0, count).Select(i => new SamplePage($"https://shop.cz/sk/p{i}", VersionComparer.Sentences(slovak(i)), null, [])).ToList();
        var labels = otherPages.Select((p, i) => (p.Url, Labels: (IReadOnlyList<string>)(i < untranslated ? ["cs", "cs"] : [skLanguage, skLanguage]))).ToDictionary(x => x.Url, x => x.Labels);
        var cs = new VersionSample("cs", "cs", true, mainPages, [], 120);
        var sk = new VersionSample("sk", "sk", false, otherPages, [], 118) { PageLanguages = labels };
        return (cs, sk, mainPages.Select((p, i) => new SamplePair(p.Url, otherPages[i].Url, "sk")).ToList());
    }

    private static VersionComparison Comparison(VersionSample cs, VersionSample sk, List<SamplePair> pairs) =>
        VersionComparer.Compare([cs, sk], pairs, new MarketsOptions()).Versions.Single(v => !v.IsMain);

    private static VersionSampleInput Input(string key, string language, bool isMain, string pattern, string otherLanguage, string otherPattern, int count) => new(
        key, language, isMain,
        Enumerable.Range(0, count).Select(i => new SitemapEntry(new Uri(string.Format(System.Globalization.CultureInfo.InvariantCulture, pattern, i)), null, true)
        {
            Alternates = [new PageAlternate(otherLanguage, new Uri(string.Format(System.Globalization.CultureInfo.InvariantCulture, otherPattern, i)))],
        }).ToList(),
        isMain ? [new Uri("https://shop.cz/obchodni-podminky")] : [new Uri("https://shop.cz/sk/obchodne-podmienky")]);

    private static List<LanguageVersionCandidate> Versions(string languages) =>
        languages.Split(',').Select(l => l switch { "cs" => Cs, "sk" => Sk, _ => Pl }).ToList();
}
