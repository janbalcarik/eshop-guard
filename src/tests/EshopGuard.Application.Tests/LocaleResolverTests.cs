using EshopGuard.Application.Localization;

namespace EshopGuard.Application.Tests;

/// <summary>The language of a user (change 9, task 9.3; AD 10).</summary>
public sealed class LocaleResolverTests
{
    private static readonly IReadOnlyList<LocaleInfo> Locales =
    [
        new("sk", "Slovenčina", null, true),
        new("cs", "Čeština", null, true),
        new("de", "Deutsch", null, false),
    ];

    private static readonly IReadOnlyList<MarketInfo> Markets =
    [
        new("sk", "SK", "sk", ["sk"], "EUR", "live", "full"),
        new("cz", "CZ", "cs", ["cs"], "CZK", "live", "limited"),
    ];

    [Fact]
    public void StoredLanguage_Wins()
    {
        var resolved = LocaleResolver.Resolve("cs", "sk-SK", "sk", Locales, Markets, "sk");

        Assert.Equal(new ResolvedLocale("cs", LocaleSource.User), resolved);
        Assert.Equal("user", resolved.SourceCode);
    }

    [Fact]
    public void AcceptLanguage_IsOrderedByQ()
    {
        var resolved = LocaleResolver.Resolve(null, "sk-SK;q=0.8, cs;q=0.9", "sk", Locales, Markets, "sk");

        Assert.Equal(new ResolvedLocale("cs", LocaleSource.AcceptLanguage), resolved);
    }

    [Fact]
    public void UnknownBrowserLanguage_FallsToTheMarket()
    {
        Assert.Equal(new ResolvedLocale("cs", LocaleSource.Market), LocaleResolver.Resolve(null, "en-US", "cz", Locales, Markets, "sk"));
        Assert.Equal(new ResolvedLocale("sk", LocaleSource.Market), LocaleResolver.Resolve(null, null, null, Locales, Markets, "sk"));
        Assert.Equal(new ResolvedLocale("sk", LocaleSource.Market), LocaleResolver.Resolve(null, null, "xx", Locales, Markets, "sk"));
    }

    [Fact]
    public void DisabledLanguage_IsIgnored_Everywhere()
    {
        Assert.Equal(new ResolvedLocale("sk", LocaleSource.Market), LocaleResolver.Resolve("de", "de-DE, de;q=0.9", "sk", Locales, Markets, "sk"));
    }

    [Fact]
    public void MarketLanguage_IsTheLastResort_EvenWhenNotEnabledYet()
    {
        IReadOnlyList<LocaleInfo> none = [new("sk", "Slovenčina", null, false), new("cs", "Čeština", null, false)];

        Assert.Equal(new ResolvedLocale("cs", LocaleSource.Market), LocaleResolver.Resolve("sk", "sk", "cz", none, Markets, "sk"));
    }

    [Theory]
    [InlineData("sk-SK", "sk")]
    [InlineData("cs_CZ", "cs")]
    [InlineData("CS", "cs")]
    public void PrimarySubtag(string tag, string expected) => Assert.Equal(expected, LocaleResolver.Primary(tag));

    [Fact]
    public void AcceptLanguage_SkipsZeroQualityStarsAndGarbage()
    {
        Assert.Equal(["cs", "sk"], LocaleResolver.ParseAcceptLanguage("de;q=0, *, x-klingon, cs, sk;q=0.5, 12"));
        Assert.Empty(LocaleResolver.ParseAcceptLanguage(null));
    }
}
