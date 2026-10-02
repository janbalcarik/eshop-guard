using System.Net;
using Microsoft.Extensions.Time.Testing;

namespace EshopGuard.Api.Tests.Auth;

/// <summary>The session (change 9, task 6.7; specification „Relace, cookies a ochrana CSRF“).</summary>
public sealed class SessionTests : ApiTestBase
{
    [Fact]
    public async Task WithoutASession_401Problem_NoRedirect()
    {
        await using var factory = Factory();
        using var browser = factory.CreateApiClient();

        using var response = await browser.GetAsync("/api/me");

        var (status, code, body) = await ApiClient.ProblemAsync(response);
        Assert.Equal(HttpStatusCode.Unauthorized, status);
        Assert.Equal("auth.unauthenticated", code);
        Assert.Null(response.Headers.Location);
        Assert.False(body.TryGetProperty("detail", out _));
    }

    [Fact]
    public async Task CookiesOfTheSessionAndOfCsrf_HaveTheirAttributes()
    {
        await using var factory = Factory();
        using var browser = factory.CreateApiClient();
        using var csrf = await browser.GetAsync("/api/auth/csrf");
        var cookie = csrf.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("__Host-eg_csrf=", StringComparison.Ordinal));
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("domain=", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Logout_EndsThisSession()
    {
        await using var factory = Factory();
        using var browser = factory.CreateApiClient();
        await browser.SignInByLinkAsync(factory, NewEmail());

        using var logout = await browser.PostAsync("/api/auth/logout");

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        using var me = await browser.GetAsync("/api/me");
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Fact]
    public async Task LogoutEverywhere_EndsTheOtherSessionWithinTheStampInterval()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = Factory(time);
        var email = NewEmail();
        using var a = factory.CreateApiClient();
        using var b = factory.CreateApiClient();
        await a.SignInByLinkAsync(factory, email);
        time.Advance(TimeSpan.FromSeconds(61));
        await b.SignInByLinkAsync(factory, email);

        using var everywhere = await a.PostAsync("/api/auth/logout-everywhere");

        Assert.Equal(HttpStatusCode.NoContent, everywhere.StatusCode);
        using (var signedOut = await a.GetAsync("/api/me"))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, signedOut.StatusCode);
        }

        time.Advance(TimeSpan.FromMinutes(6));
        using var ended = await b.GetAsync("/api/me");
        Assert.Equal("auth.unauthenticated", (await ApiClient.ProblemAsync(ended)).Code);
        Assert.Equal(1L, await AdminScalarAsync<long>(
            "SELECT count(*) FROM ops.audit_log WHERE action = 'auth.logout_everywhere' AND actor_user_id = (SELECT id FROM iam.users WHERE email = $1)", email));
    }
}
