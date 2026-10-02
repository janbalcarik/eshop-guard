using System.Net;
using System.Text.Json;

namespace EshopGuard.Api.Tests.Shops;

/// <summary>The language versions (change 10, task 6.4; specification „Jazykové verze, potvrzení a vyloučení“).</summary>
public sealed class LanguageVersionsTests : ShopTestBase
{
    [Fact]
    public async Task Bylinkovo_BothVersionsChecked_ForSkAndCz_WithTheTranslatedShare()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        var domain = NewDomain("bylinkovo");
        var shopId = (await CreateShopAsync(owner, "https://" + domain)).GetProperty("id").GetGuid();
        await SeedBylinkovoAsync(owner.TenantId, shopId, domain);

        var languages = await GetJsonAsync(owner, $"/api/t/{owner.TenantId}/shops/{shopId}/languages");

        var summary = languages.GetProperty("summary");
        Assert.Equal("all_checked", summary.GetProperty("kind").GetString());
        Assert.Equal(2, summary.GetProperty("versionsFound").GetInt32());
        Assert.True(summary.GetProperty("mutualJurisdictions").GetBoolean());
        var versions = languages.GetProperty("versions").EnumerateArray().ToList();
        Assert.Equal(["sk", "cs"], versions.Select(v => v.GetProperty("language").GetString()));
        Assert.All(versions, v =>
        {
            Assert.True(v.GetProperty("checked").GetBoolean());
            Assert.Equal(["cz", "sk"], v.GetProperty("jurisdictions").EnumerateArray().Select(j => j.GetString()));
        });
        var cs = versions[1];
        Assert.Equal(0.96, cs.GetProperty("translatedShare").GetDouble(), 3);
        Assert.Equal(1, cs.GetProperty("untranslatedProducts").GetInt32());
        Assert.Equal("sk", cs.GetProperty("foreignTextLanguage").GetString());
    }

    [Fact]
    public async Task VersionOnAnotherDomain_WaitsForTheClient_AndRejectedIsExcluded()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        var shopId = (await CreateShopAsync(owner)).GetProperty("id").GetGuid();
        await SeedLanguageAsync(owner.TenantId, shopId, "sk", "https://goodie.sk/", "main", "active", 100, 1, null);
        await SeedLanguageAsync(owner.TenantId, shopId, "cs", "https://goodie.cz/", "switcher", "needs_confirmation", 100, null, null);
        var path = $"/api/t/{owner.TenantId}/shops/{shopId}/languages";

        var before = await GetJsonAsync(owner, path);
        using var excludeWaiting = await owner.Browser.PutAsync($"{path}/cs/exclusion", new { excluded = true });
        using var rejected = await owner.Browser.PostAsync($"{path}/cs/confirmation", new { belongsToShop = false });
        using var again = await owner.Browser.PostAsync($"{path}/cs/confirmation", new { belongsToShop = true });

        Assert.Equal("needs_confirmation", before.GetProperty("summary").GetProperty("kind").GetString());
        Assert.Equal("language.awaiting_confirmation", (await ApiClient.ProblemAsync(excludeWaiting)).Code);
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        var cs = (await ApiClient.JsonAsync(rejected)).GetProperty("versions").EnumerateArray().Single(v => v.GetProperty("language").GetString() == "cs");
        Assert.Equal("excluded", cs.GetProperty("status").GetString());
        Assert.Equal("language.not_awaiting_confirmation", (await ApiClient.ProblemAsync(again)).Code);
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.audit_log WHERE action = 'language.rejected_other_domain' AND entity_id = $1", shopId.ToString("D")));
        Assert.Equal(owner.UserId, await AdminScalarAsync<Guid>("SELECT decided_by FROM shop.shop_languages WHERE shop_id = $1 AND language = 'cs'", shopId));
    }

    [Fact]
    public async Task ConfirmedVersion_IsActive()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        var shopId = (await CreateShopAsync(owner)).GetProperty("id").GetGuid();
        await SeedLanguageAsync(owner.TenantId, shopId, "sk", "https://goodie.sk/", "main", "active", 100, 1, null);
        await SeedLanguageAsync(owner.TenantId, shopId, "cs", "https://goodie.cz/", "switcher", "needs_confirmation", 100, null, null);

        using var confirmed = await owner.Browser.PostAsync($"/api/t/{owner.TenantId}/shops/{shopId}/languages/cs/confirmation", new { belongsToShop = true });

        Assert.Equal("active", await AdminScalarAsync<string>("SELECT status FROM shop.shop_languages WHERE shop_id = $1 AND language = 'cs'", shopId));
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.audit_log WHERE action = 'language.confirmed' AND entity_id = $1", shopId.ToString("D")));
    }

    [Fact]
    public async Task UnsupportedVersion_IsNotReturned_AndIsNotFound()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        var domain = NewDomain();
        var shopId = (await CreateShopAsync(owner, "https://" + domain)).GetProperty("id").GetGuid();
        await SeedBylinkovoAsync(owner.TenantId, shopId, domain);

        var languages = await GetJsonAsync(owner, $"/api/t/{owner.TenantId}/shops/{shopId}/languages");
        using var polish = await owner.Browser.PutAsync($"/api/t/{owner.TenantId}/shops/{shopId}/languages/pl/exclusion", new { excluded = true });

        Assert.DoesNotContain(languages.GetProperty("versions").EnumerateArray(), v => v.GetProperty("language").GetString() == "pl");
        Assert.Equal((HttpStatusCode.NotFound, "language.not_found"), ((await ApiClient.ProblemAsync(polish)).Status, (await ApiClient.ProblemAsync(polish)).Code));
    }

    [Fact]
    public async Task ExcludingTheLastCheckedVersion_Is400_AnotherOneIsExcludedAndIncludedAgain()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        var domain = NewDomain();
        var shopId = (await CreateShopAsync(owner, "https://" + domain)).GetProperty("id").GetGuid();
        await SeedBylinkovoAsync(owner.TenantId, shopId, domain);
        using (var markets = await owner.Browser.PutAsync($"/api/t/{owner.TenantId}/shops/{shopId}/markets", new { active = new[] { "sk" } }))
        {
            Assert.Equal(HttpStatusCode.OK, markets.StatusCode);
        }

        var path = $"/api/t/{owner.TenantId}/shops/{shopId}/languages";
        using var last = await owner.Browser.PutAsync($"{path}/sk/exclusion", new { excluded = true });
        using var other = await owner.Browser.PutAsync($"{path}/cs/exclusion", new { excluded = true });
        var excluded = await ApiClient.JsonAsync(other);
        using var back = await owner.Browser.PutAsync($"{path}/cs/exclusion", new { excluded = false });

        Assert.Equal((HttpStatusCode.BadRequest, "language.last_checked_version"), ((await ApiClient.ProblemAsync(last)).Status, (await ApiClient.ProblemAsync(last)).Code));
        Assert.Equal("excluded", Version(excluded, "cs").GetProperty("status").GetString());
        Assert.Equal("active", Version(await ApiClient.JsonAsync(back), "cs").GetProperty("status").GetString());
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.audit_log WHERE action = 'language.excluded' AND entity_id = $1", shopId.ToString("D")));
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.audit_log WHERE action = 'language.included' AND entity_id = $1", shopId.ToString("D")));
    }

    private static JsonElement Version(JsonElement body, string language) =>
        body.GetProperty("versions").EnumerateArray().Single(v => v.GetProperty("language").GetString() == language);
}
