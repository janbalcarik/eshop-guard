using System.Net;

namespace EshopGuard.Api.Tests.Findings;

/// <summary>Decisions about a finding (change 11, design B; spec „Stavy nálezu a povolené přechody“).</summary>
public sealed class FindingDecisionTests : FindingsTestBase
{
    [Fact]
    public async Task Keep_RemembersTheDecision_AndWritesTheAudit()
    {
        var (factory, owner, data) = await BylinkovoAsync();
        await using var _ = factory;
        using var __ = owner;
        var findingId = data.Findings["zubna.5"];

        using var response = await owner.Browser.PostAsync($"{S(owner, data.ShopId)}/findings/{findingId}/keep", new { reasonCode = "no_promise" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = await ApiClient.JsonAsync(response);
        Assert.Equal("kept", detail.GetProperty("finding").GetProperty("status").GetString());
        var history = Assert.Single(detail.GetProperty("history").EnumerateArray());
        Assert.Equal("proposed", history.GetProperty("from").GetString());
        Assert.Equal("kept", history.GetProperty("to").GetString());
        Assert.Equal("no_promise", history.GetProperty("reasonCode").GetString());
        Assert.Equal(1L, await AdminScalarAsync<long>(
            "SELECT count(*) FROM fixes.decision_memory WHERE shop_id = $1 AND segment_hash = 7105 AND decision = 'keep' AND superseded_at IS NULL", data.ShopId));
    }

    [Fact]
    public async Task Reopen_SupersedesTheMemory_AndKeepsTheRow()
    {
        var (factory, owner, data) = await BylinkovoAsync();
        await using var _ = factory;
        using var __ = owner;
        var path = $"{S(owner, data.ShopId)}/findings/{data.Findings["zubna.5"]}";
        using (var kept = await owner.Browser.PostAsync(path + "/keep", new { reasonCode = "other" }))
        {
            Assert.Equal(HttpStatusCode.OK, kept.StatusCode);
        }

        using var reopened = await owner.Browser.PostAsync(path + "/reopen");

        Assert.Equal(HttpStatusCode.OK, reopened.StatusCode);
        Assert.Equal("open", (await ApiClient.JsonAsync(reopened)).GetProperty("finding").GetProperty("status").GetString());
        Assert.Equal(1L, await AdminScalarAsync<long>(
            "SELECT count(*) FROM fixes.decision_memory WHERE shop_id = $1 AND segment_hash = 7105 AND superseded_at IS NOT NULL", data.ShopId));
    }

    [Fact]
    public async Task PublishedFinding_CannotBeKept()
    {
        var (factory, owner, data) = await BylinkovoAsync();
        await using var _ = factory;
        using var __ = owner;

        using var response = await owner.Browser.PostAsync($"{S(owner, data.ShopId)}/findings/{data.Findings["published.0.0"]}/keep", new { reasonCode = "no_promise" });

        var problem = await ProblemAsync(response, HttpStatusCode.Conflict, "finding.transition_not_allowed");
        Assert.Equal("published", problem.GetProperty("params").GetProperty("from").GetString());
        Assert.Equal("kept", problem.GetProperty("params").GetProperty("to").GetString());
    }

    [Fact]
    public async Task Dismiss_NeedsTheReasonFalsePositive()
    {
        var (factory, owner, data) = await BylinkovoAsync();
        await using var _ = factory;
        using var __ = owner;
        var path = $"{S(owner, data.ShopId)}/findings/{data.Findings["approve.1.open"]}/dismiss";

        using var wrong = await owner.Browser.PostAsync(path, new { reasonCode = "no_promise" });
        using var right = await owner.Browser.PostAsync(path, new { reasonCode = "false_positive" });

        await ProblemAsync(wrong, HttpStatusCode.BadRequest, "validation.failed");
        Assert.Equal(HttpStatusCode.OK, right.StatusCode);
        Assert.Equal("dismissed", (await ApiClient.JsonAsync(right)).GetProperty("finding").GetProperty("status").GetString());
    }

    [Fact]
    public async Task SampleOnlyShop_RefusesDecisions()
    {
        var (factory, owner, data) = await BylinkovoAsync();
        await using var _ = factory;
        using var __ = owner;
        await AdminAsync("UPDATE shop.shops SET status = 'sample' WHERE id = $1", data.ShopId);

        using var response = await owner.Browser.PostAsync($"{S(owner, data.ShopId)}/findings/{data.Findings["zubna.5"]}/keep", new { reasonCode = "no_promise" });

        await ProblemAsync(response, HttpStatusCode.Conflict, "shop.sample_only");
    }
}
