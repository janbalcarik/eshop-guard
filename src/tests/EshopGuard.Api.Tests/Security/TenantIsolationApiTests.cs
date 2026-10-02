using System.Net;
using EshopGuard.Application;
using EshopGuard.Data;
using EshopGuard.Data.Tenancy;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EshopGuard.Api.Tests.Security;

/// <summary>Two tenants, both with the shop <c>vegis.sk</c> and an invitation (change 9, task 11.2).</summary>
internal sealed class TwoTenantsFixture : IAsyncDisposable
{
    private TwoTenantsFixture(ApiFactory factory, Person a, Person b)
    {
        Factory = factory;
        A = a;
        B = b;
    }

    public ApiFactory Factory { get; }

    public Person A { get; }

    public Person B { get; }

    public static async Task<TwoTenantsFixture> CreateAsync(ApiFactory factory, Func<string, object?[], Task<int>> admin)
    {
        var a = await People.OwnerAsync(factory);
        var b = await People.OwnerAsync(factory);
        foreach (var tenant in new[] { a.TenantId, b.TenantId })
        {
            await admin(
                "INSERT INTO shop.shops (id, tenant_id, domain, base_url, base_path, home_country, platform, source_mode, status, created_at, updated_at) " +
                "VALUES ($1, $2, 'vegis.sk', 'https://vegis.sk/', '/', 'SK', 'unknown', 'web', 'draft', now(), now())", [Guid.CreateVersion7(), tenant]);
        }

        using (var invited = await a.Browser.PostAsync($"/api/t/{a.TenantId}/invitations", new { email = People.NewEmail("a"), role = "viewer" }))
        {
            Assert.Equal(HttpStatusCode.Created, invited.StatusCode);
        }

        using (var invited = await b.Browser.PostAsync($"/api/t/{b.TenantId}/invitations", new { email = People.NewEmail("b"), role = "viewer" }))
        {
            Assert.Equal(HttpStatusCode.Created, invited.StatusCode);
        }

        return new TwoTenantsFixture(factory, a, b);
    }

    public ValueTask DisposeAsync()
    {
        A.Dispose();
        B.Dispose();
        return ValueTask.CompletedTask;
    }
}

/// <summary>A user of tenant A never reaches tenant B (change 9, task 11.2; specification „Přístup k tenantovi jen přes členství“).</summary>
public sealed class TenantIsolationApiTests : ApiTestBase
{
    [Fact]
    public async Task EveryEndpointOfTenantB_Is404ForAUserOfA_LikeARandomTenant()
    {
        await using var factory = Factory();
        await using var tenants = await TwoTenantsFixture.CreateAsync(factory, AdminAsync);
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Select(e => (Pattern: "/" + (e.RoutePattern.RawText ?? string.Empty).TrimStart('/'), Methods: e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? []))
            .Where(e => e.Pattern.StartsWith("/api/t/", StringComparison.Ordinal))
            .SelectMany(e => e.Methods.Select(m => (Method: m, e.Pattern)))
            .ToList();
        Assert.NotEmpty(endpoints);

        var failures = new List<string>();
        foreach (var (method, pattern) in endpoints)
        {
            foreach (var tenant in new[] { tenants.B.TenantId, Guid.NewGuid() })
            {
                var path = pattern.Replace("{tenantId:guid}", tenant.ToString("D"), StringComparison.Ordinal)
                    .Replace("{userId:guid}", tenants.B.UserId.ToString("D"), StringComparison.Ordinal)
                    .Replace("{invitationId:guid}", Guid.NewGuid().ToString("D"), StringComparison.Ordinal);
                using var response = await tenants.A.Browser.SendAsync(new HttpMethod(method), path, method == "GET" ? null : new { }, csrf: method != "GET");
                if (response.StatusCode != HttpStatusCode.NotFound || (await ApiClient.ProblemAsync(response)).Code != "tenant.not_found")
                {
                    failures.Add($"{method} {pattern} ({(tenant == tenants.B.TenantId ? "B" : "random")}): {(int)response.StatusCode}");
                }
            }
        }

        Assert.Empty(failures);
        var me = await People.MeAsync(tenants.A.Browser);
        Assert.DoesNotContain(me.GetProperty("memberships").EnumerateArray(), m => m.GetProperty("tenantId").GetGuid() == tenants.B.TenantId);
        Assert.Equal("Viewer", await AdminScalarAsync<string>("SELECT initcap(role) FROM iam.invitations WHERE tenant_id = $1 LIMIT 1", tenants.B.TenantId));
    }

    [Fact]
    public async Task PlainSqlWithoutATenantCondition_ReturnsOnlyTheTenantOfTheContext()
    {
        await using var factory = Factory();
        await using var tenants = await TwoTenantsFixture.CreateAsync(factory, AdminAsync);
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenants.A.TenantId, tenants.A.UserId);
        var db = scope.ServiceProvider.GetRequiredService<EshopGuardDb>();

        var seen = await db.ExecuteInTenantTransactionAsync(async () =>
        {
            var invitations = await db.Database.SqlQueryRaw<Guid>("SELECT tenant_id AS \"Value\" FROM iam.invitations").ToListAsync(Ct);
            var shops = await db.Database.SqlQueryRaw<Guid>("SELECT tenant_id AS \"Value\" FROM shop.shops WHERE domain = 'vegis.sk'").ToListAsync(Ct);
            var memberships = await db.Database.SqlQueryRaw<Guid>("SELECT tenant_id AS \"Value\" FROM iam.memberships").ToListAsync(Ct);
            return invitations.Concat(shops).Concat(memberships).Distinct().ToList();
        }, Ct);

        Assert.Equal([tenants.A.TenantId], seen);
        _ = scope.ServiceProvider.GetRequiredService<RequestContext>();
    }
}
