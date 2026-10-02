using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace EshopGuard.Api.Tests.Security;

/// <summary>
/// <c>/api</c> only over HTTPS (found by the manual click-through over <c>http://localhost</c>, change 9 task 11.5): plain HTTP
/// gets <c>400 request.https_required</c>; behind a known proxy (loopback) <c>X-Forwarded-Proto: https</c> counts, from anyone
/// else it does not.
/// </summary>
public sealed class HttpsTests : ApiTestBase
{
    [Fact]
    public async Task PlainHttp_Is400HttpsRequired_NotAnErrorOfTheServer()
    {
        await using var factory = Factory();
        using var http = Client(factory);

        using var response = await http.GetAsync("/api/auth/csrf", Ct);

        var (status, code, _) = await ApiClient.ProblemAsync(response);
        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("request.https_required", code);
        using var health = await http.GetAsync("/health", Ct);
        Assert.NotEqual(HttpStatusCode.BadRequest, health.StatusCode);
    }

    [Theory]
    [InlineData("127.0.0.1", HttpStatusCode.OK)]
    [InlineData("203.0.113.9", HttpStatusCode.BadRequest)]
    public async Task ForwardedProto_CountsOnlyFromAKnownProxy(string proxy, HttpStatusCode expected)
    {
        await using var factory = Factory();
        using var http = Client(factory);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/csrf");
        request.Headers.Add(ApiFactory.ClientIpHeader, proxy);
        request.Headers.Add("X-Forwarded-For", "198.51.100.20");
        request.Headers.Add("X-Forwarded-Proto", "https");

        using var response = await http.SendAsync(request, Ct);

        Assert.Equal(expected, response.StatusCode);
    }

    private static HttpClient Client(ApiFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("http://localhost"), AllowAutoRedirect = false });
}
