using System.Net;

namespace EshopGuard.Api.Tests.Shops;

/// <summary>The settings of an e-shop (change 10, tasks 10.1 and 10.4; specification „Nastavení e-shopu“).</summary>
public sealed class ShopSettingsTests : ShopTestBase
{
    [Fact]
    public async Task Modules_FollowTheMarkets_CzWithoutEcoAndDur_SkWithAll()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        var shopId = (await CreateShopAsync(owner)).GetProperty("id").GetGuid();
        var path = $"/api/t/{owner.TenantId}/shops/{shopId}";

        using (var cz = await owner.Browser.PutAsync($"{path}/markets", new { active = new[] { "cz" } }))
        {
            Assert.Equal(HttpStatusCode.OK, cz.StatusCode);
        }

        var czSettings = await GetJsonAsync(owner, $"{path}/settings");
        using (var sk = await owner.Browser.PutAsync($"{path}/markets", new { active = new[] { "sk" } }))
        {
            Assert.Equal(HttpStatusCode.OK, sk.StatusCode);
        }

        var skSettings = await GetJsonAsync(owner, $"{path}/settings");

        Assert.Equal(["legal", "ucp"], Available(czSettings));
        Assert.Equal(["dur", "eco", "legal", "ucp"], Available(skSettings));
    }

    [Fact]
    public async Task UnavailableModule_AndNoModule_Are400()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        var shop = await CreateShopAsync(owner);
        var shopId = shop.GetProperty("id").GetGuid();
        var path = $"/api/t/{owner.TenantId}/shops/{shopId}";
        using (var cz = await owner.Browser.PutAsync($"{path}/markets", new { active = new[] { "cz" } }))
        {
            Assert.Equal(HttpStatusCode.OK, cz.StatusCode);
        }

        var version = (await GetJsonAsync(owner, $"{path}/settings")).GetProperty("version").GetUInt32();
        using var eco = await owner.Browser.PatchAsync($"{path}/settings", new { modules = new[] { "eco", "ucp" }, version });
        using var none = await owner.Browser.PatchAsync($"{path}/settings", new { modules = Array.Empty<string>(), version });

        var problem = await ApiClient.ProblemAsync(eco);
        Assert.Equal((HttpStatusCode.BadRequest, "settings.module_unavailable"), (problem.Status, problem.Code));
        Assert.Equal("eco", problem.Body.GetProperty("params").GetProperty("module").GetString());
        Assert.Equal("settings.no_module", (await ApiClient.ProblemAsync(none)).Code);
    }

    [Fact]
    public async Task CheckOnSave_NeedsAConnector()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        var shopId = (await CreateShopAsync(owner)).GetProperty("id").GetGuid();
        var path = $"/api/t/{owner.TenantId}/shops/{shopId}/settings";
        var settings = await GetJsonAsync(owner, path);

        using var response = await owner.Browser.PatchAsync(path, new { checkHiddenOnSave = true, version = settings.GetProperty("version").GetUInt32() });

        Assert.False(settings.GetProperty("checkHiddenOnSaveAvailable").GetBoolean());
        Assert.Equal((HttpStatusCode.Conflict, "settings.hidden_check_requires_connector"), ((await ApiClient.ProblemAsync(response)).Status, (await ApiClient.ProblemAsync(response)).Code));
    }

    [Fact]
    public async Task ConcurrentChange_Is409_AndTheFirstOneStays()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        using var admin = await People.MemberAsync(factory, owner, "admin");
        var shopId = (await CreateShopAsync(owner)).GetProperty("id").GetGuid();
        var path = $"/api/t/{owner.TenantId}/shops/{shopId}/settings";
        var version = (await GetJsonAsync(owner, path)).GetProperty("version").GetUInt32();

        using var first = await owner.Browser.PatchAsync(path, new { name = "Bylinkovo", modules = new[] { "legal" }, version });
        using var second = await admin.Browser.PatchAsync(path, new { name = "Iné", version });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal("concurrency.conflict", (await ApiClient.ProblemAsync(second)).Code);
        var stored = await GetJsonAsync(owner, path);
        Assert.Equal("Bylinkovo", stored.GetProperty("name").GetString());
        Assert.Equal(["legal"], stored.GetProperty("modules").EnumerateArray().Where(m => m.GetProperty("enabled").GetBoolean()).Select(m => m.GetProperty("module").GetString()));
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.audit_log WHERE action = 'settings.changed' AND entity_id = $1", shopId.ToString("D")));
    }

    private static List<string?> Available(System.Text.Json.JsonElement settings) =>
        settings.GetProperty("modules").EnumerateArray().Where(m => m.GetProperty("available").GetBoolean()).Select(m => m.GetProperty("module").GetString()).ToList();
}
