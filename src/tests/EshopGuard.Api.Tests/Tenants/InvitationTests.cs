using System.Net;
using EshopGuard.Application.Email;
using Microsoft.Extensions.Time.Testing;

namespace EshopGuard.Api.Tests.Tenants;

/// <summary>Invitations (change 9, task 8.5; specification „Pozvánky do účtu“).</summary>
public sealed class InvitationTests : ApiTestBase
{
    [Fact]
    public async Task InvitingAnEditorWithoutAccount_SendsACzechEmailWithTheTokenAfterTheHash()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        var email = NewEmail("peter");

        using var response = await owner.Browser.PostAsync($"/api/t/{owner.TenantId}/invitations", new { email, role = "editor", locale = "cs" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await ApiClient.JsonAsync(response);
        Assert.Equal("pending", dto.GetProperty("status").GetString());
        Assert.False(dto.TryGetProperty("token", out _));
        var row = (await AdminRowsAsync("SELECT length(token_hash), expires_at - created_at FROM iam.invitations WHERE email = $1", email)).Single();
        Assert.Equal(32, row[0]);
        Assert.InRange((TimeSpan)row[1]!, TimeSpan.FromDays(7) - TimeSpan.FromSeconds(5), TimeSpan.FromDays(7));
        var message = factory.Emails.To(email).Single();
        Assert.Equal(EmailTemplateKind.Invitation.Code, message.Kind);
        Assert.Equal("cs", message.Locale);
        Assert.Contains(ApiFactory.FrontendBaseUrl + "/pozvanka#t=", message.Text, StringComparison.Ordinal);
        Assert.Contains("editor", message.Text, StringComparison.Ordinal);
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.audit_log WHERE action = 'invitation.created' AND tenant_id = $1", owner.TenantId));
    }

    [Fact]
    public async Task Accepting_CreatesTheAccountAndTheMembership_WithoutATenantOfItsOwn()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        var email = NewEmail("peter");
        await (await owner.Browser.PostAsync($"/api/t/{owner.TenantId}/invitations", new { email, role = "editor" })).Content.ReadAsStringAsync(Ct);
        var token = factory.Emails.LastToken(email, EmailTemplateKind.Invitation.Code);
        using var browser = factory.CreateApiClient();

        using (var inspected = await browser.PostAsync("/api/invitations/inspect", new { token }))
        {
            var info = await ApiClient.JsonAsync(inspected);
            Assert.Equal(email, info.GetProperty("email").GetString());
            Assert.Equal("editor", info.GetProperty("role").GetString());
            Assert.False(info.GetProperty("accountExists").GetBoolean());
            Assert.Equal(owner.Email, info.GetProperty("tenantName").GetString());
        }

        using var accepted = await browser.PostAsync("/api/invitations/accept", new { token });

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        var body = await ApiClient.JsonAsync(accepted);
        Assert.Equal("editor", body.GetProperty("membership").GetProperty("role").GetString());
        Assert.True(body.GetProperty("isNewAccount").GetBoolean());
        var me = await People.MeAsync(browser);
        Assert.Equal(owner.TenantId, me.GetProperty("memberships").EnumerateArray().Single().GetProperty("tenantId").GetGuid());
        Assert.True(await AdminScalarAsync<bool>("SELECT email_confirmed FROM iam.users WHERE email = $1", email));
        Assert.Equal(1L, await AdminScalarAsync<long>(
            "SELECT count(*) FROM ops.audit_log WHERE action = 'auth.login_succeeded' AND data ->> 'method' = 'invitation' AND actor_user_id = (SELECT id FROM iam.users WHERE email = $1)", email));
        var outbox = (await AdminRowsAsync("SELECT payload::text FROM ops.outbox WHERE tenant_id = $1 AND payload ->> 'template' = 'invitation_accepted'", owner.TenantId)).Single();
        Assert.Contains(owner.UserId.ToString("D"), (string)outbox[0]!, StringComparison.Ordinal);
        Assert.DoesNotContain(token, (string)outbox[0]!, StringComparison.Ordinal);
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.jobs WHERE tenant_id = $1 AND kind = 'email.send'", owner.TenantId));

        using var again = await factory.CreateApiClient().PostAsync("/api/invitations/accept", new { token });
        Assert.Equal("invitation.used", (await ApiClient.ProblemAsync(again)).Code);
    }

    [Fact]
    public async Task SignedInWithAnotherEmail_Is409Mismatch_AndNoMembership()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        using var jana = await People.OwnerAsync(factory);
        var email = NewEmail("peter");
        await (await owner.Browser.PostAsync($"/api/t/{owner.TenantId}/invitations", new { email, role = "viewer" })).Content.ReadAsStringAsync(Ct);
        var token = factory.Emails.LastToken(email, EmailTemplateKind.Invitation.Code);

        using var response = await jana.Browser.PostAsync("/api/invitations/accept", new { token });

        var (status, code, _) = await ApiClient.ProblemAsync(response);
        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Equal("invitation.email_mismatch", code);
        Assert.Equal(0L, await AdminScalarAsync<long>("SELECT count(*) FROM iam.memberships WHERE tenant_id = $1 AND user_id = $2", owner.TenantId, jana.UserId));
        Assert.Null(await AdminScalarAsync<DateTime?>("SELECT accepted_at FROM iam.invitations WHERE email = $1", email));
    }

    [Fact]
    public async Task RevokedAndExpiredInvitations_Are410Expired()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = Factory(time);
        using var owner = await People.OwnerAsync(factory);
        var revoked = NewEmail("zruseny");
        using (var created = await owner.Browser.PostAsync($"/api/t/{owner.TenantId}/invitations", new { email = revoked, role = "viewer" }))
        {
            var id = (await ApiClient.JsonAsync(created)).GetProperty("id").GetGuid();
            using var revoke = await owner.Browser.DeleteAsync($"/api/t/{owner.TenantId}/invitations/{id}");
            Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);
        }

        using (var response = await factory.CreateApiClient().PostAsync("/api/invitations/accept", new { token = factory.Emails.LastToken(revoked, EmailTemplateKind.Invitation.Code) }))
        {
            var (status, code, _) = await ApiClient.ProblemAsync(response);
            Assert.Equal(HttpStatusCode.Gone, status);
            Assert.Equal("invitation.expired", code);
        }

        var late = NewEmail("neskoro");
        await (await owner.Browser.PostAsync($"/api/t/{owner.TenantId}/invitations", new { email = late, role = "viewer" })).Content.ReadAsStringAsync(Ct);
        time.Advance(TimeSpan.FromDays(7) + TimeSpan.FromSeconds(1));
        using (var response = await factory.CreateApiClient().PostAsync("/api/invitations/accept", new { token = factory.Emails.LastToken(late, EmailTemplateKind.Invitation.Code) }))
        {
            Assert.Equal("invitation.expired", (await ApiClient.ProblemAsync(response)).Code);
        }

        Assert.Equal(0L, await AdminScalarAsync<long>(
            "SELECT count(*) FROM iam.memberships m JOIN iam.users u ON u.id = m.user_id WHERE m.tenant_id = $1 AND u.email IN ($2, $3)", owner.TenantId, revoked, late));
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.audit_log WHERE action = 'invitation.revoked' AND tenant_id = $1", owner.TenantId));
    }

    [Fact]
    public async Task Admin_CannotInviteAnAdmin_AMemberCannotBeInvitedAgain()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        using var admin = await People.MemberAsync(factory, owner, "admin");

        using var invite = await admin.Browser.PostAsync($"/api/t/{owner.TenantId}/invitations", new { email = NewEmail(), role = "admin" });
        var (status, code, _) = await ApiClient.ProblemAsync(invite);
        Assert.Equal(HttpStatusCode.Forbidden, status);
        Assert.Equal("membership.role_not_allowed", code);

        using var member = await admin.Browser.PostAsync($"/api/t/{owner.TenantId}/invitations", new { email = owner.Email, role = "viewer" });
        Assert.Equal("invitation.already_member", (await ApiClient.ProblemAsync(member)).Code);
    }

    [Fact]
    public async Task AnExistingMember_KeepsHisRole_WhenAnOlderInvitationIsAccepted()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        using var editor = await People.MemberAsync(factory, owner, "editor");
        var (token, hash) = Application.Security.OneTimeTokens.Create();
        await AdminAsync(
            "INSERT INTO iam.invitations (id, tenant_id, email, role, token_hash, expires_at, invited_by, created_at, updated_at) VALUES ($1, $2, $3, 'viewer', $4, now() + interval '1 day', $5, now(), now())",
            Guid.CreateVersion7(), owner.TenantId, editor.Email, hash, owner.UserId);

        using var accepted = await editor.Browser.PostAsync("/api/invitations/accept", new { token });

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal("editor", (await ApiClient.JsonAsync(accepted)).GetProperty("membership").GetProperty("role").GetString());
        Assert.Equal("editor", await AdminScalarAsync<string>("SELECT role FROM iam.memberships WHERE tenant_id = $1 AND user_id = $2", owner.TenantId, editor.UserId));
    }

    [Fact]
    public async Task Resend_GivesANewToken_TheOldOneStopsWorking()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        var email = NewEmail("peter");
        using var created = await owner.Browser.PostAsync($"/api/t/{owner.TenantId}/invitations", new { email, role = "viewer" });
        var id = (await ApiClient.JsonAsync(created)).GetProperty("id").GetGuid();
        var first = factory.Emails.LastToken(email, EmailTemplateKind.Invitation.Code);

        using var resent = await owner.Browser.PostAsync($"/api/t/{owner.TenantId}/invitations/{id}/resend");

        Assert.Equal(HttpStatusCode.NoContent, resent.StatusCode);
        var second = factory.Emails.LastToken(email, EmailTemplateKind.Invitation.Code);
        Assert.NotEqual(first, second);
        using var old = await factory.CreateApiClient().PostAsync("/api/invitations/inspect", new { token = first });
        Assert.Equal("invitation.invalid", (await ApiClient.ProblemAsync(old)).Code);
        using var fresh = await factory.CreateApiClient().PostAsync("/api/invitations/inspect", new { token = second });
        Assert.Equal(HttpStatusCode.OK, fresh.StatusCode);
        using var list = await owner.Browser.GetAsync($"/api/t/{owner.TenantId}/invitations");
        Assert.DoesNotContain(second, await list.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }
}
