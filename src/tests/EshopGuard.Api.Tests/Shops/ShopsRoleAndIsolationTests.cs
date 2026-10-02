using System.Net;
using EshopGuard.Api.Tests.Security;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace EshopGuard.Api.Tests.Shops;

/// <summary>
/// E-shops of another tenant (change 10, task 11.1; specification „Oprávnění rolí a izolace e-shopů přes API“): the e-shop of
/// tenant B under the address of tenant A is <c>404 shop.not_found</c> on every endpoint, with a valid body, and the rows of
/// B do not change. The roles of every endpoint are in <see cref="RoleMatrixTests"/>.
/// </summary>
public sealed class ShopsRoleAndIsolationTests : ShopTestBase
{
    private static readonly Dictionary<string, object?> Bodies = new(StringComparer.Ordinal)
    {
        ["PATCH /api/t/{tenantId:guid}/shops/{shopId:guid}"] = new { name = "x", version = 1 },
        ["PUT /api/t/{tenantId:guid}/shops/{shopId:guid}/platform"] = new { platform = "shoptet" },
        ["PUT /api/t/{tenantId:guid}/shops/{shopId:guid}/source"] = new { mode = "web" },
        ["PUT /api/t/{tenantId:guid}/shops/{shopId:guid}/markets"] = new { active = new[] { "sk" } },
        ["POST /api/t/{tenantId:guid}/shops/{shopId:guid}/languages/{language}/confirmation"] = new { belongsToShop = true },
        ["PUT /api/t/{tenantId:guid}/shops/{shopId:guid}/languages/{language}/exclusion"] = new { excluded = true },
        ["POST /api/t/{tenantId:guid}/shops/{shopId:guid}/quote"] = new { activeMarkets = new[] { "sk" } },
        ["POST /api/t/{tenantId:guid}/shops/{shopId:guid}/ownership/verifications"] = new { method = "dns" },
        ["PATCH /api/t/{tenantId:guid}/shops/{shopId:guid}/settings"] = new { name = "x", version = 1 },
    };

    [Fact]
    public async Task ShopOfTenantB_UnderTenantA_Is404Everywhere_AndItsRowsStay()
    {
        await using var factory = Factory();
        await using var tenants = await TwoTenantsFixture.CreateAsync(factory, AdminAsync);
        await SeedBylinkovoAsync(tenants.B.TenantId, tenants.ShopB, "vegis.sk");
        var verification = Guid.CreateVersion7();
        await AdminAsync("INSERT INTO shop.shop_verifications (id, tenant_id, shop_id, method, token, status, created_at, updated_at) VALUES ($1, $2, $3, 'dns', 'tokenB', 'pending', now(), now())",
            verification, tenants.B.TenantId, tenants.ShopB);
        var before = await SnapshotAsync(tenants.ShopB);
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .SelectMany(e => (e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? []).Select(m => (Method: m, Pattern: "/" + e.RoutePattern.RawText!.TrimStart('/'))))
            .Where(e => e.Pattern.Contains("{shopId:guid}", StringComparison.Ordinal))
            .ToList();
        Assert.True(endpoints.Count >= 20, $"only {endpoints.Count} endpoints of an e-shop");

        var failures = new List<string>();
        foreach (var (method, pattern) in endpoints)
        {
            var path = RoutePaths.Fill(pattern, tenants.A.TenantId, new Dictionary<string, string>
            {
                ["shopId"] = tenants.ShopB.ToString("D"),
                ["verificationId"] = verification.ToString("D"),
            });
            var body = Bodies.GetValueOrDefault($"{method} {pattern}");
            using var response = await tenants.A.Browser.SendAsync(new HttpMethod(method), path, method == "GET" ? null : body, csrf: method != "GET");
            if (response.StatusCode != HttpStatusCode.NotFound || (await ApiClient.ProblemAsync(response)).Code != "shop.not_found")
            {
                failures.Add($"{method} {pattern}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync(Ct)}");
            }
        }

        Assert.Empty(failures);
        Assert.Equal(before, await SnapshotAsync(tenants.ShopB));
    }

    [Fact]
    public async Task EditorMayNotStartTheSample()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        using var editor = await People.MemberAsync(factory, owner, "editor");
        var shopId = (await CreateShopAsync(owner)).GetProperty("id").GetGuid();

        using var response = await editor.Browser.PostAsync($"/api/t/{owner.TenantId}/shops/{shopId}/sample");

        var problem = await ApiClient.ProblemAsync(response);
        Assert.Equal((HttpStatusCode.Forbidden, "auth.forbidden_role"), (problem.Status, problem.Code));
        Assert.Equal("admin", problem.Body.GetProperty("params").GetProperty("requiredRole").GetString());
    }

    private static async Task<string> SnapshotAsync(Guid shopId)
    {
        var rows = new List<object?[]>();
        rows.AddRange(await AdminRowsAsync("SELECT name, platform, source_mode, status, deleted_at::text, xmin::text FROM shop.shops WHERE id = $1", shopId));
        rows.AddRange(await AdminRowsAsync("SELECT country_code, status, confirmed_at::text, updated_at::text FROM shop.shop_markets WHERE shop_id = $1 ORDER BY 1", shopId));
        rows.AddRange(await AdminRowsAsync("SELECT language, status, decided_at::text, updated_at::text FROM shop.shop_languages WHERE shop_id = $1 ORDER BY 1", shopId));
        rows.AddRange(await AdminRowsAsync("SELECT method, status, updated_at::text FROM shop.shop_verifications WHERE shop_id = $1 ORDER BY 1", shopId));
        rows.AddRange(await AdminRowsAsync("SELECT count(*)::text FROM checks.runs WHERE shop_id = $1", shopId));
        return string.Join("\n", rows.Select(r => string.Join("|", r.Select(v => v?.ToString()))));
    }
}
