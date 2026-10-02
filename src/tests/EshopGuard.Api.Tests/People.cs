using System.Net;
using System.Text.Json;
using EshopGuard.Application.Email;

namespace EshopGuard.Api.Tests;

/// <summary>A signed-in person of a test: his browser, e-mail, id and (for an owner) his tenant.</summary>
internal sealed record Person(ApiClient Browser, string Email, Guid UserId, Guid TenantId) : IDisposable
{
    public void Dispose() => Browser.Dispose();
}

/// <summary>People of the scenarios with tenants: an owner of a new account, members invited and accepted through the API.</summary>
internal static class People
{
    public static string NewEmail(string name = "jana") => $"{name}.{Guid.NewGuid():N}@bylinkovo-test.sk";

    /// <summary>A new account by a link, with its own tenant (role owner).</summary>
    public static async Task<Person> OwnerAsync(ApiFactory factory, string? market = null)
    {
        var browser = factory.CreateApiClient();
        var email = NewEmail();
        var session = await browser.SignInByLinkAsync(factory, email, market);
        return new Person(browser, email, session.GetProperty("me").GetProperty("id").GetGuid(), session.GetProperty("createdTenantId").GetGuid());
    }

    /// <summary>Invites a new person with <paramref name="role"/> into the tenant of <paramref name="inviter"/>; he accepts it in his own browser.</summary>
    public static async Task<Person> MemberAsync(ApiFactory factory, Person inviter, string role, Guid? tenantId = null)
    {
        var tenant = tenantId ?? inviter.TenantId;
        var email = NewEmail(role);
        using (var invited = await inviter.Browser.PostAsync($"/api/t/{tenant}/invitations", new { email, role }))
        {
            Assert.Equal(HttpStatusCode.Created, invited.StatusCode);
        }

        var browser = factory.CreateApiClient();
        var token = factory.Emails.LastToken(email, EmailTemplateKind.Invitation.Code);
        using var accepted = await browser.PostAsync("/api/invitations/accept", new { token });
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        var body = await ApiClient.JsonAsync(accepted);
        return new Person(browser, email, body.GetProperty("me").GetProperty("id").GetGuid(), tenant);
    }

    public static async Task<JsonElement> MeAsync(ApiClient browser)
    {
        using var response = await browser.GetAsync("/api/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ApiClient.JsonAsync(response);
    }
}
