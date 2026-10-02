using System.Net;
using EshopGuard.Api.Tests.Fakes;
using EshopGuard.Application.Fixes;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using static EshopGuard.Api.Tests.Findings.ShopSeed;

namespace EshopGuard.Api.Tests.Findings;

/// <summary>
/// Two tenants with the same e-shop „bylinkovo.sk“ and the same texts of findings (change 11, task 12.1): every object of
/// tenant B under the address of tenant A is <c>404</c> on every endpoint that names an object, and no row of B changes.
/// The roles of every endpoint are checked by <c>RoleMatrixTests</c>.
/// </summary>
public sealed class FindingsRoleAndIsolationTests : FindingsTestBase
{
    /// <summary>The tags of the endpoints of this change.</summary>
    private static readonly string[] ChangeTags = ["fixes", "evidence", "runs", "notifications"];

    /// <summary>The rows of B whose content must not change (any column, the version included).</summary>
    private const string RowsOfB = """
        SELECT md5(coalesce(string_agg(r, '|' ORDER BY r), '')) FROM (
            SELECT t::text AS r FROM shop.shops t WHERE tenant_id = $1
            UNION ALL SELECT t::text FROM checks.findings t WHERE tenant_id = $1
            UNION ALL SELECT t::text FROM checks.questions t WHERE tenant_id = $1
            UNION ALL SELECT t::text FROM checks.runs t WHERE tenant_id = $1
            UNION ALL SELECT t::text FROM fixes.fix_proposals t WHERE tenant_id = $1
            UNION ALL SELECT t::text FROM fixes.fix_groups t WHERE tenant_id = $1
            UNION ALL SELECT t::text FROM fixes.publications t WHERE tenant_id = $1
            UNION ALL SELECT t::text FROM fixes.protocols t WHERE tenant_id = $1
            UNION ALL SELECT t::text FROM fixes.evidence_items t WHERE tenant_id = $1
            UNION ALL SELECT t::text FROM fixes.evidence_links t WHERE tenant_id = $1
            UNION ALL SELECT t::text FROM fixes.decision_memory t WHERE tenant_id = $1
            UNION ALL SELECT t::text FROM iam.notifications t WHERE tenant_id = $1) rows
        """;

    [Fact]
    public async Task EveryObjectOfTenantB_Is404UnderTheAddressOfA_AndNothingOfBChanges()
    {
        await using var factory = Factory(services: s => s.AddSingleton<IFixPublisher, FakeFixPublisher>());
        using var a = await People.OwnerAsync(factory);
        using var b = await People.OwnerAsync(factory);
        var shopA = await BylinkovoSeed.SeedAsync(factory, a);
        var shopB = await BylinkovoSeed.SeedAsync(factory, b);
        var ids = await ObjectsOfBAsync(b, shopB);
        var before = await ScalarAsync<string>(RowsOfB, b.TenantId);

        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Select(e => (Pattern: "/" + (e.RoutePattern.RawText ?? string.Empty).TrimStart('/'), Methods: e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [],
                Ours: e.Metadata.GetMetadata<ITagsMetadata>()?.Tags.Any(ChangeTags.Contains) == true))
            .Where(e => e.Pattern.StartsWith("/api/t/", StringComparison.Ordinal) && e.Pattern.Count(c => c == '{') > 1)
            .SelectMany(e => e.Methods.Select(m => (Method: m, e.Pattern, e.Ours)))
            .ToList();
        Assert.Contains(endpoints, e => e.Ours);
        var failures = new List<string>();
        foreach (var (method, pattern, ours) in endpoints)
        {
            var path = RoutePaths.Fill(pattern, a.TenantId, ids);
            using var response = await a.Browser.SendAsync(new HttpMethod(method), path, method == "GET" ? null : new { }, csrf: method != "GET");
            // The endpoints of this change answer 404; those of changes 9 and 10 may refuse the empty body first, never succeed.
            if (ours ? response.StatusCode != HttpStatusCode.NotFound : response.IsSuccessStatusCode)
            {
                failures.Add($"{method} {pattern}: {(int)response.StatusCode} {(await ApiClient.ProblemAsync(response)).Code}");
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
        Assert.Equal(before, await ScalarAsync<string>(RowsOfB, b.TenantId));
        // The same texts exist in both tenants; A still sees only its own.
        using var list = await a.Browser.GetAsync($"{S(a, shopA.ShopId)}/findings?limit=100");
        Assert.DoesNotContain((await ApiClient.JsonAsync(list)).GetProperty("items").EnumerateArray(), i => shopB.Findings.ContainsValue(i.GetProperty("findingId").GetGuid()));
    }

    /// <summary>One object of every kind of tenant B, by the name of its parameter in the routes.</summary>
    private static async Task<Dictionary<string, string>> ObjectsOfBAsync(Person b, BylinkovoSeed shop)
    {
        var evidence = Guid.CreateVersion7();
        await ExecuteAsync(
            "INSERT INTO fixes.evidence_items (id, tenant_id, claim_text, subject_kind, kind, source, status, created_at, updated_at) VALUES ($1, $2, 'Vegan', 'brand', 'certificate', 'upload', 'valid', now(), now())",
            evidence, b.TenantId);
        var connector = await ScalarAsync<Guid>("SELECT id FROM shop.connectors WHERE shop_id = $1", shop.ShopId);
        var publication = await ScalarAsync<Guid>(
            """
            INSERT INTO fixes.publications (tenant_id, shop_id, connector_id, page_id, field, new_value, idempotency_key, status, attempts, published_at, created_at, updated_at)
            VALUES ($1, $2, $3, $4, 'block', 'x', $5, 'published', 1, now(), now(), now()) RETURNING id
            """, b.TenantId, shop.ShopId, connector, shop.Pages["zubna"].PageId, Guid.NewGuid().ToString("N"));
        var protocol = await ScalarAsync<Guid>(
            "INSERT INTO fixes.protocols (tenant_id, shop_id, number, period_from, period_to, locale, status, pdf_blob_key, created_at, updated_at) VALUES ($1, $2, 'EG-2026-0001', '2026-09-30', '2026-10-31', 'sk', 'ready', 'x', now(), now()) RETURNING id",
            b.TenantId, shop.ShopId);
        var notification = await ScalarAsync<Guid>(
            "INSERT INTO iam.notifications (tenant_id, user_id, shop_id, kind, params, route, created_at) VALUES ($1, $2, $3, 'protocol_ready', '{}'::jsonb, '{}'::jsonb, now()) RETURNING id",
            b.TenantId, b.UserId, shop.ShopId);
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["shopId"] = shop.ShopId.ToString("D"),
            ["pageId"] = shop.Pages["zubna"].PageId.ToString("D"),
            ["findingId"] = shop.Findings["zubna.1"].ToString("D"),
            ["proposalId"] = shop.Proposals["zubna.1"].ToString("D"),
            ["groupId"] = shop.Groups["group"].ToString("D"),
            ["questionId"] = shop.Questions["vodnar"].ToString("D"),
            ["evidenceId"] = evidence.ToString("D"),
            ["publicationId"] = publication.ToString("D"),
            ["protocolId"] = protocol.ToString("D"),
            ["runId"] = shop.Seed.RunId.ToString("D"),
            ["notificationId"] = notification.ToString("D"),
            ["userId"] = b.UserId.ToString("D"),
        };
    }
}
