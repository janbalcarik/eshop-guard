namespace EshopGuard.Api.Tests.Shops;

/// <summary>Logs of the e-shops in the API (change 10, task 11.3): codes and ids only, never quotes, texts of pages or tokens.</summary>
public sealed class ShopLogRedactionTests : ShopTestBase
{
    [Fact]
    public async Task LogsOfTheOnboarding_HoldNoQuotesTextsOrTokens()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        var domain = NewDomain("bylinkovo");
        var shopId = (await CreateShopAsync(owner, "https://" + domain)).GetProperty("id").GetGuid();
        await SeedBylinkovoAsync(owner.TenantId, shopId, domain);
        var path = $"/api/t/{owner.TenantId}/shops/{shopId}";

        foreach (var get in new[] { "/sample", "/markets", "/languages", "/scope", "/onboarding", "/ownership", "/settings" })
        {
            await GetJsonAsync(owner, path + get);
        }

        using var verification = await owner.Browser.PostAsync($"{path}/ownership/verifications", new { method = "meta" });
        var token = (await ApiClient.JsonAsync(verification)).GetProperty("token").GetString()!;
        string[] secrets = [token, "Doprava do Českej republiky", "Hlavná 1, Trenčín", "interní"];

        Assert.NotEmpty(factory.Logs.Logs);
        Assert.All(factory.Logs.Logs, l => Assert.All(secrets, s => Assert.DoesNotContain(s, l.AllText, StringComparison.Ordinal)));
    }
}
