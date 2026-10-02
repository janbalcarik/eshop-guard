using System.Net;
using EshopGuard.Api.Auth;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace EshopGuard.Api.Tests.Auth;

/// <summary>CSRF on every change (change 9, task 6.6; specification „Relace, cookies a ochrana CSRF“).</summary>
public sealed class CsrfTests : ApiTestBase
{
    [Fact]
    public async Task EveryChangingEndpoint_WithoutTheHeader_Is400CsrfInvalid()
    {
        await using var factory = Factory();
        using var browser = factory.CreateApiClient();
        var session = await browser.SignInByLinkAsync(factory, NewEmail());
        var tenantId = session.GetProperty("createdTenantId").GetGuid();
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .SelectMany(e => (e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? []).Select(m => (Method: m, Pattern: "/" + e.RoutePattern.RawText!.TrimStart('/'))))
            .Where(e => e.Pattern.StartsWith("/api/", StringComparison.Ordinal) && CsrfEndpointFilter.IsUnsafe(e.Method))
            .ToList();
        Assert.True(endpoints.Count >= 25, $"only {endpoints.Count} changing endpoints found");

        var failures = new List<string>();
        foreach (var (method, pattern) in endpoints)
        {
            var path = pattern.Replace("{tenantId:guid}", tenantId.ToString("D"), StringComparison.Ordinal)
                .Replace("{userId:guid}", Guid.NewGuid().ToString("D"), StringComparison.Ordinal)
                .Replace("{invitationId:guid}", Guid.NewGuid().ToString("D"), StringComparison.Ordinal);
            using var response = await browser.SendAsync(new HttpMethod(method), path, new { }, csrf: false);
            if (response.StatusCode != HttpStatusCode.BadRequest || (await ApiClient.ProblemAsync(response)).Code != "csrf.invalid")
            {
                failures.Add($"{method} {pattern}: {(int)response.StatusCode}");
            }
        }

        Assert.Empty(failures);
    }

    [Fact]
    public async Task PatchMe_WithoutTheHeader_ChangesNothing()
    {
        await using var factory = Factory();
        using var browser = factory.CreateApiClient();
        var email = NewEmail();
        await browser.SignInByLinkAsync(factory, email);

        using var response = await browser.PatchAsync("/api/me", new { displayName = "Útočník" }, csrf: false);

        Assert.Equal("csrf.invalid", (await ApiClient.ProblemAsync(response)).Code);
        Assert.Null(await AdminScalarAsync<string>("SELECT display_name FROM iam.users WHERE email = $1", email));
    }

    [Fact]
    public async Task LoginLink_WithoutTheHeader_IsRefusedBeforeSignIn()
    {
        await using var factory = Factory();
        using var browser = factory.CreateApiClient();
        var email = NewEmail();

        using var response = await browser.PostAsync("/api/auth/login-link", new { email }, csrf: false);

        Assert.Equal("csrf.invalid", (await ApiClient.ProblemAsync(response)).Code);
        Assert.Empty(factory.Emails.To(email));
    }
}
