using EshopGuard.Core.Markets;
using EshopGuard.Core.Rules;

namespace EshopGuard.Core.Tests;

/// <summary>
/// Markets are data in <c>config/jurisdictions.yaml</c> (change 7, task 1.2): supported markets are the markets with an
/// enabled rule set that the host allows; another market is a line of data.
/// </summary>
public sealed class MarketCatalogTests
{
    [Fact]
    public void ShippedMarkets_AreSlovakiaAndCzechia()
    {
        var catalog = MarketCatalog.From(TestTexts.Catalog);

        Assert.Equal(["cz", "sk"], catalog.Supported.Select(m => m.Code));
        var sk = catalog.ByCountry("sk")!;
        Assert.Equal(("SK", "sk"), (sk.Country, sk.Language));
        Assert.True(sk.Reads("cs-CZ"));
        Assert.False(sk.Reads("pl"));
        Assert.Equal("CZ", catalog.ByHost("www.goodie.cz")!.Country);
        Assert.Null(catalog.ByHost("shop.com"));
        Assert.True(catalog.IsSupportedLanguage("sk"));
        Assert.False(catalog.IsSupportedLanguage("pl"));
    }

    [Fact]
    public async Task Supported_IsMarketsWithEnabledRulesAllowedByTheHost()
    {
        using var rules = new TempRules();
        rules.Write("config/jurisdictions.yaml", rules.Text("config/jurisdictions.yaml")
            + "pl:\n  law_language: pl\n  country: PL\n  language: pl\n  readable_languages: [pl]\n  tlds: [pl]\n");
        rules.Replace("rules/texts/cs/_engine.yaml", "  cz: \"Česko\"\n", "  cz: \"Česko\"\n  pl: \"Polsko\"\n");
        var loaded = await rules.LoadAsync();

        var all = MarketCatalog.From(loaded);
        var hostWithoutSlovakia = MarketCatalog.From(loaded, code => code != "sk");

        Assert.Equal(["cz", "pl", "sk"], all.All.Select(m => m.Code));
        Assert.Equal(["cz", "sk"], all.Supported.Select(m => m.Code));
        Assert.False(all.IsSupportedCountry("PL"));
        Assert.NotNull(all.ByCountry("PL"));
        Assert.Equal(["cz"], hostWithoutSlovakia.Supported.Select(m => m.Code));
    }

    [Fact]
    public async Task UnknownKey_StopsTheLoad()
    {
        using var rules = new TempRules();
        rules.Replace("config/jurisdictions.yaml", "  tlds: [sk]\n", "  tlds: [sk]\n  currency: EUR\n");

        var error = await Assert.ThrowsAsync<RuleValidationException>(rules.LoadAsync);

        Assert.Contains(error.Errors, e => e.StartsWith("jurisdictions.yaml", StringComparison.Ordinal));
    }

    [Fact]
    public async Task InvalidMarketFields_StopTheLoad()
    {
        using var rules = new TempRules();
        rules.Replace("config/jurisdictions.yaml", "  country: SK\n", "  country: sk\n");
        rules.Replace("config/jurisdictions.yaml", "  readable_languages: [cs, sk]\n", "  readable_languages: [sk]\n");

        var error = await Assert.ThrowsAsync<RuleValidationException>(rules.LoadAsync);

        Assert.Contains("jurisdictions.yaml: jurisdikce „sk“: country musí být kód ISO 3166-1 alpha-2 velkými písmeny (např. SK).", error.Errors);
        Assert.Contains("jurisdictions.yaml: jurisdikce „cz“: readable_languages musí obsahovat i language (cs).", error.Errors);
    }
}
