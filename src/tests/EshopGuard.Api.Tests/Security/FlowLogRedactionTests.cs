using EshopGuard.Application.Email;
using Microsoft.Extensions.Time.Testing;

namespace EshopGuard.Api.Tests.Security;

/// <summary>Logs of the flows of the link, the reset and the invitation hold no token, password or e-mail (change 9, task 11.3).</summary>
public sealed class FlowLogRedactionTests : ApiTestBase
{
    [Fact]
    public async Task LogsOfLinkResetAndInvitation_HoldNoSecrets()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = Factory(time);
        const string password = "tajne-heslo-logu";
        using var owner = await People.OwnerAsync(factory);
        await (await owner.Browser.PutAsync("/api/me/password", new { newPassword = password })).Content.ReadAsStringAsync(Ct);
        await (await factory.CreateApiClient().PostAsync("/api/auth/password/login", new { email = owner.Email, password })).Content.ReadAsStringAsync(Ct);
        time.Advance(TimeSpan.FromSeconds(61));
        using (var forgot = factory.CreateApiClient())
        {
            await (await forgot.PostAsync("/api/auth/password/forgot", new { email = owner.Email })).Content.ReadAsStringAsync(Ct);
            var token = factory.Emails.LastToken(owner.Email, EmailTemplateKind.PasswordReset.Code);
            await (await forgot.PostAsync("/api/auth/password/reset", new { token, newPassword = "druhe-tajne-heslo" })).Content.ReadAsStringAsync(Ct);
        }

        using var member = await People.MemberAsync(factory, owner, "editor");
        factory.Emails.Fail = true;
        await (await factory.CreateApiClient().PostAsync("/api/auth/login-link", new { email = NewEmail("zlyhanie") })).Content.ReadAsStringAsync(Ct);

        var secrets = new[] { password, "druhe-tajne-heslo", owner.Email, member.Email, "@bylinkovo-test.sk" }
            .Concat(factory.Emails.Sent.Select(CapturingEmailTransport.TokenOf)).ToList();
        Assert.NotEmpty(factory.Logs.Logs);
        foreach (var log in factory.Logs.Logs)
        {
            Assert.All(secrets, s => Assert.DoesNotContain(s, log.AllText, StringComparison.OrdinalIgnoreCase));
        }
    }
}
