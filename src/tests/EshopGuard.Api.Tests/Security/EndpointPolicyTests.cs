using EshopGuard.Api.Auth;
using EshopGuard.Api.Tenancy;
using EshopGuard.Application.Tenants;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;

namespace EshopGuard.Api.Tests.Security;

/// <summary>An endpoint without its policy stops the start (change 9, task 7.3; specification „Relace, cookies a ochrana CSRF“).</summary>
public sealed class EndpointPolicyTests : ApiTestBase
{
    [Fact]
    public async Task EveryEndpointOfTheApi_HasItsPolicies()
    {
        await using var factory = Factory();
        using var client = factory.CreateApiClient();

        Assert.Empty(EndpointPolicyValidator.Validate(factory.Services.GetRequiredService<EndpointDataSource>()));
    }

    [Fact]
    public async Task TenantEndpointWithoutRole_AndChangeWithoutCsrf_StopTheStartAndAreNamed()
    {
        var source = new DefaultEndpointDataSource(
            Endpoint("POST", "/api/t/{tenantId:guid}/x", new CsrfProtectedMetadata()),
            Endpoint("POST", "/api/shops", new TenantRoleMetadata(TenantRole.Admin)),
            Endpoint("POST", "/api/me/x", new DisableCsrfMetadata()),
            Endpoint("POST", "/api/webhooks/stripe", new DisableCsrfMetadata()),
            Endpoint("GET", "/api/t/{tenantId:guid}/y", new TenantRoleMetadata(TenantRole.Viewer)));

        var problems = EndpointPolicyValidator.Validate(source);

        Assert.Equal(
        [
            "POST /api/t/{tenantId:guid}/x: RequireTenantRole",
            "POST /api/shops: RequireCsrf",
            "POST /api/me/x: DisableCsrf outside /api/webhooks/",
        ], problems);
        var validator = new EndpointPolicyValidator(source);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => validator.StartAsync(Ct));
        Assert.Contains("/api/t/{tenantId:guid}/x", ex.Message, StringComparison.Ordinal);
    }

    private static RouteEndpoint Endpoint(string method, string pattern, params object[] metadata) =>
        new(_ => Task.CompletedTask, RoutePatternFactory.Parse(pattern), 0,
            new EndpointMetadataCollection([new HttpMethodMetadata([method]), .. metadata]), pattern);
}
