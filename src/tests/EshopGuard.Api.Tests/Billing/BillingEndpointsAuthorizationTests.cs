using System.Net;
using EshopGuard.Api.Tests.Shops;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using static EshopGuard.Api.Tests.Billing.InvoiceSeed;
using static EshopGuard.Api.Tests.Billing.OrderFlowTests;
using static EshopGuard.Api.Tests.Billing.SubscriptionFlowTests;

namespace EshopGuard.Api.Tests.Billing;

/// <summary>
/// Who may pay and see the invoices (change 12, task 11.4): every endpoint of billing (groups 5, 7, 9 and 11) is for owner and
/// admin only (editor and viewer <c>403 auth.forbidden_role</c>), a user outside the tenant gets <c>404 tenant.not_found</c>, and
/// every object of tenant B under the address of A is <c>404</c> without a change of B, a call of Stripe or a signed link.
/// </summary>
public sealed class BillingEndpointsAuthorizationTests : ShopTestBase
{
    private const string Prefix = "/api/t/{tenantId:guid}";

    /// <summary>The endpoints of billing of a tenant (the matrix of all endpoints is <c>RoleMatrixTests</c>).</summary>
    private static readonly string[] Expected =
    [
        $"POST {Prefix}/shops/{{shopId:guid}}/orders",
        $"GET {Prefix}/orders/{{orderId:guid}}",
        $"POST {Prefix}/orders/{{orderId:guid}}/checkout",
        $"POST {Prefix}/orders/{{orderId:guid}}/pay-with-saved-card",
        $"GET {Prefix}/billing/overview",
        $"GET {Prefix}/billing/details",
        $"PUT {Prefix}/billing/details",
        $"POST {Prefix}/billing/card/portal-session",
        $"POST {Prefix}/billing/card/setup-intent",
        $"POST {Prefix}/shops/{{shopId:guid}}/subscription/cancel",
        $"POST {Prefix}/shops/{{shopId:guid}}/subscription/resume",
        $"POST {Prefix}/shops/{{shopId:guid}}/subscription",
        $"GET {Prefix}/invoices",
        $"GET {Prefix}/invoices/zip",
        $"GET {Prefix}/invoices/{{invoiceId:guid}}/pdf",
    ];

    /// <summary>The rows of B whose content must not change (any column).</summary>
    private const string RowsOfB = """
        SELECT md5(coalesce(string_agg(r, '|' ORDER BY r), '')) FROM (
            SELECT t::text AS r FROM billing.orders t WHERE tenant_id = $1
            UNION ALL SELECT t::text FROM billing.subscriptions t WHERE tenant_id = $1
            UNION ALL SELECT t::text FROM billing.payments t WHERE tenant_id = $1
            UNION ALL SELECT t::text FROM billing.invoices t WHERE tenant_id = $1
            UNION ALL SELECT t::text FROM shop.shops t WHERE tenant_id = $1
            UNION ALL SELECT t::text FROM iam.tenants t WHERE id = $1) rows
        """;

    [Fact]
    public async Task EveryBillingEndpoint_IsForOwnerAndAdmin_AndAUserOutsideTheTenantGets404()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        using var admin = await People.MemberAsync(factory, owner, "admin");
        using var editor = await People.MemberAsync(factory, owner, "editor");
        using var viewer = await People.MemberAsync(factory, owner, "viewer");
        using var stranger = await People.OwnerAsync(factory);
        var endpoints = Endpoints(factory);
        Assert.Equal(Expected.Order(StringComparer.Ordinal), endpoints.Select(e => $"{e.Method} {e.Pattern}").Order(StringComparer.Ordinal));

        var failures = new List<string>();
        foreach (var (method, pattern) in endpoints)
        {
            var path = RoutePaths.Fill(pattern, owner.TenantId);
            foreach (var (role, person, allowed) in new[] { ("owner", owner, true), ("admin", admin, true), ("editor", editor, false), ("viewer", viewer, false) })
            {
                using var response = await SendAsync(person, method, path);
                var forbidden = response.StatusCode == HttpStatusCode.Forbidden && (await ApiClient.ProblemAsync(response)).Code == "auth.forbidden_role";
                if (forbidden == allowed || (!allowed && response.Headers.Location is not null))
                {
                    failures.Add($"{method} {pattern} as {role}: {(int)response.StatusCode}");
                }
            }

            using var outside = await SendAsync(stranger, method, path);
            var problem = await ApiClient.ProblemAsync(outside);
            if ((problem.Status, problem.Code) != (HttpStatusCode.NotFound, "tenant.not_found"))
            {
                failures.Add($"{method} {pattern} outside the tenant: {(int)outside.StatusCode} {problem.Code}");
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public async Task EveryObjectOfTenantB_Is404UnderTheAddressOfA_WithoutAChangeOfB_AStripeCallOrALink()
    {
        await using var factory = Factory();
        var b = await ReadyAsync(factory);
        using var _ = b.Owner;
        var order = await OrderIdAsync(b);
        await CheckoutAsync(b, order);
        await SubscribedAsync(factory, b.Owner.TenantId, b.ShopId, b.PriceListId, "t20000", 59m, "active", DateTimeOffset.UtcNow.AddDays(20), ordinal: 1, orderId: order);
        var invoice = await DocumentAsync(factory, b.Owner.TenantId, b.ShopId, "2026000001", At(2026, 10, 5), 199m);
        using var a = await People.OwnerAsync(factory);
        await CardAsync(factory, a.TenantId);
        var before = await AdminScalarAsync<string>(RowsOfB, b.Owner.TenantId);
        var calls = factory.Stripe.Calls.Count;
        var ids = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["shopId"] = b.ShopId.ToString("D"),
            ["orderId"] = order.ToString("D"),
            ["invoiceId"] = invoice.ToString("D"),
        };
        var paths = Endpoints(factory)
            .Where(e => e.Pattern.Count(c => c == '{') > 1)
            .Select(e => (e.Method, Path: RoutePaths.Fill(e.Pattern, a.TenantId, ids)))
            .Append(("GET", $"/api/t/{a.TenantId}/invoices?shopId={b.ShopId}"))
            .Append(("GET", $"/api/t/{a.TenantId}/invoices/zip?shopId={b.ShopId}"))
            .ToList();
        Assert.Equal(10, paths.Count);

        var failures = new List<string>();
        foreach (var (method, path) in paths)
        {
            // A valid order of B's own quote, so the 404 comes from the e-shop and not from the body.
            object? body = method == "GET" ? null : path.EndsWith("/orders", StringComparison.Ordinal)
                ? new { quoteId = b.QuoteId, scopeHash = b.ScopeHash, termsVersion = "test-terms-1" }
                : new { };
            using var response = await a.Browser.SendAsync(new HttpMethod(method), path, body, csrf: method != "GET");
            if (response.StatusCode != HttpStatusCode.NotFound || response.Headers.Location is not null)
            {
                failures.Add($"{method} {path}: {(int)response.StatusCode} {(await ApiClient.ProblemAsync(response)).Code}");
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
        Assert.Equal(before, await AdminScalarAsync<string>(RowsOfB, b.Owner.TenantId));
        Assert.Equal(calls, factory.Stripe.Calls.Count);
    }

    private static List<(string Method, string Pattern)> Endpoints(ApiFactory factory) =>
        factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(e => e.Metadata.GetMetadata<ITagsMetadata>()?.Tags.Contains("billing") == true)
            .Select(e => (Pattern: "/" + (e.RoutePattern.RawText ?? string.Empty).TrimStart('/'), Methods: e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? []))
            .Where(e => e.Pattern.StartsWith("/api/t/", StringComparison.Ordinal))
            .SelectMany(e => e.Methods.Select(m => (m, e.Pattern)))
            .ToList();

    private static Task<HttpResponseMessage> SendAsync(Person person, string method, string path) =>
        person.Browser.SendAsync(new HttpMethod(method), path, method == "GET" ? null : new { }, csrf: method != "GET");
}
