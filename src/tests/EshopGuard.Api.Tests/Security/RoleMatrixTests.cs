using System.Net;
using EshopGuard.Api.Tenancy;
using EshopGuard.Application.Tenants;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace EshopGuard.Api.Tests.Security;

/// <summary>
/// Every endpoint of <c>/api/t/{tenantId}</c> against the matrix of roles (change 9, task 11.1): its lowest role is the
/// expected one, and members below it get <c>403 auth.forbidden_role</c>, members at or above it never do. A new endpoint
/// without a row here fails the test.
/// </summary>
public sealed class RoleMatrixTests : ApiTestBase
{
    /// <summary>The lowest role of each endpoint by the matrix of the design (K rozhodnutí 2).</summary>
    private static readonly Dictionary<string, TenantRole> Expected = new(StringComparer.Ordinal)
    {
        ["GET /api/t/{tenantId:guid}/"] = TenantRole.Viewer,
        ["PATCH /api/t/{tenantId:guid}/"] = TenantRole.Admin,
        ["GET /api/t/{tenantId:guid}/members"] = TenantRole.Admin,
        ["PATCH /api/t/{tenantId:guid}/members/{userId:guid}"] = TenantRole.Admin,
        ["DELETE /api/t/{tenantId:guid}/members/{userId:guid}"] = TenantRole.Viewer,
        ["POST /api/t/{tenantId:guid}/ownership-transfer"] = TenantRole.Owner,
        ["GET /api/t/{tenantId:guid}/invitations"] = TenantRole.Admin,
        ["POST /api/t/{tenantId:guid}/invitations"] = TenantRole.Admin,
        ["POST /api/t/{tenantId:guid}/invitations/{invitationId:guid}/resend"] = TenantRole.Admin,
        ["DELETE /api/t/{tenantId:guid}/invitations/{invitationId:guid}"] = TenantRole.Admin,
    };

    [Fact]
    public async Task EveryTenantEndpoint_AllowsItsRolesAndRefusesTheLowerOnes()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        var members = new Dictionary<TenantRole, Person>
        {
            [TenantRole.Owner] = owner,
            [TenantRole.Admin] = await People.MemberAsync(factory, owner, "admin"),
            [TenantRole.Editor] = await People.MemberAsync(factory, owner, "editor"),
            [TenantRole.Viewer] = await People.MemberAsync(factory, owner, "viewer"),
        };
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(e => ("/" + e.RoutePattern.RawText!.TrimStart('/')).StartsWith("/api/t/", StringComparison.Ordinal))
            .SelectMany(e => e.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Select(m =>
                (Method: m, Pattern: "/" + e.RoutePattern.RawText!.TrimStart('/'), Role: e.Metadata.GetMetadata<TenantRoleMetadata>()?.Role)))
            .ToList();

        var keys = endpoints.Select(e => Normalize(e.Method, e.Pattern)).ToList();
        Assert.Equal(Expected.Keys.Order(StringComparer.Ordinal), keys.Order(StringComparer.Ordinal));

        var failures = new List<string>();
        foreach (var endpoint in endpoints)
        {
            var key = Normalize(endpoint.Method, endpoint.Pattern);
            var minimum = Expected[key];
            if (endpoint.Role != minimum)
            {
                failures.Add($"{key}: metadata {endpoint.Role}, matrix {minimum}");
            }

            foreach (var (role, person) in members)
            {
                var path = endpoint.Pattern.Replace("{tenantId:guid}", owner.TenantId.ToString("D"), StringComparison.Ordinal)
                    .Replace("{userId:guid}", Guid.NewGuid().ToString("D"), StringComparison.Ordinal)
                    .Replace("{invitationId:guid}", Guid.NewGuid().ToString("D"), StringComparison.Ordinal);
                using var response = await person.Browser.SendAsync(new HttpMethod(endpoint.Method), path, endpoint.Method == "GET" ? null : new { }, csrf: endpoint.Method != "GET");
                var forbidden = response.StatusCode == HttpStatusCode.Forbidden
                    && (await ApiClient.ProblemAsync(response)).Code == "auth.forbidden_role";
                if (forbidden != role < minimum)
                {
                    failures.Add($"{key} as {role}: {(int)response.StatusCode}");
                }
            }
        }

        Assert.Empty(failures);
        foreach (var person in members.Values.Distinct())
        {
            person.Dispose();
        }
    }

    private static string Normalize(string method, string pattern) =>
        $"{method} {(pattern.EndsWith("{tenantId:guid}", StringComparison.Ordinal) ? pattern + "/" : pattern)}";
}
