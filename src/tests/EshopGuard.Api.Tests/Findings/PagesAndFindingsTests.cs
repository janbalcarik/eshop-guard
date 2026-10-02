using System.Net;
using System.Text.Json;
using EshopGuard.Storage;
using Microsoft.Extensions.DependencyInjection;
using static EshopGuard.Api.Tests.Findings.ShopSeed;

namespace EshopGuard.Api.Tests.Findings;

/// <summary>Lists of pages and findings (change 11, task 3.7): order by the strictest verdict, a stable cursor, filters and tabs.</summary>
public sealed class PagesAndFindingsTests : FindingsTestBase
{
    [Fact]
    public async Task Pages_AreOrderedByTheStrictestVerdict_ThenByCount()
    {
        var (factory, owner, data) = await BylinkovoAsync();
        await using var _ = factory;
        using var __ = owner;

        var page = await GetJsonAsync(owner, S(owner, data.ShopId) + "/pages?tab=to_resolve&limit=100");

        var items = page.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(24, items.Count);
        Assert.Null(page.GetProperty("nextCursor").GetString());
        Assert.Equal(data.Pages["zubna"].PageId, items[0].GetProperty("pageId").GetGuid());
        Assert.Equal("sk", items[0].GetProperty("strictest").GetProperty("jurisdiction").GetString());
        Assert.Equal("text", items[0].GetProperty("strictest").GetProperty("checkability").GetString());
        var counts = items[0].GetProperty("counts");
        Assert.Equal(3, counts.GetProperty("text").GetInt32());
        Assert.Equal(1, counts.GetProperty("assess").GetInt32());
        Assert.Equal(1, counts.GetProperty("verify").GetInt32());
        // The template comes right after: as strict, fewer findings.
        Assert.Equal("site_template", items[1].GetProperty("kind").GetString());
    }

    [Fact]
    public async Task FindingTabs_AreTheDesignCounts()
    {
        var (factory, owner, data) = await BylinkovoAsync();
        await using var _ = factory;
        using var __ = owner;

        var tabs = await GetJsonAsync(owner, S(owner, data.ShopId) + "/findings/tabs");

        Assert.Equal(14, tabs.GetProperty("text").GetInt32());
        Assert.Equal(23, tabs.GetProperty("assess").GetInt32());
        Assert.Equal(6, tabs.GetProperty("verify").GetInt32());
        Assert.Equal(9, tabs.GetProperty("fixed").GetInt32());
    }

    [Fact]
    public async Task FindingDetail_HasVerdictsByCountry_TheStrictestFirst_AndContextFromTheExtraction()
    {
        var (factory, owner, data) = await BylinkovoAsync();
        await using var _ = factory;
        using var __ = owner;

        var detail = await GetJsonAsync(owner, $"{S(owner, data.ShopId)}/findings/{data.Findings["group"]}");

        var finding = detail.GetProperty("finding");
        Assert.Equal(["sk", "cz"], finding.GetProperty("verdicts").EnumerateArray().Select(v => v.GetProperty("jurisdiction").GetString()));
        Assert.Equal("sk", finding.GetProperty("strictest").GetProperty("jurisdiction").GetString());
        Assert.Equal("text", finding.GetProperty("strictest").GetProperty("checkability").GetString());
        Assert.Contains("108/2024 Z. z.", finding.GetProperty("verdicts")[0].GetProperty("legalRefs")[0].GetProperty("ref").GetString(), StringComparison.Ordinal);
        Assert.Equal("Okamžitý komfort pre citlivé zuby v ekologickom sete.", detail.GetProperty("contextBefore").GetString());
        Assert.Equal("Bambusová kefka – ekologická alternatíva", detail.GetProperty("contextAfter").GetString());
        Assert.Single(detail.GetProperty("occurrencePages").EnumerateArray());
    }

    [Fact]
    public async Task UnknownCheckability_IsRefused()
    {
        var (factory, owner, data) = await BylinkovoAsync();
        await using var _ = factory;
        using var __ = owner;

        using var response = await owner.Browser.GetAsync(S(owner, data.ShopId) + "/findings?checkability=fatal");

        var problem = await ProblemAsync(response, HttpStatusCode.BadRequest, "validation.failed");
        Assert.Equal("value.not_allowed", problem.GetProperty("errors").GetProperty("checkability")[0].GetString());
    }

    [Fact]
    public async Task Findings_FilterByGroupCountryAndVersion()
    {
        var (factory, owner, data) = await BylinkovoAsync();
        await using var _ = factory;
        using var __ = owner;
        var path = S(owner, data.ShopId);

        var verify = await GetJsonAsync(owner, path + "/findings?checkability=verify&limit=100");
        var czech = await GetJsonAsync(owner, path + "/findings?language=cs&limit=100");
        var cz = await GetJsonAsync(owner, path + "/findings?jurisdiction=cz&limit=100");

        Assert.Equal(6, verify.GetProperty("total").GetInt32());
        // One finding on each of the 3 Czech pages, and the finding of the whole site.
        Assert.Equal(4, czech.GetProperty("total").GetInt32());
        Assert.All(cz.GetProperty("items").EnumerateArray(), i => Assert.Contains(i.GetProperty("verdicts").EnumerateArray(), v => v.GetProperty("jurisdiction").GetString() == "cz"));
    }

    [Fact]
    public async Task Cursor_IsStable_Over120Findings()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        var shopId = (await CreateShopAsync(owner)).GetProperty("id").GetGuid();
        await ExecuteAsync("UPDATE shop.shops SET status = 'active' WHERE id = $1", shopId);
        var seed = new ShopSeed(owner.TenantId, shopId, factory.Services.GetRequiredService<IBlobStore>());
        await seed.RunAsync();
        var eco = await RuleSetAsync("eco", "eco");
        var page = await seed.PageAsync("Stránka", "/stranka/", "sk", "Stránka");
        string[] groups = ["text", "assess", "verify"];
        string[] severities = ["high", "medium", "low"];
        for (var i = 0; i < 120; i++)
        {
            await seed.FindingAsync(eco, "eco_generic_claim", "eco", Verdicts(Verdict("sk", groups[i % 3], severities[i / 3 % 3])), "open", $"Veta {i:000}", 9_000 + i, [page.PageId]);
        }

        var seen = new List<Guid>();
        string? cursor = null;
        var ranks = new List<int>();
        do
        {
            var list = await GetJsonAsync(owner, $"{S(owner, shopId)}/findings?limit=25{(cursor is null ? "" : "&cursor=" + cursor)}");
            foreach (var item in list.GetProperty("items").EnumerateArray())
            {
                seen.Add(item.GetProperty("findingId").GetGuid());
                var strictest = item.GetProperty("strictest");
                ranks.Add((Array.IndexOf(groups, strictest.GetProperty("checkability").GetString()) * 4) + Array.IndexOf(severities, strictest.GetProperty("severity").GetString()));
            }

            cursor = list.GetProperty("nextCursor").GetString();
        }
        while (cursor is not null);

        Assert.Equal(120, seen.Count);
        Assert.Equal(120, seen.Distinct().Count());
        Assert.Equal(ranks.Order(), ranks);
    }

    [Fact]
    public async Task BadCursor_IsAValidationError()
    {
        var (factory, owner, data) = await BylinkovoAsync();
        await using var _ = factory;
        using var __ = owner;

        using var response = await owner.Browser.GetAsync(S(owner, data.ShopId) + "/findings?cursor=nonsense");

        await ProblemAsync(response, HttpStatusCode.BadRequest, "validation.failed");
    }

    [Fact]
    public async Task Search_FindsPagesAndFindings_AndNeedsTwoCharacters()
    {
        var (factory, owner, data) = await BylinkovoAsync();
        await using var _ = factory;
        using var __ = owner;

        var result = await GetJsonAsync(owner, S(owner, data.ShopId) + "/search?q=vodn%C3%A1r");
        using var tooShort = await owner.Browser.GetAsync(S(owner, data.ShopId) + "/search?q=a");

        Assert.Equal(4, result.GetProperty("pages").GetArrayLength());
        var bambus = await GetJsonAsync(owner, S(owner, data.ShopId) + "/search?q=bambus");
        Assert.Contains(bambus.GetProperty("findings").EnumerateArray(), f => f.GetProperty("text").GetString() == "Bambusová kefka – ekologická alternatíva");
        await ProblemAsync(tooShort, HttpStatusCode.BadRequest, "search.query_too_short");
    }
}
