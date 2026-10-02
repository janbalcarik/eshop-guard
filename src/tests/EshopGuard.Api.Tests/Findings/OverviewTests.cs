namespace EshopGuard.Api.Tests.Findings;

/// <summary>„Prehľad“ of an e-shop (change 11, task 3.8).</summary>
public sealed class OverviewTests : FindingsTestBase
{
    [Fact]
    public async Task Overview_HasTheLastCheck_Counts_Tabs_PriorityPages_AndQuickAnswers()
    {
        var (factory, owner, data) = await BylinkovoAsync();
        await using var _ = factory;
        using var __ = owner;

        var overview = await GetJsonAsync(owner, S(owner, data.ShopId) + "/overview");

        Assert.Equal(data.Domain, overview.GetProperty("shop").GetProperty("domain").GetString());
        var run = overview.GetProperty("lastRun");
        Assert.Equal("full_analysis", run.GetProperty("kind").GetString());
        Assert.Equal(120, run.GetProperty("pagesChecked").GetInt32());
        Assert.Equal(7, run.GetProperty("notChecked").GetProperty("robotsBlocked").GetInt32());
        var counts = overview.GetProperty("findingCounts");
        Assert.Equal(43, counts.GetProperty("total").GetInt32());
        Assert.Equal(14, counts.GetProperty("text").GetInt32());
        Assert.Equal(23, counts.GetProperty("assess").GetInt32());
        Assert.Equal(6, counts.GetProperty("verify").GetInt32());
        Assert.Equal(12, overview.GetProperty("pageTabs").GetProperty("toApprove").GetInt32());
        Assert.Equal(5, overview.GetProperty("priorityPages").GetArrayLength());
        Assert.Equal(data.Pages["zubna"].PageId, overview.GetProperty("priorityPages")[0].GetProperty("pageId").GetGuid());
        var quick = overview.GetProperty("quickQuestions");
        Assert.Equal(5, quick.GetProperty("total").GetInt32());
        Assert.Equal(3, quick.GetProperty("items").GetArrayLength());
        Assert.Equal(12, overview.GetProperty("badges").GetProperty("fixes").GetInt32());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, overview.GetProperty("monitoring").ValueKind);
    }
}
