using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EshopGuard.Application.Email;

namespace EshopGuard.Api.Tests;

/// <summary>
/// A browser of the tests: keeps the cookies, sends every request from its own client address and every change with a
/// fresh <c>X-CSRF-TOKEN</c> (a token is bound to the user, so it is fetched again after a sign-in).
/// </summary>
internal sealed class ApiClient(HttpClient http, string ip) : IDisposable
{
    public HttpClient Http { get; } = http;

    public string Ip { get; } = ip;

    public string? AcceptLanguage { get; set; }

    /// <summary>A random private address, so the buckets of IP addresses of the tests do not mix.</summary>
    public static string RandomIp() => $"10.{Random.Shared.Next(256)}.{Random.Shared.Next(256)}.{Random.Shared.Next(1, 255)}";

    public async Task<string> CsrfAsync()
    {
        using var response = await SendRawAsync(HttpMethod.Get, "/api/auth/csrf", null, csrf: null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await JsonAsync(response)).GetProperty("token").GetString()!;
    }

    public Task<HttpResponseMessage> GetAsync(string path) => SendRawAsync(HttpMethod.Get, path, null, csrf: null);

    public Task<HttpResponseMessage> PostAsync(string path, object? body = null, bool csrf = true) => SendAsync(HttpMethod.Post, path, body, csrf);

    public Task<HttpResponseMessage> PutAsync(string path, object? body = null, bool csrf = true) => SendAsync(HttpMethod.Put, path, body, csrf);

    public Task<HttpResponseMessage> PatchAsync(string path, object? body = null, bool csrf = true) => SendAsync(HttpMethod.Patch, path, body, csrf);

    public Task<HttpResponseMessage> DeleteAsync(string path, bool csrf = true) => SendAsync(HttpMethod.Delete, path, null, csrf);

    public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body, bool csrf) =>
        await SendRawAsync(method, path, body, csrf ? await CsrfAsync() : null);

    public async Task<HttpResponseMessage> SendRawAsync(HttpMethod method, string path, object? body, string? csrf)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Add(ApiFactory.ClientIpHeader, Ip);
        if (csrf is not null)
        {
            request.Headers.Add("X-CSRF-TOKEN", csrf);
        }

        if (AcceptLanguage is not null)
        {
            request.Headers.TryAddWithoutValidation("Accept-Language", AcceptLanguage);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType());
        }

        return await Http.SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>Requests a sign-in link for the e-mail, takes its token from the captured e-mail and uses it.</summary>
    public async Task<JsonElement> SignInByLinkAsync(ApiFactory factory, string email, string? market = null)
    {
        ArgumentNullException.ThrowIfNull(factory);
        using (var requested = await PostAsync("/api/auth/login-link", new { email, market }))
        {
            Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        }

        var token = factory.Emails.LastToken(email, EmailTemplateKind.LoginLink.Code);
        using var consumed = await PostAsync("/api/auth/login-link/consume", new { token, market });
        Assert.Equal(HttpStatusCode.OK, consumed.StatusCode);
        return await JsonAsync(consumed);
    }

    public static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(string.IsNullOrEmpty(text) ? "null" : text);
        return document.RootElement.Clone();
    }

    /// <summary>Status and code of a problem answer (asserts its content type).</summary>
    public static async Task<(HttpStatusCode Status, string Code, JsonElement Body)> ProblemAsync(HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await JsonAsync(response);
        return (response.StatusCode, body.GetProperty("code").GetString()!, body);
    }

    public void Dispose() => Http.Dispose();
}
