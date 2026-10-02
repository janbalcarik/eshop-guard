using EshopGuard.Application.Security;

namespace EshopGuard.Application.Tests;

/// <summary>E-mail normalization, return paths and tokens (change 9, tasks 3.1, 3.2 and 5.4).</summary>
public sealed class SecurityHelpersTests
{
    [Theory]
    [InlineData(" Jana@Bylinkovo.SK ", "jana@bylinkovo.sk")]
    [InlineData("peter.novak+eshop@agentura.cz", "peter.novak+eshop@agentura.cz")]
    [InlineData("jana@čajovňa.sk", "jana@xn--ajova-gya13b.sk")]
    public void Email_IsNormalized(string input, string expected) => Assert.Equal(expected, EmailNormalizer.Normalize(input));

    [Theory]
    [InlineData("jana@")]
    [InlineData("@bylinkovo.sk")]
    [InlineData("jana")]
    [InlineData("jana@bylinkovo")]
    [InlineData("ja..na@bylinkovo.sk")]
    [InlineData("jana@@bylinkovo.sk")]
    [InlineData("jana nova@bylinkovo.sk")]
    [InlineData("jana@-bylinkovo.sk")]
    [InlineData("")]
    [InlineData(null)]
    public void InvalidEmail_IsNull(string? input) => Assert.Null(EmailNormalizer.Normalize(input));

    [Fact]
    public void TooLongEmail_IsNull() => Assert.Null(EmailNormalizer.Normalize(new string('a', 60) + "@" + string.Join(".", Enumerable.Repeat(new string('b', 60), 4)) + ".sk"));

    [Theory]
    [InlineData("/")]
    [InlineData("/app/nastavenia")]
    [InlineData("/app?dalej=https://x.sk#a:b")]
    public void ReturnPath_OfTheApplication_IsValid(string path) => Assert.True(ReturnPathValidator.IsValid(path));

    [Theory]
    [InlineData("//zly.example/")]
    [InlineData("/\\zly.example")]
    [InlineData("https://zly.example/")]
    [InlineData("javascript:alert(1)")]
    [InlineData("/a:b")]
    [InlineData("app")]
    [InlineData("/a\nb")]
    [InlineData("")]
    [InlineData(null)]
    public void ForeignReturnPath_IsInvalid(string? path) => Assert.False(ReturnPathValidator.IsValid(path));

    [Fact]
    public void LongReturnPath_IsInvalid() => Assert.False(ReturnPathValidator.IsValid("/" + new string('a', 512)));

    [Fact]
    public void Token_Is43CharactersOfBase64Url_AndHashesTheSameWay()
    {
        var (token, hash) = OneTimeTokens.Create();

        Assert.Equal(43, token.Length);
        Assert.All(token, c => Assert.True(char.IsAsciiLetterOrDigit(c) || c is '-' or '_'));
        Assert.Equal(32, hash.Length);
        Assert.Equal(hash, OneTimeTokens.TryHash(token));
        Assert.NotEqual(token, OneTimeTokens.Create().Token);
        Assert.Null(OneTimeTokens.TryHash(token[..42]));
        Assert.Null(OneTimeTokens.TryHash(token[..42] + "*"));
        Assert.Null(OneTimeTokens.TryHash(null));
    }
}
