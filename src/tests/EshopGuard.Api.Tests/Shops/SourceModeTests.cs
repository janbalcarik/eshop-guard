using System.Net;

namespace EshopGuard.Api.Tests.Shops;

/// <summary>The way of reading the texts of an e-shop (change 10, task 3.3; specification „Způsob napojení e-shopu“).</summary>
public sealed class SourceModeTests : ShopTestBase
{
    [Fact]
    public async Task Feed_IsStoredWithItsFormat()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        var shopId = (await CreateShopAsync(owner)).GetProperty("id").GetGuid();

        using var response = await owner.Browser.PutAsync($"/api/t/{owner.TenantId}/shops/{shopId}/source",
            new { mode = "feed", feed = new { url = "https://eshop.sk/heureka.xml?key=1", format = "heureka" } });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var shop = await ApiClient.JsonAsync(response);
        Assert.Equal("feed", shop.GetProperty("sourceMode").GetString());
        Assert.Equal("https://eshop.sk/heureka.xml?key=1", shop.GetProperty("feed").GetProperty("url").GetString());
        Assert.Equal(["https://eshop.sk/heureka.xml?key=1", "heureka"], (await AdminRowsAsync("SELECT url, format FROM shop.feeds WHERE shop_id = $1", shopId)).Single());
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.audit_log WHERE action = 'shop.source_changed' AND entity_id = $1", shopId.ToString("D")));
    }

    [Fact]
    public async Task UnknownFormat_Is400_AndInternalFeedAddressIsRefused()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        var shopId = (await CreateShopAsync(owner)).GetProperty("id").GetGuid();
        var path = $"/api/t/{owner.TenantId}/shops/{shopId}/source";

        using var format = await owner.Browser.PutAsync(path, new { mode = "feed", feed = new { url = "https://eshop.sk/feed.xml", format = "csv" } });
        using var address = await owner.Browser.PutAsync(path, new { mode = "feed", feed = new { url = "http://10.0.0.1/feed.xml", format = "google" } });

        Assert.Equal("feed.format_unknown", (await ApiClient.ProblemAsync(format)).Code);
        Assert.Equal("feed.url_invalid", (await ApiClient.ProblemAsync(address)).Code);
        Assert.Equal("web", (await GetJsonAsync(owner, $"/api/t/{owner.TenantId}/shops/{shopId}")).GetProperty("sourceMode").GetString());
    }

    [Fact]
    public async Task ConnectorWithoutAConnection_Is409_AndTheWebStays()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        var shopId = (await CreateShopAsync(owner)).GetProperty("id").GetGuid();

        using var response = await owner.Browser.PutAsync($"/api/t/{owner.TenantId}/shops/{shopId}/source", new { mode = "connector" });

        Assert.Equal((HttpStatusCode.Conflict, "shop.connector_not_connected"), ((await ApiClient.ProblemAsync(response)).Status, (await ApiClient.ProblemAsync(response)).Code));
        Assert.Equal("web", await AdminScalarAsync<string>("SELECT source_mode FROM shop.shops WHERE id = $1", shopId));
    }

    [Fact]
    public async Task ChangeDuringARunningAnalysis_Is409()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        var shopId = (await CreateShopAsync(owner)).GetProperty("id").GetGuid();
        await SeedRunAsync(owner.TenantId, shopId, "full_analysis", "evaluating");

        using var response = await owner.Browser.PutAsync($"/api/t/{owner.TenantId}/shops/{shopId}/source", new { mode = "web" });

        Assert.Equal("shop.run_in_progress", (await ApiClient.ProblemAsync(response)).Code);
    }

    [Fact]
    public async Task UnknownMode_IsAValidationError()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        var shopId = (await CreateShopAsync(owner)).GetProperty("id").GetGuid();

        using var response = await owner.Browser.PutAsync($"/api/t/{owner.TenantId}/shops/{shopId}/source", new { mode = "ftp" });

        var problem = await ApiClient.ProblemAsync(response);
        Assert.Equal("validation.failed", problem.Code);
        Assert.Equal("source.mode_unknown", problem.Body.GetProperty("errors").GetProperty("mode")[0].GetString());
    }
}
