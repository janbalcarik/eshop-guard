using System.Net;
using EshopGuard.Application.Email;
using Microsoft.Extensions.Time.Testing;

namespace EshopGuard.Api.Tests.Auth;

/// <summary>The optional password (change 9, task 5.6; specification „Přihlášení heslem“ and „Volitelné heslo a jeho obnova“).</summary>
public sealed class PasswordTests : ApiTestBase
{
    private const string Password = "zeleny-caj-2026";

    [Fact]
    public async Task WrongPassword_UnknownAccount_AndAccountWithoutPassword_AreIndistinguishable()
    {
        await using var factory = Factory();
        var withPassword = await AccountWithPasswordAsync(factory);
        var withoutPassword = NewEmail("bez");
        using (var browser = factory.CreateApiClient())
        {
            await browser.SignInByLinkAsync(factory, withoutPassword);
        }

        var bodies = new List<string>();
        foreach (var (email, password) in new[] { (withPassword, "nespravne-heslo"), ("nikto@nikde-test.sk", "nespravne-heslo"), (withoutPassword, Password) })
        {
            using var browser = factory.CreateApiClient();
            using var response = await browser.PostAsync("/api/auth/password/login", new { email, password });
            var (status, code, body) = await ApiClient.ProblemAsync(response);
            Assert.Equal(HttpStatusCode.Unauthorized, status);
            Assert.Equal("auth.invalid_credentials", code);
            bodies.Add(body.GetProperty("params").GetRawText() + body.GetProperty("type").GetString());
        }

        Assert.Single(bodies.Distinct());
        var failed = (await AdminRowsAsync(
            "SELECT tenant_id, data FROM ops.audit_log WHERE action = 'auth.login_failed' AND actor_user_id = (SELECT id FROM iam.users WHERE email = $1)", withPassword)).Single();
        Assert.Null(failed[0]);
        var data = (string)failed[1]!;
        Assert.Contains("\"method\": \"password\"", data, StringComparison.Ordinal);
        Assert.Contains("emailHash", data, StringComparison.Ordinal);
        Assert.DoesNotContain(withPassword, data, StringComparison.Ordinal);
        Assert.DoesNotContain("nespravne-heslo", data, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FiveWrongPasswords_LockThePasswordOut_ALinkEndsTheLockout()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = Factory(time);
        var email = await AccountWithPasswordAsync(factory);
        using var browser = factory.CreateApiClient();
        for (var i = 0; i < 5; i++)
        {
            using var wrong = await browser.PostAsync("/api/auth/password/login", new { email, password = "nespravne-heslo" });
            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        }

        time.Advance(TimeSpan.FromMinutes(1));
        using (var correct = await browser.PostAsync("/api/auth/password/login", new { email, password = Password }))
        {
            Assert.Equal("auth.invalid_credentials", (await ApiClient.ProblemAsync(correct)).Code);
        }

        Assert.Equal(1L, await AdminScalarAsync<long>(
            "SELECT count(*) FROM ops.audit_log WHERE action = 'auth.locked_out' AND actor_user_id = (SELECT id FROM iam.users WHERE email = $1)", email));

        time.Advance(TimeSpan.FromSeconds(61));
        await browser.SignInByLinkAsync(factory, email);
        var user = (await AdminRowsAsync("SELECT access_failed_count, lockout_end FROM iam.users WHERE email = $1", email)).Single();
        Assert.Equal(0, user[0]);
        Assert.Null(user[1]);
        using var again = await factory.CreateApiClient().PostAsync("/api/auth/password/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
    }

    [Fact]
    public async Task FirstPassword_NeedsARecentSignIn_TwoDaysLaterItIsRefused()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = Factory(time);
        using var browser = factory.CreateApiClient();
        await browser.SignInByLinkAsync(factory, NewEmail());

        time.Advance(TimeSpan.FromDays(2));
        using var late = await browser.PutAsync("/api/me/password", new { newPassword = Password });
        var (status, code, _) = await ApiClient.ProblemAsync(late);
        Assert.Equal(HttpStatusCode.Forbidden, status);
        Assert.Equal("auth.reauthentication_required", code);
    }

    [Fact]
    public async Task FirstPassword_ThreeMinutesAfterTheLink_IsSet_AndAudited()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = Factory(time);
        using var browser = factory.CreateApiClient();
        var email = NewEmail();
        await browser.SignInByLinkAsync(factory, email);
        time.Advance(TimeSpan.FromMinutes(3));

        using var set = await browser.PutAsync("/api/me/password", new { newPassword = "dvanast-znak" });

        Assert.Equal(HttpStatusCode.NoContent, set.StatusCode);
        using var me = await browser.GetAsync("/api/me");
        Assert.True((await ApiClient.JsonAsync(me)).GetProperty("hasPassword").GetBoolean());
        Assert.Equal(1L, await AdminScalarAsync<long>(
            "SELECT count(*) FROM ops.audit_log WHERE action = 'auth.password_set' AND actor_user_id = (SELECT id FROM iam.users WHERE email = $1)", email));
        Assert.StartsWith("AQAAAA", await AdminScalarAsync<string>("SELECT password_hash FROM iam.users WHERE email = $1", email), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShortPassword_IsValidationFailed_OnTheField()
    {
        await using var factory = Factory();
        using var browser = factory.CreateApiClient();
        await browser.SignInByLinkAsync(factory, NewEmail());

        using var response = await browser.PutAsync("/api/me/password", new { newPassword = "kratke" });

        var (status, code, body) = await ApiClient.ProblemAsync(response);
        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("validation.failed", code);
        Assert.Equal(["password.too_short"], body.GetProperty("errors").GetProperty("newPassword").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public async Task ChangingAPassword_NeedsTheCurrentOne_RemovingNeedsARecentSignIn()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = Factory(time);
        using var browser = factory.CreateApiClient();
        var email = NewEmail();
        await browser.SignInByLinkAsync(factory, email);
        using (var set = await browser.PutAsync("/api/me/password", new { newPassword = Password }))
        {
            Assert.Equal(HttpStatusCode.NoContent, set.StatusCode);
        }

        using (var wrong = await browser.PutAsync("/api/me/password", new { currentPassword = "nespravne-heslo", newPassword = "ine-dlhe-heslo" }))
        {
            Assert.Equal("password.current_invalid", (await ApiClient.ProblemAsync(wrong)).Code);
        }

        using (var changed = await browser.PutAsync("/api/me/password", new { currentPassword = Password, newPassword = "ine-dlhe-heslo" }))
        {
            Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
        }

        time.Advance(TimeSpan.FromMinutes(16));
        using (var late = await browser.DeleteAsync("/api/me/password"))
        {
            Assert.Equal("auth.reauthentication_required", (await ApiClient.ProblemAsync(late)).Code);
        }

        using var fresh = factory.CreateApiClient();
        using (var login = await fresh.PostAsync("/api/auth/password/login", new { email, password = "ine-dlhe-heslo" }))
        {
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        }

        using var removed = await fresh.DeleteAsync("/api/me/password");
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        using var me = await fresh.GetAsync("/api/me");
        Assert.False((await ApiClient.JsonAsync(me)).GetProperty("hasPassword").GetBoolean());
    }

    [Fact]
    public async Task Reset_SignsInWithReset_AndTheOtherSessionsEndAfterTheStampInterval()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = Factory(time);
        var email = await AccountWithPasswordAsync(factory);
        using var first = factory.CreateApiClient();
        using var second = factory.CreateApiClient();
        foreach (var browser in new[] { first, second })
        {
            using var login = await browser.PostAsync("/api/auth/password/login", new { email, password = Password });
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        }

        using var third = factory.CreateApiClient();
        using (var unknown = await third.PostAsync("/api/auth/password/forgot", new { email = NewEmail("nikto") }))
        {
            Assert.Equal(HttpStatusCode.Accepted, unknown.StatusCode);
        }

        using (var forgot = await third.PostAsync("/api/auth/password/forgot", new { email }))
        {
            Assert.Equal(HttpStatusCode.Accepted, forgot.StatusCode);
        }

        var token = factory.Emails.LastToken(email, EmailTemplateKind.PasswordReset.Code);
        using (var inspected = await third.PostAsync("/api/auth/password/reset/inspect", new { token }))
        {
            Assert.Equal(email, (await ApiClient.JsonAsync(inspected)).GetProperty("email").GetString());
        }

        using (var reset = await third.PostAsync("/api/auth/password/reset", new { token, newPassword = "nove-dlhe-heslo" }))
        {
            Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        }

        Assert.Equal(1L, await AdminScalarAsync<long>(
            "SELECT count(*) FROM ops.audit_log WHERE action = 'auth.login_succeeded' AND data ->> 'method' = 'reset' AND actor_user_id = (SELECT id FROM iam.users WHERE email = $1)", email));
        using (var before = await first.GetAsync("/api/me"))
        {
            Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        }

        time.Advance(TimeSpan.FromMinutes(6));
        foreach (var browser in new[] { first, second })
        {
            using var ended = await browser.GetAsync("/api/me");
            Assert.Equal("auth.unauthenticated", (await ApiClient.ProblemAsync(ended)).Code);
        }

        using var still = await third.GetAsync("/api/me");
        Assert.Equal(HttpStatusCode.OK, still.StatusCode);
        using var reused = await third.PostAsync("/api/auth/password/reset", new { token, newPassword = "este-ine-heslo" });
        Assert.Equal("reset_link.used", (await ApiClient.ProblemAsync(reused)).Code);
    }

    /// <summary>An account signed in by a link once, with <see cref="Password"/> set.</summary>
    private static async Task<string> AccountWithPasswordAsync(ApiFactory factory)
    {
        var email = NewEmail();
        using var browser = factory.CreateApiClient();
        await browser.SignInByLinkAsync(factory, email);
        using var set = await browser.PutAsync("/api/me/password", new { newPassword = Password });
        Assert.Equal(HttpStatusCode.NoContent, set.StatusCode);
        return email;
    }
}
