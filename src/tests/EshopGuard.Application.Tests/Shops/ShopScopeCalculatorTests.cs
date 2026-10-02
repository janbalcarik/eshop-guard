using EshopGuard.Application.Problems;
using EshopGuard.Application.Shops.Scope;
using EshopGuard.Core.Markets;
using EshopGuard.Core.Rules;
using EshopGuard.Data.Entities.Shops;

namespace EshopGuard.Application.Tests.Shops;

/// <summary>The scope of the check and the products of the price (change 10, task 7.3): the cases A–J of the design, AD 7.</summary>
public sealed class ShopScopeCalculatorTests
{
    private static readonly Guid Sample = Guid.Parse("0199a000-0000-7000-8000-000000000001");

    /// <summary>SK and CZ read Slovak and Czech; PL is a known market without rules (unsupported).</summary>
    internal static MarketCatalog Catalog { get; } = MarketCatalog.From(new RuleCatalog
    {
        Jurisdictions = new JurisdictionRegistry(new Dictionary<string, JurisdictionInfo>
        {
            ["sk"] = new() { LawLanguage = "sk", Country = "SK", Language = "sk", ReadableLanguages = ["sk", "cs"], Tlds = ["sk"] },
            ["cz"] = new() { LawLanguage = "cs", Country = "CZ", Language = "cs", ReadableLanguages = ["cs", "sk"], Tlds = ["cz"] },
            ["pl"] = new() { LawLanguage = "pl", Country = "PL", Language = "pl", ReadableLanguages = ["pl"], Tlds = ["pl"] },
        }),
        RuleSets =
        [
            new RuleSet { Module = "legal", Jurisdictions = ["sk"], SourceFile = "legal_sk.yaml" },
            new RuleSet { Module = "legal", Jurisdictions = ["cz"], SourceFile = "legal_cz.yaml" },
        ],
    });

    private static ScopeVersionInput Sk(int? products = 5834, ShopLanguageStatus status = ShopLanguageStatus.Active) =>
        new("sk", "https://bylinkovo.sk/", status, false, true, products, 38);

    private static ScopeVersionInput Cs(int? products = 5834, ShopLanguageStatus status = ShopLanguageStatus.Active, string url = "https://bylinkovo.sk/cz/") =>
        new("cs", url, status, false, false, products, 38);

    private static ShopScope Scope(string[] markets, ScopeVersionInput[] versions, string[]? excluded = null) =>
        ShopScopeCalculator.Calculate(new ShopScopeInput(markets, versions, excluded ?? [], Sample), Catalog);

    private static string[] Checked(ShopScope scope) =>
        scope.CheckedVersions.Select(v => $"{v.Language} ({string.Join(", ", v.Jurisdictions)})").ToArray();

    [Fact]
    public void A_SkAndCz_TwoVersions_EachForItsMarket()
    {
        var scope = Scope(["sk", "cz"], [Sk(), Cs()]);

        Assert.Equal(["sk (cz, sk)", "cs (cz, sk)"], Checked(scope));
        Assert.Equal(11668, scope.ProductTotal);
        Assert.Equal([("cz", "cs", 5834), ("sk", "sk", 5834)], scope.ByMarket.Select(m => (m.MarketCode, m.Language, m.ProductCount ?? 0)).OrderBy(m => m.MarketCode));
        Assert.Equal(76, scope.OtherPagesTotal);
        Assert.Empty(scope.Issues);
        Assert.All(scope.CheckedVersions, v => Assert.Equal(ShopScopeCalculator.MarketLanguage, v.Reason));
    }

    [Fact]
    public void B_CzUnticked_OnlySk()
    {
        var a = Scope(["sk", "cz"], [Sk(), Cs()]);
        var b = Scope(["sk"], [Sk(), Cs()]);

        Assert.Equal(["sk (sk)"], Checked(b));
        Assert.Equal(5834, b.ProductTotal);
        Assert.Equal([("cs", ShopScopeCalculator.NotNeeded)], b.NotCheckedVersions.Select(v => (v.Language, v.Reason)));
        Assert.NotEqual(a.ScopeHash, b.ScopeHash);
    }

    [Fact]
    public void C_OneVersionForTwoMarkets_CountsTwice()
    {
        var scope = Scope(["sk", "cz"], [Sk()]);

        Assert.Equal(["sk (cz, sk)"], Checked(scope));
        Assert.Equal(11668, scope.ProductTotal);
        Assert.Equal(["cz", "sk"], scope.CheckedVersions[0].Markets.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void D_UnknownProducts_ArePricedByThePagesToCheck()
    {
        // cs: products not known, its sitemap 38 pages; sk: 5 834 products and 38 other pages (decision of 2. 10. 2026).
        var scope = Scope(["sk", "cz"], [Sk(), Cs(products: null)]);

        Assert.Equal(["sk (cz, sk)", "cs (cz, sk)"], Checked(scope));
        Assert.Null(scope.ProductTotal);
        Assert.Equal((PriceUnits.Pages, 5872 + 38), (scope.PriceUnit, scope.PriceCount));
        Assert.Equal([("cz", 38), ("sk", 5872)], scope.ByMarket.Select(m => (m.MarketCode, m.PageCount ?? 0)).OrderBy(m => m.MarketCode));
        Assert.DoesNotContain(ProblemCodes.ScopeProductCountUnknown, scope.Issues);
    }

    [Fact]
    public void D2_OneSitemapWithoutProducts_LikeNaturfyt_CountsItsPages()
    {
        var scope = Scope(["sk"], [new("sk", "https://www.naturfyt.sk/", ShopLanguageStatus.Active, false, true, null, 4036, 4036)]);

        Assert.Equal((PriceUnits.Pages, 4036), (scope.PriceUnit, scope.PriceCount));
        Assert.Empty(scope.Issues);
    }

    [Fact]
    public void D3_NeitherProductsNorPages_GiveNoPrice()
    {
        var scope = Scope(["sk", "cz"], [Sk(), new("cs", "https://bylinkovo.sk/cz/", ShopLanguageStatus.Active, false, false, null, null)]);

        Assert.Null(scope.PriceUnit);
        Assert.Null(scope.PriceCount);
        Assert.Contains(ProblemCodes.ScopeProductCountUnknown, scope.Issues);
    }

    [Fact]
    public void A2_KnownProducts_ArePricedByProducts()
    {
        var scope = Scope(["sk", "cz"], [Sk(), Cs()]);

        Assert.Equal((PriceUnits.Products, 11668), (scope.PriceUnit, scope.PriceCount));
    }

    [Fact]
    public void E_VersionOnAnotherDomainAwaitingConfirmation_MainVersionServesCz()
    {
        var scope = Scope(["sk", "cz"], [Sk(), Cs(status: ShopLanguageStatus.NeedsConfirmation, url: "https://goodie.cz/")]);

        Assert.Equal(["sk (cz, sk)"], Checked(scope));
        Assert.Equal(11668, scope.ProductTotal);
        Assert.Equal([("cs", ShopScopeCalculator.AwaitingConfirmation)], scope.NotCheckedVersions.Select(v => (v.Language, v.Reason)));
        Assert.Equal(ShopScopeCalculator.MarketLanguage, scope.CheckedVersions[0].Reason);
    }

    [Fact]
    public void F_ExcludedCs_SkServesBoth_AndTheHashChanges()
    {
        var a = Scope(["sk", "cz"], [Sk(), Cs()]);
        var f = Scope(["sk", "cz"], [Sk(), Cs()], excluded: ["cs"]);

        Assert.Equal(["sk (cz, sk)"], Checked(f));
        Assert.Equal(11668, f.ProductTotal);
        Assert.Equal([("cs", ShopScopeCalculator.Excluded)], f.NotCheckedVersions.Select(v => (v.Language, v.Reason)));
        Assert.NotEqual(a.ScopeHash, f.ScopeHash);
    }

    [Fact]
    public void G_UnsupportedVersion_IsNotListed()
    {
        var scope = Scope(["sk"], [Sk(), new ScopeVersionInput("pl", "https://bylinkovo.sk/pl/", ShopLanguageStatus.Unsupported, false, false, 5000, 10)]);

        Assert.Equal(["sk (sk)"], Checked(scope));
        Assert.Equal(5834, scope.ProductTotal);
        Assert.Empty(scope.NotCheckedVersions);
    }

    [Fact]
    public void H_CzOnly_CzechVersion()
    {
        var scope = Scope(["cz"], [Sk(), Cs(products: 5790)]);

        Assert.Equal(["cs (cz)"], Checked(scope));
        Assert.Equal(5790, scope.ProductTotal);
        Assert.Equal([("sk", ShopScopeCalculator.NotNeeded)], scope.NotCheckedVersions.Select(v => (v.Language, v.Reason)));
    }

    [Fact]
    public void I_BigShop_SumsAboveTheLastBand()
    {
        var scope = Scope(["sk", "cz"], [Sk(products: 12000), Cs(products: 12000)]);

        Assert.Equal(24000, scope.ProductTotal);
    }

    [Fact]
    public void J_CzWithOnlyASlovakVersion_ChecksItByCzechLaw()
    {
        var scope = Scope(["cz"], [Sk()]);

        Assert.Equal(["sk (cz)"], Checked(scope));
        Assert.Equal(5834, scope.ProductTotal);
        Assert.Equal(ShopScopeCalculator.MainFallback, scope.CheckedVersions[0].Reason);
    }

    [Fact]
    public void ExclusionOfTheAnalysis_IsAvailable_ExclusionOfTheClientIsNot()
    {
        var byAnalysis = Scope(["cz"], [Sk(), Cs(status: ShopLanguageStatus.Excluded)]);
        var byClient = Scope(["cz"], [Sk(), Cs(status: ShopLanguageStatus.Excluded) with { DecidedByUser = true }]);

        Assert.Equal(["cs (cz)"], Checked(byAnalysis));
        Assert.Equal(["sk (cz)"], Checked(byClient));
    }

    [Fact]
    public void NoMarket_AndNoCheckableVersion_AreIssues()
    {
        Assert.Contains(ProblemCodes.MarketsNoneSelected, Scope(["pl"], [Sk()]).Issues);
        var none = Scope(["cz"], [Sk(status: ShopLanguageStatus.Excluded) with { DecidedByUser = true }]);
        Assert.Contains(ProblemCodes.ScopeNoCheckableVersion, none.Issues);
        Assert.Empty(none.CheckedVersions);
        Assert.Null(none.ProductTotal);
    }

    [Fact]
    public void Hash_IsStable_AndDependsOnTheBasis()
    {
        var first = Scope(["cz", "sk"], [Sk(), Cs()]);
        var again = Scope(["sk", "cz"], [Cs(), Sk()]);
        var otherBasis = ShopScopeCalculator.Calculate(new ShopScopeInput(["sk", "cz"], [Sk(), Cs()], [], Guid.NewGuid()), Catalog);

        Assert.Equal(first.ScopeHash, again.ScopeHash);
        Assert.Equal(64, first.ScopeHash.Length);
        Assert.NotEqual(first.ScopeHash, otherBasis.ScopeHash);
    }
}
