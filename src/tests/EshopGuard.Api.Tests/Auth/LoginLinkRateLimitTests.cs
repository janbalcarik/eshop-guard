using System.Net;
using Microsoft.Extensions.Time.Testing;

namespace EshopGuard.Api.Tests.Auth;

/// <summary>Limits of sign-in links (change 9, task 4.8; specification „Limity odesílání odkazu“).</summary>
public sealed class LoginLinkRateLimitTests : ApiTestBase
{
    [Fact]
    public async Task SecondLinkAfter18Seconds_IsRefused_With42SecondsToWait()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = Factory(time);
        using var browser = factory.CreateApiClient();
        var email = NewEmail();
        using (var first = await browser.PostAsync("/api/auth/login-link", new { email }))
        {
            Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        }

        time.Advance(TimeSpan.FromSeconds(18));
        using var second = await browser.PostAsync("/api/auth/login-link", new { email });

        var (status, code, body) = await ApiClient.ProblemAsync(second);
        Assert.Equal(HttpStatusCode.TooManyRequests, status);
        Assert.Equal("rate_limited", code);
        Assert.Equal("email_cooldown", body.GetProperty("params").GetProperty("scope").GetString());
        Assert.Equal(42, body.GetProperty("params").GetProperty("retryAfterSeconds").GetInt32());
        Assert.Equal("42", second.Headers.RetryAfter?.ToString());
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM iam.user_tokens WHERE email = $1", email));
        Assert.True(await AdminScalarAsync<bool>("SELECT expires_at > now() FROM iam.user_tokens WHERE email = $1", email));
    }

    [Fact]
    public async Task SixthLinkInAnHour_IsRefused_TheSameForAnExistingAndAnUnknownAccount()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = Factory(time);
        var existing = NewEmail();
        using (var browser = factory.CreateApiClient())
        {
            await browser.SignInByLinkAsync(factory, existing);
        }

        var unknown = NewEmail("novy");
        var answers = new List<string>();
        foreach (var email in new[] { existing, unknown })
        {
            for (var i = email == existing ? 1 : 0; i < 5; i++)
            {
                time.Advance(TimeSpan.FromSeconds(61));
                using var browser = factory.CreateApiClient();
                using var accepted = await browser.PostAsync("/api/auth/login-link", new { email });
                Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
            }

            time.Advance(TimeSpan.FromSeconds(61));
            using var sixth = await factory.CreateApiClient().PostAsync("/api/auth/login-link", new { email });
            var (status, code, body) = await ApiClient.ProblemAsync(sixth);
            Assert.Equal(HttpStatusCode.TooManyRequests, status);
            Assert.Equal("rate_limited", code);
            Assert.Equal("email_hourly", body.GetProperty("params").GetProperty("scope").GetString());
            answers.Add(body.GetProperty("params").GetRawText());
        }

        Assert.Equal(answers[0], answers[1]);
    }

    [Fact]
    public async Task TwentyFirstRequestFromOneIp_IsRefused_AndTheKeyHoldsNoReadableAddress()
    {
        await using var factory = Factory();
        const string ip = "198.51.100.77";
        for (var i = 0; i < 20; i++)
        {
            using var browser = factory.CreateApiClient(ip);
            using var accepted = await browser.PostAsync("/api/auth/login-link", new { email = NewEmail() });
            Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        }

        var last = NewEmail();
        using var refused = await factory.CreateApiClient(ip).PostAsync("/api/auth/login-link", new { email = last });

        var (status, code, body) = await ApiClient.ProblemAsync(refused);
        Assert.Equal(HttpStatusCode.TooManyRequests, status);
        Assert.Equal("rate_limited", code);
        Assert.Equal("ip_hourly", body.GetProperty("params").GetProperty("scope").GetString());
        Assert.Equal(0L, await AdminScalarAsync<long>("SELECT count(*) FROM iam.user_tokens WHERE email = $1", last));
        Assert.Equal(0L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.rate_limit_buckets WHERE key LIKE '%' || $1 || '%'", ip));
        var hash = new Application.Security.IpHasher(Microsoft.Extensions.Options.Options.Create(new Application.Options.SecurityOptions { IpHashKey = factory.IpHashKey }))
            .HashIp(IPAddress.Parse(ip));
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.rate_limit_buckets WHERE key = $1", "auth:link:ip:" + hash));
    }

    [Fact]
    public async Task TwoInstancesOfTheApi_ShareTheLimitThroughTheDatabase()
    {
        var key = ApiFactory.NewKey();
        await using var first = Factory(ipHashKey: key);
        await using var second = Factory(ipHashKey: key);
        var ip = ApiClient.RandomIp();
        for (var i = 0; i < 20; i++)
        {
            var instance = i % 2 == 0 ? first : second;
            using var accepted = await instance.CreateApiClient(ip).PostAsync("/api/auth/login-link", new { email = NewEmail() });
            Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        }

        using var refused = await first.CreateApiClient(ip).PostAsync("/api/auth/login-link", new { email = NewEmail() });
        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        using var refusedToo = await second.CreateApiClient(ip).PostAsync("/api/auth/login-link", new { email = NewEmail() });
        Assert.Equal(HttpStatusCode.TooManyRequests, refusedToo.StatusCode);
    }

    [Fact]
    public async Task BurstLimiterOfTheInstance_AnswersRateLimitedAsAProblem()
    {
        await using var factory = Factory(settings: new Dictionary<string, string?> { ["RateLimiting:AuthPerMinute"] = "3" });
        using var browser = factory.CreateApiClient();
        for (var i = 0; i < 3; i++)
        {
            using var ok = await browser.GetAsync("/api/auth/csrf");
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        }

        using var refused = await browser.GetAsync("/api/auth/csrf");
        var (status, code, body) = await ApiClient.ProblemAsync(refused);
        Assert.Equal(HttpStatusCode.TooManyRequests, status);
        Assert.Equal("rate_limited", code);
        Assert.Equal("burst", body.GetProperty("params").GetProperty("scope").GetString());
    }
}
