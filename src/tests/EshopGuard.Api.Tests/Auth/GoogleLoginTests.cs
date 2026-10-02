using System.Net;
using System.Web;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.Extensions.DependencyInjection;

namespace EshopGuard.Api.Tests.Auth;

/// <summary>Sign-in through Google with a fake back channel (change 9, task 5.7; specification „Přihlášení přes Google“).</summary>
public sealed class GoogleLoginTests : ApiTestBase
{
    private readonly FakeGoogleHandler _google = new();

    [Fact]
    public async Task VerifiedEmailOfAnExistingAccount_LinksIt_AndSignsIn()
    {
        await using var factory = GoogleFactory();
        var email = NewEmail();
        using (var first = factory.CreateApiClient())
        {
            await first.SignInByLinkAsync(factory, email);
        }

        _google.User = new { sub = "g-" + Guid.NewGuid().ToString("N"), email, email_verified = true, name = "Jana" };
        using var browser = factory.CreateApiClient();
        var location = await SignInThroughGoogleAsync(browser, "/app/nastavenia");

        Assert.Equal(ApiFactory.FrontendBaseUrl + "/app/nastavenia", location);
        using var me = await browser.GetAsync("/api/me");
        var body = await ApiClient.JsonAsync(me);
        Assert.Equal(email, body.GetProperty("email").GetString());
        Assert.True(body.GetProperty("googleLinked").GetBoolean());
        Assert.Equal(1L, await AdminScalarAsync<long>(
            "SELECT count(*) FROM iam.user_logins WHERE provider = 'google' AND user_id = (SELECT id FROM iam.users WHERE email = $1)", email));
        Assert.Equal(1L, await AdminScalarAsync<long>(
            "SELECT count(*) FROM ops.audit_log WHERE action = 'auth.google_linked' AND actor_user_id = (SELECT id FROM iam.users WHERE email = $1)", email));

        // Next time the link of Google finds the account by itself, unlinking removes it.
        using var again = factory.CreateApiClient();
        Assert.Equal(ApiFactory.FrontendBaseUrl + "/", await SignInThroughGoogleAsync(again, null));
        using var unlinked = await again.DeleteAsync("/api/me/logins/google");
        Assert.Equal(HttpStatusCode.NoContent, unlinked.StatusCode);
        using var none = await again.DeleteAsync("/api/me/logins/google");
        Assert.Equal("login.not_linked", (await ApiClient.ProblemAsync(none)).Code);
    }

    [Fact]
    public async Task UnverifiedEmail_CreatesNothing_AndReturnsToTheSignInPageWithTheCode()
    {
        await using var factory = GoogleFactory();
        var email = NewEmail();
        _google.User = new { sub = "g-" + Guid.NewGuid().ToString("N"), email, email_verified = false };
        using var browser = factory.CreateApiClient();

        var location = await SignInThroughGoogleAsync(browser, "/app");

        Assert.Equal(ApiFactory.FrontendBaseUrl + "/prihlasenie?error=google.email_not_verified", location);
        Assert.Equal(0L, await AdminScalarAsync<long>("SELECT count(*) FROM iam.users WHERE email = $1", email));
        using var me = await browser.GetAsync("/api/me");
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Fact]
    public async Task NewAccount_GetsItsTenant()
    {
        await using var factory = GoogleFactory();
        var email = NewEmail("novy");
        _google.User = new { sub = "g-" + Guid.NewGuid().ToString("N"), email, verified_email = true };
        using var browser = factory.CreateApiClient();

        await SignInThroughGoogleAsync(browser, "/app", market: "cz");

        using var me = await browser.GetAsync("/api/me");
        var membership = (await ApiClient.JsonAsync(me)).GetProperty("memberships").EnumerateArray().Single();
        Assert.Equal("owner", membership.GetProperty("role").GetString());
        Assert.Equal("cz", await AdminScalarAsync<string>("SELECT market_code FROM iam.tenants WHERE id = $1", membership.GetProperty("tenantId").GetGuid()));
        Assert.Equal(1L, await AdminScalarAsync<long>(
            "SELECT count(*) FROM ops.audit_log WHERE action = 'auth.login_succeeded' AND data ->> 'method' = 'google' AND actor_user_id = (SELECT id FROM iam.users WHERE email = $1)", email));
    }

    [Theory]
    [InlineData("//zly.example/")]
    [InlineData("/\\zly.example")]
    [InlineData("https://zly.example/")]
    [InlineData("javascript:alert(1)")]
    public async Task ForeignReturnPath_Is400_AndDoesNotGoToGoogle(string returnPath)
    {
        await using var factory = GoogleFactory();
        using var browser = factory.CreateApiClient();

        using var response = await browser.GetAsync("/api/auth/google/start?returnPath=" + Uri.EscapeDataString(returnPath));

        var (status, code, _) = await ApiClient.ProblemAsync(response);
        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("return_path.invalid", code);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task FailureAtGoogle_ReturnsToTheSignInPage_NotA500()
    {
        await using var factory = GoogleFactory();
        using var browser = factory.CreateApiClient();
        using var start = await browser.GetAsync("/api/auth/google/start?returnPath=/app");
        Assert.Equal(HttpStatusCode.Redirect, start.StatusCode);

        // A callback with a state that does not match the correlation cookie (forged or replayed).
        using var callback = await browser.GetAsync("/api/auth/google/signin?code=fake-code&state=forged");

        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Equal(ApiFactory.FrontendBaseUrl + "/prihlasenie?error=google.failed", callback.Headers.Location!.OriginalString);
        Assert.Empty(_google.Requests);
    }

    [Fact]
    public async Task WithoutClientId_GoogleIsOff_TheRestRuns()
    {
        await using var factory = Factory();
        using var browser = factory.CreateApiClient();

        using var response = await browser.GetAsync("/api/auth/google/start?returnPath=/app");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var csrf = await browser.GetAsync("/api/auth/csrf");
        Assert.Equal(HttpStatusCode.OK, csrf.StatusCode);
    }

    private ApiFactory GoogleFactory() => Factory(
        settings: new Dictionary<string, string?>
        {
            ["Authentication:Google:ClientId"] = "test-client-id",
            ["Authentication:Google:ClientSecret"] = "test-client-value",
        },
        services: s => s.Configure<GoogleOptions>(GoogleDefaults.AuthenticationScheme, o =>
        {
            // Configure (not PostConfigure): the back channel is built from the handler in the handler's own PostConfigure.
            o.BackchannelHttpHandler = _google;
            o.TokenEndpoint = "https://google.invalid/token";
            o.UserInformationEndpoint = "https://google.invalid/userinfo";
        }));

    /// <summary>start → (Google) → signin callback → complete; returns where the browser is sent at the end.</summary>
    private async Task<string> SignInThroughGoogleAsync(ApiClient browser, string? returnPath, string? market = null)
    {
        var query = (returnPath is null ? string.Empty : "returnPath=" + Uri.EscapeDataString(returnPath)) + (market is null ? string.Empty : "&market=" + market);
        using var start = await browser.GetAsync("/api/auth/google/start?" + query.TrimStart('&'));
        Assert.Equal(HttpStatusCode.Redirect, start.StatusCode);
        var google = start.Headers.Location!;
        Assert.Equal("accounts.google.com", google.Host);
        var state = HttpUtility.ParseQueryString(google.Query)["state"];

        using var callback = await browser.GetAsync("/api/auth/google/signin?code=fake-code&state=" + Uri.EscapeDataString(state!));
        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Equal("/api/auth/google/complete", callback.Headers.Location!.OriginalString);
        Assert.All(_google.Requests, r => Assert.Equal("google.invalid", r.Host));

        using var complete = await browser.GetAsync("/api/auth/google/complete");
        Assert.Equal(HttpStatusCode.Redirect, complete.StatusCode);
        return complete.Headers.Location!.OriginalString;
    }
}
