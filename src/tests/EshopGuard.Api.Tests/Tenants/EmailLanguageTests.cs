using System.Net;
using EshopGuard.Application.Email;
using Microsoft.Extensions.Time.Testing;

namespace EshopGuard.Api.Tests.Tenants;

/// <summary>The language of the recipient (change 9, tasks 9.2 and 9.4; specification „Jazyk uživatele a e-mailů“).</summary>
public sealed class EmailLanguageTests : ApiTestBase
{
    [Fact]
    public async Task LinkForAUserWithCzech_IsCzech_EvenFromTheSlovakPage()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = Factory(time);
        var email = NewEmail();
        using (var czech = factory.CreateApiClient())
        {
            czech.AcceptLanguage = "cs-CZ";
            await czech.SignInByLinkAsync(factory, email, market: "cz");
        }

        time.Advance(TimeSpan.FromSeconds(61));
        using var slovak = factory.CreateApiClient();
        slovak.AcceptLanguage = "sk-SK";
        using var response = await slovak.PostAsync("/api/auth/login-link", new { email, market = "sk" });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var message = factory.Emails.To(email)[^1];
        Assert.Equal("cs", message.Locale);
        Assert.Contains("Přihlásit se", message.Html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvitationForAnEmailWithoutAccount_IsInTheLanguageOfTheRequest_OtherwiseOfTheTenant()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory, market: "sk");
        var czech = NewEmail("peter");
        var plain = NewEmail("eva");

        await (await owner.Browser.PostAsync($"/api/t/{owner.TenantId}/invitations", new { email = czech, role = "viewer", locale = "cs" })).Content.ReadAsStringAsync(Ct);
        await (await owner.Browser.PostAsync($"/api/t/{owner.TenantId}/invitations", new { email = plain, role = "viewer" })).Content.ReadAsStringAsync(Ct);

        Assert.Equal("cs", factory.Emails.To(czech).Single().Locale);
        Assert.Equal("sk", factory.Emails.To(plain).Single().Locale);
        Assert.Contains("čtenář", factory.Emails.To(czech).Single().Text, StringComparison.Ordinal);
        Assert.Contains("čitateľ", factory.Emails.To(plain).Single().Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MeLocale_StoredChoiceWins_DisabledLanguageIsRefused_NullIsAutomatic()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        owner.Browser.AcceptLanguage = "sk-SK";

        using (var set = await owner.Browser.PutAsync("/api/me/locale", new { locale = "cs" }))
        {
            Assert.Equal(HttpStatusCode.NoContent, set.StatusCode);
        }

        var me = await People.MeAsync(owner.Browser);
        Assert.Equal("cs", me.GetProperty("effectiveLocale").GetString());
        Assert.Equal("user", me.GetProperty("localeSource").GetString());

        using (var german = await owner.Browser.PutAsync("/api/me/locale", new { locale = "de" }))
        {
            var (status, code, _) = await ApiClient.ProblemAsync(german);
            Assert.Equal(HttpStatusCode.BadRequest, status);
            Assert.Equal("locale.not_enabled", code);
        }

        Assert.Equal("cs", await AdminScalarAsync<string>("SELECT locale FROM iam.users WHERE id = $1", owner.UserId));
        using (var automatic = await owner.Browser.PutAsync("/api/me/locale", new { locale = (string?)null }))
        {
            Assert.Equal(HttpStatusCode.NoContent, automatic.StatusCode);
        }

        me = await People.MeAsync(owner.Browser);
        Assert.Equal("sk", me.GetProperty("effectiveLocale").GetString());
        Assert.Equal("accept_language", me.GetProperty("localeSource").GetString());
    }

    [Fact]
    public async Task RefLocalesAndMarkets_ListOnlyEnabledAndShown()
    {
        await using var factory = Factory();
        using var browser = factory.CreateApiClient();

        using var locales = await browser.GetAsync("/api/ref/locales");
        using var markets = await browser.GetAsync("/api/ref/markets");

        var codes = (await ApiClient.JsonAsync(locales)).EnumerateArray().Select(l => l.GetProperty("code").GetString()).ToList();
        Assert.Contains("sk", codes);
        Assert.Contains("cs", codes);
        var shown = (await ApiClient.JsonAsync(markets)).EnumerateArray().Select(m => m.GetProperty("webStatus").GetString()).ToList();
        Assert.All(shown, s => Assert.Contains(s, new[] { "preview", "live" }));
        Assert.NotNull(EmailTemplateKind.Find("login_link"));
    }
}
