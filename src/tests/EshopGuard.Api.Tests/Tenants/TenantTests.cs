using System.Net;
using EshopGuard.Application.Email;

namespace EshopGuard.Api.Tests.Tenants;

/// <summary>Tenants (change 9, task 7.8; specification „Tenanti a členství s rolemi“).</summary>
public sealed class TenantTests : ApiTestBase
{
    [Fact]
    public async Task NewAccountWithoutInvitation_GetsItsTenant_AsOwner()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory, market: "sk");

        var tenant = (await AdminRowsAsync("SELECT market_code, locale, currency, status FROM iam.tenants WHERE id = $1", owner.TenantId)).Single();
        Assert.Equal(["sk", "sk", null, "active"], tenant);
        using var get = await owner.Browser.GetAsync($"/api/t/{owner.TenantId}");
        var body = await ApiClient.JsonAsync(get);
        Assert.Equal("owner", body.GetProperty("myRole").GetString());
        Assert.Equal(owner.Email, body.GetProperty("name").GetString());
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.audit_log WHERE action = 'tenant.created' AND tenant_id = $1", owner.TenantId));
    }

    [Fact]
    public async Task NewAccountWithAPendingInvitation_GetsNoTenant_AndSeesTheInvitation()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        var email = NewEmail("peter");
        using (var invited = await owner.Browser.PostAsync($"/api/t/{owner.TenantId}/invitations", new { email, role = "editor" }))
        {
            Assert.Equal(HttpStatusCode.Created, invited.StatusCode);
        }

        using var browser = factory.CreateApiClient();
        var session = await browser.SignInByLinkAsync(factory, email);

        Assert.Equal(System.Text.Json.JsonValueKind.Null, session.GetProperty("createdTenantId").ValueKind);
        var me = session.GetProperty("me");
        Assert.Empty(me.GetProperty("memberships").EnumerateArray());
        var pending = me.GetProperty("pendingInvitations").EnumerateArray().Single();
        Assert.Equal("editor", pending.GetProperty("role").GetString());

        using var accepted = await browser.PostAsync($"/api/me/invitations/{pending.GetProperty("invitationId").GetGuid()}/accept");
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        var after = await People.MeAsync(browser);
        Assert.Equal(owner.TenantId, after.GetProperty("memberships").EnumerateArray().Single().GetProperty("tenantId").GetGuid());
        Assert.Empty(after.GetProperty("pendingInvitations").EnumerateArray());
    }

    [Fact]
    public async Task OwnedTenants_StopAtTheCap()
    {
        await using var factory = Factory(settings: new Dictionary<string, string?> { ["Tenants:MaxOwnedPerUser"] = "2" });
        using var owner = await People.OwnerAsync(factory);

        using var second = await owner.Browser.PostAsync("/api/tenants", new { name = "Agentúra – klient 2", market = "cz" });
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var created = await ApiClient.JsonAsync(second);
        Assert.Equal("CZ", created.GetProperty("countryCode").GetString());
        Assert.Equal("owner", created.GetProperty("myRole").GetString());

        using var third = await owner.Browser.PostAsync("/api/tenants", new { name = "Klient 3", market = "sk" });
        var (status, code, body) = await ApiClient.ProblemAsync(third);
        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Equal("tenant.limit_reached", code);
        Assert.Equal(2, body.GetProperty("params").GetProperty("limit").GetInt32());

        using var unknown = await owner.Browser.PostAsync("/api/tenants", new { name = "X", market = "xx" });
        Assert.Equal("market.unknown", (await ApiClient.ProblemAsync(unknown)).Code);
        Assert.Equal(2, (await People.MeAsync(owner.Browser)).GetProperty("memberships").GetArrayLength());
    }

    [Fact]
    public async Task RenameWithAnOldVersion_Is409Conflict()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        using var get = await owner.Browser.GetAsync($"/api/t/{owner.TenantId}");
        var version = (await ApiClient.JsonAsync(get)).GetProperty("version").GetUInt32();

        using var renamed = await owner.Browser.PatchAsync($"/api/t/{owner.TenantId}", new { name = "Bylinkovo", version });
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        var body = await ApiClient.JsonAsync(renamed);
        Assert.Equal("Bylinkovo", body.GetProperty("name").GetString());
        Assert.NotEqual(version, body.GetProperty("version").GetUInt32());

        using var stale = await owner.Browser.PatchAsync($"/api/t/{owner.TenantId}", new { name = "Iný názov", version });
        var (status, code, _) = await ApiClient.ProblemAsync(stale);
        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Equal("concurrency.conflict", code);
        Assert.Equal("Bylinkovo", await AdminScalarAsync<string>("SELECT name FROM iam.tenants WHERE id = $1", owner.TenantId));
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.audit_log WHERE action = 'tenant.renamed' AND tenant_id = $1", owner.TenantId));
    }

    [Fact]
    public async Task Viewer_CannotRename_403WithTheRequiredRole()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        using var viewer = await People.MemberAsync(factory, owner, "viewer");

        using var response = await viewer.Browser.PatchAsync($"/api/t/{owner.TenantId}", new { name = "Iný", version = 1 });

        var (status, code, body) = await ApiClient.ProblemAsync(response);
        Assert.Equal(HttpStatusCode.Forbidden, status);
        Assert.Equal("auth.forbidden_role", code);
        Assert.Equal("admin", body.GetProperty("params").GetProperty("requiredRole").GetString());
        using var read = await viewer.Browser.GetAsync($"/api/t/{owner.TenantId}");
        Assert.Equal("viewer", (await ApiClient.JsonAsync(read)).GetProperty("myRole").GetString());
        Assert.NotNull(factory.Emails.Sent.FirstOrDefault(m => m.Kind == EmailTemplateKind.Invitation.Code));
    }

    [Fact]
    public async Task SuspendedTenant_Is403Suspended_ExceptReadingTheAccount()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        await AdminAsync("UPDATE iam.tenants SET status = 'suspended' WHERE id = $1", owner.TenantId);

        using var members = await owner.Browser.GetAsync($"/api/t/{owner.TenantId}/members");
        using var account = await owner.Browser.GetAsync($"/api/t/{owner.TenantId}");

        var (status, code, _) = await ApiClient.ProblemAsync(members);
        Assert.Equal(HttpStatusCode.Forbidden, status);
        Assert.Equal("tenant.suspended", code);
        Assert.Equal(HttpStatusCode.OK, account.StatusCode);
        Assert.Equal("suspended", (await ApiClient.JsonAsync(account)).GetProperty("status").GetString());
    }
}
