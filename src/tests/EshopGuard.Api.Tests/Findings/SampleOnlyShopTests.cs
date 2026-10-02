using System.Net;
using System.Text.Json;
using static EshopGuard.Api.Tests.Findings.ProposalTests;

namespace EshopGuard.Api.Tests.Findings;

/// <summary>
/// An e-shop with only the free sample (change 11, task 12.2; AD 14, K rozhodnutí 4): the lists show the 5 findings of the
/// summary of the sample, the rest only in the counts; every decision is <c>409 shop.sample_only</c>.
/// </summary>
public sealed class SampleOnlyShopTests : FindingsTestBase
{
    private static readonly string[] Top = ["zubna.1", "zubna.3", "zubna.4", "zubna.5", "vodnar"];

    [Fact]
    public async Task Lists_ShowTheFiveFindingsOfTheSample_AndTheCountsStayWhole()
    {
        var (factory, owner, data) = await BylinkovoAsync();
        await using var _ = factory;
        using var __ = owner;
        var path = S(owner, data.ShopId);
        string tabsBefore;
        using (var tabs = await owner.Browser.GetAsync($"{path}/pages/tabs"))
        {
            tabsBefore = (await ApiClient.JsonAsync(tabs)).GetRawText();
        }

        await SampleAsync(owner, data);

        using (var list = await owner.Browser.GetAsync($"{path}/findings?limit=100"))
        {
            var ids = (await ApiClient.JsonAsync(list)).GetProperty("items").EnumerateArray().Select(i => i.GetProperty("findingId").GetGuid()).ToHashSet();
            Assert.Equal(Top.Select(t => data.Findings[t]).ToHashSet(), ids);
        }

        using (var hidden = await owner.Browser.GetAsync($"{path}/findings/{data.Findings["serum"]}"))
        {
            await ProblemAsync(hidden, HttpStatusCode.NotFound, "finding.not_found");
        }

        using var tabsAfter = await owner.Browser.GetAsync($"{path}/pages/tabs");
        Assert.Equal(tabsBefore, (await ApiClient.JsonAsync(tabsAfter)).GetRawText());
        using var groups = await owner.Browser.GetAsync($"{path}/fix-groups");
        Assert.Empty((await ApiClient.JsonAsync(groups)).GetProperty("groups").EnumerateArray());
    }

    [Fact]
    public async Task EveryDecision_IsRefused()
    {
        var (factory, owner, data) = await BylinkovoAsync();
        await using var _ = factory;
        using var __ = owner;
        var path = S(owner, data.ShopId);
        var proposal = $"{path}/proposals/{data.Proposals["zubna.3"]}";
        var etag = await ETagAsync(owner, proposal);
        var group = $"{path}/fix-groups/{data.Groups["group"]}";
        var groupTag = await ETagAsync(owner, group);
        await SampleAsync(owner, data);

        var requests = new (HttpMethod Method, string Path, object? Body, System.Net.Http.Headers.EntityTagHeaderValue? ETag)[]
        {
            (HttpMethod.Post, $"{path}/findings/{data.Findings["zubna.5"]}/keep", new { reasonCode = "no_promise" }, null),
            (HttpMethod.Post, $"{path}/findings/{data.Findings["zubna.5"]}/dismiss", new { reasonCode = "false_positive" }, null),
            (HttpMethod.Put, proposal + "/text", new { text = "Bambusová kefka." }, etag),
            (HttpMethod.Post, proposal + "/accept", null, etag),
            (HttpMethod.Put, group + "/values", new { values = new Dictionary<string, string> { ["materiál obalu"] = "papiera" } }, groupTag),
            (HttpMethod.Post, group + "/approve", null, groupTag),
            (HttpMethod.Post, $"{path}/questions/{data.Questions["vodnar"]}/answer", new { answer = "yes" }, null),
            (HttpMethod.Post, $"{path}/publications", new { pageIds = new[] { data.Pages["zubna"].PageId } }, null),
        };
        foreach (var (method, url, body, tag) in requests)
        {
            using var response = await SendAsync(owner, method, url, body, tag);
            await ProblemAsync(response, HttpStatusCode.Conflict, "shop.sample_only");
        }

        Assert.Equal(0L, await AdminScalarAsync<long>("SELECT count(*) FROM fixes.decision_memory WHERE shop_id = $1", data.ShopId));
    }

    /// <summary>The e-shop with only the sample, whose summary names the 5 findings of <see cref="Top"/>.</summary>
    private static async Task SampleAsync(Person owner, BylinkovoSeed data)
    {
        var top = JsonSerializer.Serialize(new { sample = new { top_finding_ids = Top.Select(t => data.Findings[t].ToString("D")) } });
        await AdminAsync("UPDATE shop.shops SET status = 'sample' WHERE id = $1", data.ShopId);
        await AdminAsync(
            """
            INSERT INTO checks.runs (tenant_id, shop_id, kind, trigger, status, priority, jurisdictions, modules, stats, finished_at, created_at, updated_at)
            VALUES ($1, $2, 'free_sample', 'user', 'finished', 0, '{sk}', '{eco}', $3::jsonb, now(), now(), now())
            """, owner.TenantId, data.ShopId, top);
    }
}
