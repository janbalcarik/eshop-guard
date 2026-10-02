using System.Text.Json;

namespace EshopGuard.Api.Tests.Findings;

/// <summary>The tabs of „Opravy“ by pages over „bylinkovo.sk“ (change 11, task 3.6; AD 3).</summary>
public sealed class PageTabsTests : FindingsTestBase
{
    [Fact]
    public async Task Tabs_MatchTheDesign()
    {
        var (factory, owner, data) = await BylinkovoAsync();
        await using var _ = factory;
        using var __ = owner;

        var tabs = await GetJsonAsync(owner, S(owner, data.ShopId) + "/pages/tabs");

        Assert.Equal(24, tabs.GetProperty("toResolve").GetInt32());
        Assert.Equal(12, tabs.GetProperty("toApprove").GetInt32());
        Assert.Equal(12, tabs.GetProperty("needsAnswer").GetInt32());
        Assert.Equal(4, tabs.GetProperty("published").GetInt32());
        Assert.Equal(28, tabs.GetProperty("withFindings").GetInt32());
    }

    [Fact]
    public async Task PageWithAQuestion_IsOnlyInNeedsAnswer()
    {
        var (factory, owner, data) = await BylinkovoAsync();
        await using var _ = factory;
        using var __ = owner;
        var zubna = data.Pages["zubna"].PageId;

        var needsAnswer = await GetJsonAsync(owner, S(owner, data.ShopId) + "/pages?tab=needs_answer&limit=100");
        var toApprove = await GetJsonAsync(owner, S(owner, data.ShopId) + "/pages?tab=to_approve&limit=100");

        var item = needsAnswer.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("pageId").ValueKind == JsonValueKind.String && i.GetProperty("pageId").GetGuid() == zubna);
        Assert.Equal("needs_answer", item.GetProperty("status").GetString());
        Assert.Equal("question", item.GetProperty("preview").GetProperty("kind").GetString());
        Assert.Equal("material_evidence", item.GetProperty("preview").GetProperty("questionCode").GetString());
        Assert.DoesNotContain(toApprove.GetProperty("items").EnumerateArray(), i => i.GetProperty("pageId").ValueKind == JsonValueKind.String && i.GetProperty("pageId").GetGuid() == zubna);
        Assert.Contains(toApprove.GetProperty("items").EnumerateArray(), i => i.GetProperty("kind").GetString() == "site_template");
        Assert.Equal(12, needsAnswer.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task CzechVersion_KeepsTheWholeSiteItems()
    {
        var (factory, owner, data) = await BylinkovoAsync();
        await using var _ = factory;
        using var __ = owner;

        var page = await GetJsonAsync(owner, S(owner, data.ShopId) + "/pages?tab=to_resolve&language=cs&limit=100");

        var items = page.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(3, items.Count(i => i.GetProperty("kind").GetString() == "page"));
        Assert.All(items.Where(i => i.GetProperty("kind").GetString() == "page"), i => Assert.Equal("cs", i.GetProperty("language").GetString()));
        Assert.Contains(items, i => i.GetProperty("kind").GetString() == "site_obligations");
        Assert.Contains(items, i => i.GetProperty("kind").GetString() == "site_template");
    }
}
