using System.Text.Json;

namespace EshopGuard.Api.Tests.Findings;

/// <summary>„Oprava stránky“ (change 11, task 4.8; AD 4).</summary>
public sealed class PageReviewTests : FindingsTestBase
{
    [Fact]
    public async Task ToothpastePage_HasFiveChangesInTheOrderOfItsText_WithContext_Source_AndPosition()
    {
        var (factory, owner, data) = await BylinkovoAsync();
        await using var _ = factory;
        using var __ = owner;
        var versions = await AdminScalarAsync<long>("SELECT count(*) FROM content.page_versions WHERE shop_id = $1", data.ShopId);

        var review = await GetJsonAsync(owner, $"{S(owner, data.ShopId)}/pages/{data.Pages["zubna"].PageId}/review?tab=to_resolve");

        var changes = review.GetProperty("changes").EnumerateArray().ToList();
        Assert.Equal(5, changes.Count);
        Assert.Equal(Enumerable.Range(1, 5), changes.Select(c => c.GetProperty("index").GetInt32()));
        Assert.Equal(["replace", "group", "replace", "question", "replace"], changes.Select(c => c.GetProperty("kind").GetString()));
        var group = changes[1];
        Assert.Equal(38, group.GetProperty("group").GetProperty("pageCount").GetInt32());
        Assert.Equal("materiál obalu", group.GetProperty("placeholders")[0].GetProperty("key").GetString());
        Assert.Equal("Okamžitý komfort pre citlivé zuby v ekologickom sete.", group.GetProperty("contextBefore").GetString());
        Assert.Equal("Bambusová kefka – ekologická alternatíva", group.GetProperty("contextAfter").GetString());
        Assert.Equal("material_evidence", changes[3].GetProperty("questionCode").GetString());
        Assert.Equal(2, changes[0].GetProperty("alternatives").GetArrayLength());
        Assert.Equal(3, review.GetProperty("unchangedBlocks").GetInt32());

        var source = review.GetProperty("page").GetProperty("source");
        Assert.Equal("connector", source.GetProperty("kind").GetString());
        Assert.Equal("shoptet", source.GetProperty("platform").GetString());
        Assert.Equal("2429", source.GetProperty("externalId").GetString());
        var position = review.GetProperty("position");
        Assert.Equal(1, position.GetProperty("index").GetInt32());
        Assert.Equal(24, position.GetProperty("total").GetInt32());
        Assert.Equal(JsonValueKind.String, position.GetProperty("nextPageId").ValueKind);
        Assert.Equal(versions, await AdminScalarAsync<long>("SELECT count(*) FROM content.page_versions WHERE shop_id = $1", data.ShopId));
    }

    [Fact]
    public async Task ShopWithoutAConnector_CannotPublish()
    {
        var (factory, owner, data) = await BylinkovoAsync();
        await using var _ = factory;
        using var __ = owner;
        await AdminAsync("UPDATE shop.shops SET source_mode = 'web' WHERE id = $1", data.ShopId);

        var review = await GetJsonAsync(owner, $"{S(owner, data.ShopId)}/pages/{data.Pages["zubna"].PageId}/review");

        Assert.False(review.GetProperty("publish").GetProperty("available").GetBoolean());
        Assert.Equal("no_connector", review.GetProperty("publish").GetProperty("reasonCode").GetString());
    }

    [Fact]
    public async Task ConnectorWithoutAPublisher_SaysSo()
    {
        var (factory, owner, data) = await BylinkovoAsync();
        await using var _ = factory;
        using var __ = owner;

        var review = await GetJsonAsync(owner, $"{S(owner, data.ShopId)}/pages/{data.Pages["zubna"].PageId}/review");

        Assert.Equal("publisher_missing", review.GetProperty("publish").GetProperty("reasonCode").GetString());
    }

    [Fact]
    public async Task UnknownPage_Is404()
    {
        var (factory, owner, data) = await BylinkovoAsync();
        await using var _ = factory;
        using var __ = owner;

        using var response = await owner.Browser.GetAsync($"{S(owner, data.ShopId)}/pages/{Guid.NewGuid()}/review");

        await ProblemAsync(response, System.Net.HttpStatusCode.NotFound, "page.not_found");
    }
}
