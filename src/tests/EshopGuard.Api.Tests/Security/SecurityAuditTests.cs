using System.Net;
using EshopGuard.Application.Email;
using Microsoft.Extensions.Time.Testing;

namespace EshopGuard.Api.Tests.Security;

/// <summary>
/// The security audit of the flows of groups 4–8 (change 9, task 10.3): each writes its action with the tenant (or
/// <c>NULL</c> for events of a person), and no <c>data</c> holds a token, a password or a readable e-mail address.
/// </summary>
public sealed class SecurityAuditTests : ApiTestBase
{
    [Fact]
    public async Task FlowsOfSignInTenantsAndInvitations_WriteTheirActions_WithoutSecrets()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = Factory(time);
        const string password = "audit-heslo-123";
        using var owner = await People.OwnerAsync(factory);
        await (await owner.Browser.PutAsync("/api/me/password", new { newPassword = password })).Content.ReadAsStringAsync(Ct);
        using (var wrong = factory.CreateApiClient())
        {
            await (await wrong.PostAsync("/api/auth/password/login", new { email = owner.Email, password = "zle-heslo-1234" })).Content.ReadAsStringAsync(Ct);
        }

        using var get = await owner.Browser.GetAsync($"/api/t/{owner.TenantId}");
        var version = (await ApiClient.JsonAsync(get)).GetProperty("version").GetUInt32();
        await (await owner.Browser.PatchAsync($"/api/t/{owner.TenantId}", new { name = "Bylinkovo", version })).Content.ReadAsStringAsync(Ct);
        using var editor = await People.MemberAsync(factory, owner, "editor");
        await (await owner.Browser.PatchAsync($"/api/t/{owner.TenantId}/members/{editor.UserId}", new { role = "viewer" })).Content.ReadAsStringAsync(Ct);
        var revokedEmail = NewEmail("zruseny");
        using (var created = await owner.Browser.PostAsync($"/api/t/{owner.TenantId}/invitations", new { email = revokedEmail, role = "viewer" }))
        {
            var id = (await ApiClient.JsonAsync(created)).GetProperty("id").GetGuid();
            await (await owner.Browser.PostAsync($"/api/t/{owner.TenantId}/invitations/{id}/resend")).Content.ReadAsStringAsync(Ct);
            await (await owner.Browser.DeleteAsync($"/api/t/{owner.TenantId}/invitations/{id}")).Content.ReadAsStringAsync(Ct);
        }

        await (await owner.Browser.DeleteAsync($"/api/t/{owner.TenantId}/members/{editor.UserId}")).Content.ReadAsStringAsync(Ct);
        time.Advance(TimeSpan.FromSeconds(61));
        using (var link = factory.CreateApiClient())
        {
            await (await link.PostAsync("/api/auth/login-link", new { email = owner.Email })).Content.ReadAsStringAsync(Ct);
        }

        using (var forgot = factory.CreateApiClient())
        {
            await (await forgot.PostAsync("/api/auth/password/forgot", new { email = owner.Email })).Content.ReadAsStringAsync(Ct);
            var token = factory.Emails.LastToken(owner.Email, EmailTemplateKind.PasswordReset.Code);
            using var reset = await forgot.PostAsync("/api/auth/password/reset", new { token, newPassword = "nove-audit-heslo" });
            Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        }

        await (await owner.Browser.PostAsync("/api/auth/logout-everywhere")).Content.ReadAsStringAsync(Ct);

        var rows = await AdminRowsAsync(
            "SELECT action, tenant_id, coalesce(data::text, ''), ip FROM ops.audit_log WHERE actor_user_id IN ($1, $2) OR tenant_id = $3",
            owner.UserId, editor.UserId, owner.TenantId);
        var byAction = rows.GroupBy(r => (string)r[0]!).ToDictionary(g => g.Key, g => g.Select(r => (Guid?)r[1]).ToList());
        string[] personal =
        [
            "auth.login_link_requested", "auth.login_succeeded", "auth.login_failed", "auth.password_set", "auth.password_reset_requested",
            "auth.password_reset", "auth.logout_everywhere", "user.created", "user.terms_accepted",
        ];
        string[] ofTenant =
        [
            "tenant.created", "tenant.renamed", "membership.role_changed", "membership.removed", "invitation.created", "invitation.resent",
            "invitation.revoked", "invitation.accepted",
        ];
        var misplaced = personal.Where(a => !(byAction.TryGetValue(a, out var t) && t.All(x => x is null)))
            .Concat(ofTenant.Where(a => !(byAction.TryGetValue(a, out var t) && t.All(x => x == owner.TenantId))))
            .ToList();
        Assert.True(misplaced.Count == 0, "Missing or with a wrong tenant: " + string.Join(", ", misplaced));

        var secrets = new[] { password, "zle-heslo-1234", "nove-audit-heslo", owner.Email, editor.Email, revokedEmail }
            .Concat(factory.Emails.Sent.Select(CapturingEmailTransport.TokenOf));
        foreach (var row in rows)
        {
            var data = (string)row[2]!;
            Assert.DoesNotContain("@", data, StringComparison.Ordinal);
            Assert.All(secrets, s => Assert.DoesNotContain(s, data, StringComparison.Ordinal));
            Assert.Null(row[3]);
        }

        Assert.Contains(rows, r => ((string)r[2]!).Contains("ipHash", StringComparison.Ordinal));
    }
}
