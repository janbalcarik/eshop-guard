using System.Net;
using System.Text.Json;
using EshopGuard.Application.Email;
using Microsoft.Extensions.Time.Testing;

namespace EshopGuard.Api.Tests.Auth;

/// <summary>Sign-in by a link (change 9, task 4.7; specification „Vyžádání odkazu“ and „Přihlášení potvrzením odkazu“).</summary>
public sealed class LoginLinkTests : ApiTestBase
{
    [Fact]
    public async Task ExistingAndUnknownAccount_GetTheSameAnswer_AndTheSameKindOfEmail()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = Factory(time);
        using var browser = factory.CreateApiClient();
        var existing = NewEmail();
        await browser.SignInByLinkAsync(factory, existing);
        time.Advance(TimeSpan.FromSeconds(61));

        var unknownEmail = NewEmail("novy");
        using var known = await factory.CreateApiClient().PostAsync("/api/auth/login-link", new { email = existing });
        using var unknown = await factory.CreateApiClient().PostAsync("/api/auth/login-link", new { email = unknownEmail });

        Assert.Equal(HttpStatusCode.Accepted, known.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, unknown.StatusCode);
        Assert.Equal(await known.Content.ReadAsStringAsync(Ct), await unknown.Content.ReadAsStringAsync(Ct));
        var body = await ApiClient.JsonAsync(unknown);
        Assert.Equal(900, body.GetProperty("expiresInSeconds").GetInt32());
        Assert.Equal(60, body.GetProperty("resendAfterSeconds").GetInt32());
        Assert.Equal(EmailTemplateKind.LoginLink.Code, factory.Emails.To(unknownEmail).Single().Kind);
        Assert.Equal(0L, await AdminScalarAsync<long>("SELECT count(*) FROM iam.users WHERE email = $1", unknownEmail));
        Assert.Equal(1L, await AdminScalarAsync<long>(
            "SELECT count(*) FROM iam.user_tokens WHERE email = $1 AND user_id IS NULL AND purpose = 'magic_link' AND length(token_hash) = 32", unknownEmail));
        var emails = factory.Emails.To(existing);
        Assert.Equal(2, emails.Count);
        Assert.All(emails, e => Assert.Equal(EmailTemplateKind.LoginLink.Code, e.Kind));
        Assert.Contains("#t=", emails[^1].Text, StringComparison.Ordinal);
        Assert.Equal("sk", emails[^1].Locale);
    }

    [Fact]
    public async Task Inspect_DoesNotUseTheLink_ConsumeCreatesTheAccountAndItsTenant()
    {
        await using var factory = Factory();
        using var browser = factory.CreateApiClient();
        browser.AcceptLanguage = "cs-CZ";
        var email = NewEmail("novy");
        using (var requested = await browser.PostAsync("/api/auth/login-link", new { email, market = "cz" }))
        {
            Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        }

        var token = factory.Emails.LastToken(email, EmailTemplateKind.LoginLink.Code);
        for (var i = 0; i < 2; i++)
        {
            using var inspected = await browser.PostAsync("/api/auth/login-link/inspect", new { token });
            Assert.Equal(HttpStatusCode.OK, inspected.StatusCode);
            var info = await ApiClient.JsonAsync(inspected);
            Assert.Equal(email, info.GetProperty("email").GetString());
            Assert.True(info.GetProperty("isNewAccount").GetBoolean());
        }

        Assert.Null(await AdminScalarAsync<DateTime?>("SELECT used_at FROM iam.user_tokens WHERE email = $1", email));

        using var consumed = await browser.PostAsync("/api/auth/login-link/consume", new { token, market = "cz" });
        Assert.Equal(HttpStatusCode.OK, consumed.StatusCode);
        var session = await ApiClient.JsonAsync(consumed);
        Assert.True(session.GetProperty("isNewAccount").GetBoolean());
        var tenantId = session.GetProperty("createdTenantId").GetGuid();
        var me = session.GetProperty("me");
        Assert.Equal(email, me.GetProperty("email").GetString());
        Assert.Equal("cs", me.GetProperty("locale").GetString());
        var membership = me.GetProperty("memberships").EnumerateArray().Single();
        Assert.Equal(tenantId, membership.GetProperty("tenantId").GetGuid());
        Assert.Equal("owner", membership.GetProperty("role").GetString());

        var user = (await AdminRowsAsync("SELECT email_confirmed, locale FROM iam.users WHERE email = $1", email)).Single();
        Assert.Equal(true, user[0]);
        Assert.Equal("cs", user[1]);
        var tenant = (await AdminRowsAsync("SELECT market_code, locale, currency, name FROM iam.tenants WHERE id = $1", tenantId)).Single();
        Assert.Equal(["cz", "cs", null, email], tenant);

        var cookie = consumed.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("__Host-eg_session=", StringComparison.Ordinal));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", cookie, StringComparison.OrdinalIgnoreCase);
        using var meAgain = await browser.GetAsync("/api/me");
        Assert.Equal(HttpStatusCode.OK, meAgain.StatusCode);

        var actions = (await AdminRowsAsync("SELECT action, data ->> 'method' FROM ops.audit_log WHERE actor_user_id = (SELECT id FROM iam.users WHERE email = $1) ORDER BY id", email))
            .Select(r => ((string)r[0]!, (string?)r[1])).ToList();
        Assert.Contains(("user.created", "magic_link"), actions);
        Assert.Contains(actions, a => a.Item1 == "user.terms_accepted");
        Assert.Contains(("auth.login_succeeded", "magic_link"), actions);
        Assert.Equal("test-terms-1", await AdminScalarAsync<string>(
            "SELECT data ->> 'termsVersion' FROM ops.audit_log WHERE action = 'user.terms_accepted' AND actor_user_id = (SELECT id FROM iam.users WHERE email = $1)", email));
    }

    [Fact]
    public async Task NewLink_ExpiresThePreviousOne()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = Factory(time);
        using var browser = factory.CreateApiClient();
        var email = NewEmail();
        await (await browser.PostAsync("/api/auth/login-link", new { email })).Content.ReadAsStringAsync(Ct);
        var first = factory.Emails.LastToken(email, EmailTemplateKind.LoginLink.Code);
        time.Advance(TimeSpan.FromMinutes(2));
        await (await browser.PostAsync("/api/auth/login-link", new { email })).Content.ReadAsStringAsync(Ct);
        var second = factory.Emails.LastToken(email, EmailTemplateKind.LoginLink.Code);

        using var old = await browser.PostAsync("/api/auth/login-link/consume", new { token = first });
        var (status, code, _) = await ApiClient.ProblemAsync(old);
        Assert.Equal(HttpStatusCode.Gone, status);
        Assert.Equal("login_link.expired", code);
        using var valid = await browser.PostAsync("/api/auth/login-link/inspect", new { token = second });
        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);
    }

    [Fact]
    public async Task LinkAfter15Minutes_IsExpired()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = Factory(time);
        using var browser = factory.CreateApiClient();
        var email = NewEmail();
        await (await browser.PostAsync("/api/auth/login-link", new { email })).Content.ReadAsStringAsync(Ct);
        var token = factory.Emails.LastToken(email, EmailTemplateKind.LoginLink.Code);
        time.Advance(TimeSpan.FromMinutes(15) + TimeSpan.FromSeconds(1));

        using var response = await browser.PostAsync("/api/auth/login-link/consume", new { token });

        var (status, code, _) = await ApiClient.ProblemAsync(response);
        Assert.Equal(HttpStatusCode.Gone, status);
        Assert.Equal("login_link.expired", code);
        using var me = await browser.GetAsync("/api/me");
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Fact]
    public async Task ConcurrentConsumes_SignInOnce_TheOtherGetsUsed()
    {
        await using var factory = Factory();
        var email = NewEmail();
        using var requester = factory.CreateApiClient();
        await (await requester.PostAsync("/api/auth/login-link", new { email })).Content.ReadAsStringAsync(Ct);
        var token = factory.Emails.LastToken(email, EmailTemplateKind.LoginLink.Code);
        var browsers = Enumerable.Range(0, 8).Select(_ => factory.CreateApiClient()).ToList();
        var csrf = await Task.WhenAll(browsers.Select(b => b.CsrfAsync()));

        var responses = await Task.WhenAll(browsers.Select((b, i) =>
            b.SendRawAsync(HttpMethod.Post, "/api/auth/login-link/consume", new { token }, csrf[i])));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        foreach (var failed in responses.Where(r => r.StatusCode != HttpStatusCode.OK))
        {
            var (status, code, _) = await ApiClient.ProblemAsync(failed);
            Assert.Equal(HttpStatusCode.Gone, status);
            Assert.Equal("login_link.used", code);
        }

        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM iam.users WHERE email = $1", email));
        browsers.ForEach(b => b.Dispose());
    }

    [Fact]
    public async Task FailedEmail_Returns503_AndTheTokenCannotBeUsed()
    {
        await using var factory = Factory();
        factory.Emails.Fail = true;
        using var browser = factory.CreateApiClient();
        var email = NewEmail();

        using var response = await browser.PostAsync("/api/auth/login-link", new { email });

        var (status, code, _) = await ApiClient.ProblemAsync(response);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, status);
        Assert.Equal("email.send_failed", code);
        Assert.Equal(2, factory.Emails.Attempts);
        Assert.True(await AdminScalarAsync<bool>("SELECT expires_at <= now() FROM iam.user_tokens WHERE email = $1", email));
    }

    [Fact]
    public async Task InvalidEmail_IsValidationFailedWithTheCodeOfTheField()
    {
        await using var factory = Factory();
        using var browser = factory.CreateApiClient();

        using var response = await browser.PostAsync("/api/auth/login-link", new { email = "jana@" });

        var (status, code, body) = await ApiClient.ProblemAsync(response);
        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("validation.failed", code);
        Assert.Equal(["email.invalid_format"], body.GetProperty("errors").GetProperty("email").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public async Task Token_IsNeverStored_NorInTheOutbox_NorInTheLogs()
    {
        await using var factory = Factory();
        using var browser = factory.CreateApiClient();
        var email = NewEmail();
        await browser.SignInByLinkAsync(factory, email);
        var token = factory.Emails.LastToken(email, EmailTemplateKind.LoginLink.Code);
        var hex = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Buffers.Text.Base64Url.DecodeFromChars(token)));

        var row = (await AdminRowsAsync("SELECT row_to_json(t)::text FROM iam.user_tokens t WHERE email = $1", email)).Single();
        Assert.DoesNotContain(token, (string)row[0]!, StringComparison.Ordinal);
        Assert.Equal(0L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.outbox WHERE payload::text LIKE '%' || $1 || '%'", token));
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM iam.user_tokens WHERE encode(token_hash, 'hex') = $1", hex));
        Assert.All(factory.Logs.Logs, l => Assert.DoesNotContain(token, l.AllText, StringComparison.Ordinal));
        Assert.All(factory.Logs.Logs, l => Assert.DoesNotContain(email, l.AllText, StringComparison.OrdinalIgnoreCase));
        _ = JsonSerializer.Serialize(row);
    }
}
