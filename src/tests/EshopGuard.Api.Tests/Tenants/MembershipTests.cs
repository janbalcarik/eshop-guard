using System.Net;

namespace EshopGuard.Api.Tests.Tenants;

/// <summary>Members and roles (change 9, task 7.6; specification „Tenanti a členství s rolemi“).</summary>
public sealed class MembershipTests : ApiTestBase
{
    [Fact]
    public async Task TheLastOwner_CannotLeave()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);

        using var response = await owner.Browser.DeleteAsync($"/api/t/{owner.TenantId}/members/{owner.UserId}");

        var (status, code, _) = await ApiClient.ProblemAsync(response);
        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Equal("membership.last_owner", code);
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM iam.memberships WHERE tenant_id = $1 AND role = 'owner'", owner.TenantId));
    }

    [Fact]
    public async Task Admin_CannotPromoteToAdmin_ButSwitchesEditorAndViewer()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        using var admin = await People.MemberAsync(factory, owner, "admin");
        using var editor = await People.MemberAsync(factory, owner, "editor");

        using var promote = await admin.Browser.PatchAsync($"/api/t/{owner.TenantId}/members/{editor.UserId}", new { role = "admin" });
        var (status, code, _) = await ApiClient.ProblemAsync(promote);
        Assert.Equal(HttpStatusCode.Forbidden, status);
        Assert.Equal("membership.role_not_allowed", code);

        using var demote = await admin.Browser.PatchAsync($"/api/t/{owner.TenantId}/members/{editor.UserId}", new { role = "viewer" });
        Assert.Equal(HttpStatusCode.OK, demote.StatusCode);
        Assert.Equal("viewer", (await ApiClient.JsonAsync(demote)).GetProperty("role").GetString());

        using var members = await admin.Browser.GetAsync($"/api/t/{owner.TenantId}/members");
        Assert.Equal(3, (await ApiClient.JsonAsync(members)).GetArrayLength());
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.audit_log WHERE action = 'membership.role_changed' AND tenant_id = $1", owner.TenantId));
    }

    [Fact]
    public async Task OwnershipTransfer_MakesTheAdminOwner_AndTheOwnerAdmin()
    {
        await using var factory = Factory();
        using var jana = await People.OwnerAsync(factory);
        using var peter = await People.MemberAsync(factory, jana, "admin");

        using var response = await jana.Browser.PostAsync($"/api/t/{jana.TenantId}/ownership-transfer", new { userId = peter.UserId });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var roles = (await AdminRowsAsync("SELECT user_id, role FROM iam.memberships WHERE tenant_id = $1", jana.TenantId)).ToDictionary(r => (Guid)r[0]!, r => (string)r[1]!);
        Assert.Equal("owner", roles[peter.UserId]);
        Assert.Equal("admin", roles[jana.UserId]);
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.audit_log WHERE action = 'membership.ownership_transferred' AND tenant_id = $1", jana.TenantId));

        // Jana is an admin now: transferring again needs the owner.
        using var again = await jana.Browser.PostAsync($"/api/t/{jana.TenantId}/ownership-transfer", new { userId = jana.UserId });
        Assert.Equal("auth.forbidden_role", (await ApiClient.ProblemAsync(again)).Code);
    }

    [Fact]
    public async Task Viewer_LeavesByHimself_ButCannotRemoveOthers()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        using var viewer = await People.MemberAsync(factory, owner, "viewer");
        using var editor = await People.MemberAsync(factory, owner, "editor");

        using var removeOther = await viewer.Browser.DeleteAsync($"/api/t/{owner.TenantId}/members/{editor.UserId}");
        Assert.Equal("membership.role_not_allowed", (await ApiClient.ProblemAsync(removeOther)).Code);

        using var leave = await viewer.Browser.DeleteAsync($"/api/t/{owner.TenantId}/members/{viewer.UserId}");
        Assert.Equal(HttpStatusCode.NoContent, leave.StatusCode);
        using var gone = await viewer.Browser.GetAsync($"/api/t/{owner.TenantId}");
        Assert.Equal("tenant.not_found", (await ApiClient.ProblemAsync(gone)).Code);
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.audit_log WHERE action = 'membership.left' AND tenant_id = $1", owner.TenantId));

        using var removed = await owner.Browser.DeleteAsync($"/api/t/{owner.TenantId}/members/{editor.UserId}");
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.audit_log WHERE action = 'membership.removed' AND tenant_id = $1", owner.TenantId));
    }
}
