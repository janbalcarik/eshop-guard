using System.Net;
using EshopGuard.Application.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EshopGuard.Api.Tests;

/// <summary>Errors as codes (change 9, task 1.6; specification „Chybové odpovědi jako kódy“).</summary>
public sealed class ProblemDetailsTests : ApiTestBase
{
    [Fact]
    public async Task UnexpectedException_Is500InternalError_WithoutItsText()
    {
        await using var factory = Factory(services: s => s.Replace(ServiceDescriptor.Singleton<IRefCatalog, ThrowingCatalog>()));
        using var browser = factory.CreateApiClient();

        using var response = await browser.GetAsync("/api/ref/locales");

        var text = await response.Content.ReadAsStringAsync(Ct);
        var (status, code, body) = await ApiClient.ProblemAsync(response);
        Assert.Equal(HttpStatusCode.InternalServerError, status);
        Assert.Equal("internal_error", code);
        Assert.False(string.IsNullOrEmpty(body.GetProperty("traceId").GetString()));
        Assert.DoesNotContain("SELECT", text, StringComparison.Ordinal);
        Assert.DoesNotContain("tajne_tabulky", text, StringComparison.Ordinal);
        Assert.DoesNotContain("   at ", text, StringComparison.Ordinal);
        Assert.False(body.TryGetProperty("detail", out _));
    }

    [Fact]
    public async Task Validation_HasErrorsByField_MissingBody_IsValueRequired()
    {
        await using var factory = Factory();
        using var browser = factory.CreateApiClient();

        using var response = await browser.PostAsync("/api/auth/login-link/inspect", null);

        var (status, code, body) = await ApiClient.ProblemAsync(response);
        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("validation.failed", code);
        Assert.Equal(["value.required"], body.GetProperty("errors").GetProperty("body").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal("urn:eshopguard:problem:validation.failed", body.GetProperty("type").GetString());
        Assert.Equal("validation.failed", body.GetProperty("title").GetString());
    }

    [Theory]
    [InlineData("GET", "/api/neexistuje", "not_found", 404)]
    [InlineData("GET", "/api/me", "auth.unauthenticated", 401)]
    [InlineData("POST", "/api/auth/login-link/inspect", "login_link.invalid", 404)]
    public async Task NoProblem_CarriesASentence(string method, string path, string expectedCode, int expectedStatus)
    {
        await using var factory = Factory();
        using var browser = factory.CreateApiClient();

        using var response = await browser.SendAsync(new HttpMethod(method), path, method == "POST" ? new { token = "neplatny" } : null, csrf: method == "POST");

        var (status, code, body) = await ApiClient.ProblemAsync(response);
        Assert.Equal(expectedStatus, (int)status);
        Assert.Equal(expectedCode, code);
        Assert.False(body.TryGetProperty("detail", out _));
        Assert.Equal(System.Text.Json.JsonValueKind.Object, body.GetProperty("params").ValueKind);
    }

    private sealed class ThrowingCatalog : IRefCatalog
    {
        public Task<IReadOnlyList<LocaleInfo>> GetLocalesAsync(CancellationToken ct = default) =>
            throw new InvalidOperationException("SELECT * FROM tajne_tabulky WHERE heslo = 'x'");

        public Task<IReadOnlyList<MarketInfo>> GetMarketsAsync(CancellationToken ct = default) => throw new NotSupportedException();
    }
}
