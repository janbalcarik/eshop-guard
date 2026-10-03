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

        // Change 10: reading for every member, changes for admin and owner.
        ["GET /api/t/{tenantId:guid}/shops/"] = TenantRole.Viewer,
        ["POST /api/t/{tenantId:guid}/shops/"] = TenantRole.Admin,
        ["GET /api/t/{tenantId:guid}/shops/{shopId:guid}"] = TenantRole.Viewer,
        ["PATCH /api/t/{tenantId:guid}/shops/{shopId:guid}"] = TenantRole.Admin,
        ["DELETE /api/t/{tenantId:guid}/shops/{shopId:guid}"] = TenantRole.Admin,
        ["GET /api/t/{tenantId:guid}/shops/{shopId:guid}/detection"] = TenantRole.Viewer,
        ["POST /api/t/{tenantId:guid}/shops/{shopId:guid}/detection"] = TenantRole.Admin,
        ["PUT /api/t/{tenantId:guid}/shops/{shopId:guid}/platform"] = TenantRole.Admin,
        ["PUT /api/t/{tenantId:guid}/shops/{shopId:guid}/source"] = TenantRole.Admin,
        ["POST /api/t/{tenantId:guid}/shops/{shopId:guid}/sample"] = TenantRole.Admin,
        ["GET /api/t/{tenantId:guid}/shops/{shopId:guid}/sample"] = TenantRole.Viewer,
        ["GET /api/t/{tenantId:guid}/shops/{shopId:guid}/markets"] = TenantRole.Viewer,
        ["PUT /api/t/{tenantId:guid}/shops/{shopId:guid}/markets"] = TenantRole.Admin,
        ["GET /api/t/{tenantId:guid}/shops/{shopId:guid}/languages"] = TenantRole.Viewer,
        ["POST /api/t/{tenantId:guid}/shops/{shopId:guid}/languages/{language}/confirmation"] = TenantRole.Admin,
        ["PUT /api/t/{tenantId:guid}/shops/{shopId:guid}/languages/{language}/exclusion"] = TenantRole.Admin,
        ["GET /api/t/{tenantId:guid}/shops/{shopId:guid}/scope"] = TenantRole.Viewer,
        ["POST /api/t/{tenantId:guid}/shops/{shopId:guid}/quote"] = TenantRole.Admin,
        ["GET /api/t/{tenantId:guid}/shops/{shopId:guid}/onboarding"] = TenantRole.Viewer,
        ["GET /api/t/{tenantId:guid}/shops/{shopId:guid}/ownership"] = TenantRole.Viewer,
        ["POST /api/t/{tenantId:guid}/shops/{shopId:guid}/ownership/verifications"] = TenantRole.Admin,
        ["POST /api/t/{tenantId:guid}/shops/{shopId:guid}/ownership/verifications/{verificationId:guid}/check"] = TenantRole.Admin,
        ["GET /api/t/{tenantId:guid}/shops/{shopId:guid}/settings"] = TenantRole.Viewer,
        ["PATCH /api/t/{tenantId:guid}/shops/{shopId:guid}/settings"] = TenantRole.Admin,

        // Change 11: reading for every member, decisions for editor and above.
        ["GET /api/t/{tenantId:guid}/shops/{shopId:guid}/overview"] = TenantRole.Viewer,
        ["GET /api/t/{tenantId:guid}/shops/{shopId:guid}/pages"] = TenantRole.Viewer,
        ["GET /api/t/{tenantId:guid}/shops/{shopId:guid}/pages/tabs"] = TenantRole.Viewer,
        ["GET /api/t/{tenantId:guid}/shops/{shopId:guid}/findings"] = TenantRole.Viewer,
        ["GET /api/t/{tenantId:guid}/shops/{shopId:guid}/findings/tabs"] = TenantRole.Viewer,
        ["GET /api/t/{tenantId:guid}/shops/{shopId:guid}/findings/export.csv"] = TenantRole.Viewer,
        ["GET /api/t/{tenantId:guid}/shops/{shopId:guid}/findings/{findingId:guid}"] = TenantRole.Viewer,
        ["POST /api/t/{tenantId:guid}/shops/{shopId:guid}/findings/{findingId:guid}/keep"] = TenantRole.Editor,
        ["POST /api/t/{tenantId:guid}/shops/{shopId:guid}/findings/{findingId:guid}/dismiss"] = TenantRole.Editor,
        ["POST /api/t/{tenantId:guid}/shops/{shopId:guid}/findings/{findingId:guid}/reopen"] = TenantRole.Editor,
        ["GET /api/t/{tenantId:guid}/shops/{shopId:guid}/search"] = TenantRole.Viewer,
        ["GET /api/t/{tenantId:guid}/shops/{shopId:guid}/pages/{pageId:guid}/review"] = TenantRole.Viewer,
        ["GET /api/t/{tenantId:guid}/shops/{shopId:guid}/proposals/{proposalId:guid}"] = TenantRole.Viewer,
        ["PUT /api/t/{tenantId:guid}/shops/{shopId:guid}/proposals/{proposalId:guid}/alternative"] = TenantRole.Editor,
        ["PUT /api/t/{tenantId:guid}/shops/{shopId:guid}/proposals/{proposalId:guid}/text"] = TenantRole.Editor,
        ["PUT /api/t/{tenantId:guid}/shops/{shopId:guid}/proposals/{proposalId:guid}/placeholders"] = TenantRole.Editor,
        ["POST /api/t/{tenantId:guid}/shops/{shopId:guid}/proposals/{proposalId:guid}/accept"] = TenantRole.Editor,
        ["POST /api/t/{tenantId:guid}/shops/{shopId:guid}/proposals/{proposalId:guid}/reject"] = TenantRole.Editor,
        ["POST /api/t/{tenantId:guid}/shops/{shopId:guid}/proposals/{proposalId:guid}/unaccept"] = TenantRole.Editor,
        ["GET /api/t/{tenantId:guid}/shops/{shopId:guid}/questions"] = TenantRole.Viewer,
        ["GET /api/t/{tenantId:guid}/shops/{shopId:guid}/runs"] = TenantRole.Viewer,
        ["GET /api/t/{tenantId:guid}/shops/{shopId:guid}/events"] = TenantRole.Viewer,
        ["GET /api/t/{tenantId:guid}/runs/{runId:guid}"] = TenantRole.Viewer,
        ["GET /api/t/{tenantId:guid}/runs/{runId:guid}/events"] = TenantRole.Viewer,
        ["POST /api/t/{tenantId:guid}/runs/{runId:guid}/cancel"] = TenantRole.Admin,
        ["GET /api/t/{tenantId:guid}/shops/{shopId:guid}/protocols"] = TenantRole.Viewer,
        ["POST /api/t/{tenantId:guid}/shops/{shopId:guid}/protocols"] = TenantRole.Editor,
        ["GET /api/t/{tenantId:guid}/shops/{shopId:guid}/protocols/{protocolId:guid}"] = TenantRole.Viewer,
        ["GET /api/t/{tenantId:guid}/shops/{shopId:guid}/protocols/{protocolId:guid}/pdf"] = TenantRole.Viewer,
        ["GET /api/t/{tenantId:guid}/shops/{shopId:guid}/pages/{pageId:guid}/fixed-text"] = TenantRole.Viewer,
        ["POST /api/t/{tenantId:guid}/shops/{shopId:guid}/publications"] = TenantRole.Editor,
        ["GET /api/t/{tenantId:guid}/shops/{shopId:guid}/publications"] = TenantRole.Viewer,
        ["GET /api/t/{tenantId:guid}/shops/{shopId:guid}/publications/{publicationId:guid}"] = TenantRole.Viewer,
        ["POST /api/t/{tenantId:guid}/shops/{shopId:guid}/publications/{publicationId:guid}/rollback"] = TenantRole.Editor,
        ["GET /api/t/{tenantId:guid}/shops/{shopId:guid}/fix-groups"] = TenantRole.Viewer,
        ["GET /api/t/{tenantId:guid}/shops/{shopId:guid}/fix-groups/{groupId:guid}"] = TenantRole.Viewer,
        ["PUT /api/t/{tenantId:guid}/shops/{shopId:guid}/fix-groups/{groupId:guid}/values"] = TenantRole.Editor,
        ["PUT /api/t/{tenantId:guid}/shops/{shopId:guid}/fix-groups/{groupId:guid}/mode"] = TenantRole.Editor,
        ["PUT /api/t/{tenantId:guid}/shops/{shopId:guid}/fix-groups/{groupId:guid}/excluded-pages"] = TenantRole.Editor,
        ["POST /api/t/{tenantId:guid}/shops/{shopId:guid}/fix-groups/{groupId:guid}/approve"] = TenantRole.Editor,
        ["POST /api/t/{tenantId:guid}/shops/{shopId:guid}/fix-groups/{groupId:guid}/approve-page"] = TenantRole.Editor,
        ["POST /api/t/{tenantId:guid}/shops/{shopId:guid}/fix-groups/{groupId:guid}/unapprove"] = TenantRole.Editor,
        ["POST /api/t/{tenantId:guid}/shops/{shopId:guid}/questions/{questionId:guid}/answer"] = TenantRole.Editor,
        ["GET /api/t/{tenantId:guid}/notifications"] = TenantRole.Viewer,
        ["POST /api/t/{tenantId:guid}/notifications/{notificationId:guid}/read"] = TenantRole.Viewer,
        ["POST /api/t/{tenantId:guid}/notifications/read-all"] = TenantRole.Viewer,
        ["GET /api/t/{tenantId:guid}/notification-settings"] = TenantRole.Viewer,
        ["PUT /api/t/{tenantId:guid}/notification-settings"] = TenantRole.Viewer,
        ["GET /api/t/{tenantId:guid}/evidence"] = TenantRole.Viewer,
        ["POST /api/t/{tenantId:guid}/evidence"] = TenantRole.Editor,
        ["GET /api/t/{tenantId:guid}/evidence/{evidenceId:guid}"] = TenantRole.Viewer,
        ["PATCH /api/t/{tenantId:guid}/evidence/{evidenceId:guid}"] = TenantRole.Editor,
        ["DELETE /api/t/{tenantId:guid}/evidence/{evidenceId:guid}"] = TenantRole.Editor,
        ["GET /api/t/{tenantId:guid}/evidence/{evidenceId:guid}/file"] = TenantRole.Viewer,
        ["POST /api/t/{tenantId:guid}/evidence/{evidenceId:guid}/links"] = TenantRole.Editor,
        ["DELETE /api/t/{tenantId:guid}/evidence/{evidenceId:guid}/links/{linkId:guid}"] = TenantRole.Editor,

        // Change 12: ordering and paying only for owner and admin.
        ["POST /api/t/{tenantId:guid}/shops/{shopId:guid}/orders"] = TenantRole.Admin,
        ["GET /api/t/{tenantId:guid}/orders/{orderId:guid}"] = TenantRole.Admin,
        ["POST /api/t/{tenantId:guid}/orders/{orderId:guid}/checkout"] = TenantRole.Admin,
        ["POST /api/t/{tenantId:guid}/orders/{orderId:guid}/pay-with-saved-card"] = TenantRole.Admin,
        ["GET /api/t/{tenantId:guid}/billing/overview"] = TenantRole.Admin,
        ["GET /api/t/{tenantId:guid}/billing/details"] = TenantRole.Admin,
        ["PUT /api/t/{tenantId:guid}/billing/details"] = TenantRole.Admin,
        ["POST /api/t/{tenantId:guid}/billing/card/portal-session"] = TenantRole.Admin,
        ["POST /api/t/{tenantId:guid}/billing/card/setup-intent"] = TenantRole.Admin,
        ["POST /api/t/{tenantId:guid}/shops/{shopId:guid}/subscription/cancel"] = TenantRole.Admin,
        ["POST /api/t/{tenantId:guid}/shops/{shopId:guid}/subscription/resume"] = TenantRole.Admin,
        ["POST /api/t/{tenantId:guid}/shops/{shopId:guid}/subscription"] = TenantRole.Admin,
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
                var path = RoutePaths.Fill(endpoint.Pattern, owner.TenantId);
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
