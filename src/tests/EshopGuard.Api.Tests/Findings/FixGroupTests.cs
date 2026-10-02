using System.Net;
using System.Text.Json;
using EshopGuard.Api.Tests.Shops;
using EshopGuard.Application.Fixes;
using EshopGuard.Storage;
using Microsoft.Extensions.DependencyInjection;
using static EshopGuard.Api.Tests.Findings.ProposalTests;
using static EshopGuard.Api.Tests.Findings.ShopSeed;

namespace EshopGuard.Api.Tests.Findings;

/// <summary>
/// Bulk fixes (change 11, task 7.5; design E): the sentence „Všetky naše produkty balíme ekologicky.“ on 38 pages of an e-shop,
/// 2 of them to fix one by one (<c>fit.individual</c>). One decision writes 36 accepted proposals in one transaction.
/// </summary>
public sealed class FixGroupTests : FindingsTestBase
{
    private const long Hash = 8_001;
    private const string Sentence = "Všetky naše produkty balíme ekologicky.";
    private const string Fact = "papierovej krabice bez plastovej výplne";

    [Fact]
    public async Task OneDecisionInsteadOf36_AcceptsTheIncludedPages_AndRemembersTheDecisionOnce()
    {
        var (factory, owner, shop) = await ShopAsync();
        await using var _ = factory;
        using var __ = owner;
        var path = $"{S(owner, shop.ShopId)}/fix-groups/{shop.GroupId}";

        using (var detail = await owner.Browser.GetAsync(path))
        {
            var body = await ApiClient.JsonAsync(detail);
            Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
            Assert.Equal((38, 36, 2), (body.GetProperty("pages").GetProperty("total").GetInt32(), body.GetProperty("fit").GetProperty("fits").GetInt32(),
                body.GetProperty("fit").GetProperty("individual").GetArrayLength()));
            Assert.Equal("needs_value", body.GetProperty("status").GetString());
            Assert.Equal(FixGroupService.PageLimit, body.GetProperty("pages").GetProperty("items").GetArrayLength());
            Assert.Equal(FixGroupService.SampleCount, body.GetProperty("samples").GetArrayLength());
            Assert.Equal("Okamžite po objednávke expedujeme.", body.GetProperty("samples")[0].GetProperty("after").GetString());
            var cursor = body.GetProperty("pages").GetProperty("nextCursor").GetString();
            using var next = await owner.Browser.GetAsync($"{path}?cursor={cursor}");
            var rest = (await ApiClient.JsonAsync(next)).GetProperty("pages");
            Assert.Equal(8, rest.GetProperty("items").GetArrayLength());
            Assert.Equal(JsonValueKind.Null, rest.GetProperty("nextCursor").ValueKind);
        }

        using (var filled = await SendAsync(owner, HttpMethod.Put, path + "/values", new { values = new Dictionary<string, string> { ["materiál obalu"] = Fact } },
            await ETagAsync(owner, path)))
        {
            Assert.Equal(HttpStatusCode.Accepted, filled.StatusCode);
            var body = await ApiClient.JsonAsync(filled);
            Assert.Equal("Všetky naše produkty balíme do " + Fact + ".", body.GetProperty("replacement").GetString());
            Assert.Equal(("draft", "pending"), (body.GetProperty("status").GetString(), body.GetProperty("recheck").GetProperty("status").GetString()));
        }

        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.jobs WHERE kind = 'fix.recheck' AND shop_id = $1 AND payload->>'target' = 'group'", shop.ShopId));
        using (var early = await SendAsync(owner, HttpMethod.Post, path + "/approve", null, await ETagAsync(owner, path)))
        {
            await ProblemAsync(early, HttpStatusCode.Conflict, "group.recheck_pending");
        }

        await RecheckOkAsync(shop.GroupId);
        using var approved = await SendAsync(owner, HttpMethod.Post, path + "/approve", null, await ETagAsync(owner, path));

        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        Assert.Equal("approved", (await ApiClient.JsonAsync(approved)).GetProperty("status").GetString());
        Assert.Equal(36L, await AdminScalarAsync<long>(
            "SELECT count(*) FROM fixes.fix_proposals WHERE group_id = $1 AND status = 'accepted' AND proposed_text = $2", shop.GroupId, "Všetky naše produkty balíme do " + Fact + "."));
        Assert.Equal(0L, await AdminScalarAsync<long>("SELECT count(*) FROM fixes.fix_proposals WHERE shop_id = $1 AND page_id = ANY($2)", shop.ShopId, shop.Individual));
        Assert.Equal(1L, await AdminScalarAsync<long>(
            "SELECT count(*) FROM fixes.decision_memory WHERE shop_id = $1 AND segment_hash = $2 AND decision = 'replace' AND superseded_at IS NULL", shop.ShopId, Hash));
        // The 2 pages to fix one by one still carry the sentence: the finding waits for them (fail-closed).
        Assert.Equal("proposed", await AdminScalarAsync<string>("SELECT status FROM checks.findings WHERE id = $1", shop.FindingId));
        Assert.NotNull(await AdminScalarAsync<DateTime?>("SELECT locked_at FROM fixes.fix_groups WHERE id = $1", shop.GroupId));
        Assert.Equal(1L, await AdminScalarAsync<long>(
            "SELECT count(*) FROM ops.audit_log WHERE tenant_id = $1 AND action = 'group.approved' AND entity_id = $2 AND (data->>'pages')::int = 36",
            owner.TenantId, shop.GroupId.ToString("D")));

        using var again = await SendAsync(owner, HttpMethod.Put, path + "/values", new { values = new Dictionary<string, string> { ["materiál obalu"] = "kartónu" } },
            await ETagAsync(owner, path));
        await ProblemAsync(again, HttpStatusCode.Conflict, "group.locked");
    }

    [Fact]
    public async Task MissingFact_IsRefused_AndNothingChanges()
    {
        var (factory, owner, shop) = await ShopAsync();
        await using var _ = factory;
        using var __ = owner;
        var path = $"{S(owner, shop.ShopId)}/fix-groups/{shop.GroupId}";

        using var approved = await SendAsync(owner, HttpMethod.Post, path + "/approve", null, await ETagAsync(owner, path));

        var problem = await ProblemAsync(approved, HttpStatusCode.Conflict, "group.value_missing");
        Assert.Equal("materiál obalu", problem.GetProperty("params").GetProperty("keys")[0].GetString());
        Assert.Equal(0L, await AdminScalarAsync<long>("SELECT count(*) FROM fixes.fix_proposals WHERE group_id = $1", shop.GroupId));
        Assert.Equal("open", await AdminScalarAsync<string>("SELECT status FROM checks.findings WHERE id = $1", shop.FindingId));
    }

    [Fact]
    public async Task OwnWording_WaitsForItsRecheck()
    {
        var (factory, owner, shop) = await ShopAsync();
        await using var _ = factory;
        using var __ = owner;
        var path = $"{S(owner, shop.ShopId)}/fix-groups/{shop.GroupId}";

        using (var empty = await SendAsync(owner, HttpMethod.Put, path + "/mode", new { mode = "custom", customText = "  " }, await ETagAsync(owner, path)))
        {
            await ProblemAsync(empty, HttpStatusCode.BadRequest, "group.custom_text_required");
        }

        using (var custom = await SendAsync(owner, HttpMethod.Put, path + "/mode", new { mode = "custom", customText = "Balíme do papiera." }, await ETagAsync(owner, path)))
        {
            Assert.Equal(HttpStatusCode.Accepted, custom.StatusCode);
            Assert.Equal("Balíme do papiera.", (await ApiClient.JsonAsync(custom)).GetProperty("replacement").GetString());
        }

        using var approved = await SendAsync(owner, HttpMethod.Post, path + "/approve", null, await ETagAsync(owner, path));

        await ProblemAsync(approved, HttpStatusCode.Conflict, "group.recheck_pending");
    }

    [Fact]
    public async Task Removal_NeedsNoRecheck_AndExcludedPagesKeepTheirText()
    {
        var (factory, owner, shop) = await ShopAsync();
        await using var _ = factory;
        using var __ = owner;
        var path = $"{S(owner, shop.ShopId)}/fix-groups/{shop.GroupId}";

        using (var removal = await SendAsync(owner, HttpMethod.Put, path + "/mode", new { mode = "remove" }, await ETagAsync(owner, path)))
        {
            Assert.Equal(HttpStatusCode.OK, removal.StatusCode);
            Assert.Equal("ok", (await ApiClient.JsonAsync(removal)).GetProperty("recheck").GetProperty("status").GetString());
        }

        using (var foreign = await SendAsync(owner, HttpMethod.Put, path + "/excluded-pages", new { pageIds = new[] { Guid.CreateVersion7() } }, await ETagAsync(owner, path)))
        {
            await ProblemAsync(foreign, HttpStatusCode.BadRequest, "group.page_not_in_group");
        }

        using (var excluded = await SendAsync(owner, HttpMethod.Put, path + "/excluded-pages", new { pageIds = new[] { shop.Pages[10], shop.Pages[11] } },
            await ETagAsync(owner, path)))
        {
            var body = await ApiClient.JsonAsync(excluded);
            Assert.Equal(HttpStatusCode.OK, excluded.StatusCode);
            Assert.Equal(2, body.GetProperty("pages").GetProperty("excludedPageIds").GetArrayLength());
        }

        using var approved = await SendAsync(owner, HttpMethod.Post, path + "/approve", null, await ETagAsync(owner, path));

        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        Assert.Equal(34L, await AdminScalarAsync<long>("SELECT count(*) FROM fixes.fix_proposals WHERE group_id = $1 AND status = 'accepted'", shop.GroupId));
        Assert.Equal(0L, await AdminScalarAsync<long>("SELECT count(*) FROM fixes.fix_proposals WHERE group_id = $1 AND proposed_text LIKE '%ekologicky%'", shop.GroupId));
        Assert.Equal(0L, await AdminScalarAsync<long>(
            "SELECT count(*) FROM fixes.fix_proposals WHERE shop_id = $1 AND page_id = ANY($2)", shop.ShopId, new[] { shop.Pages[10], shop.Pages[11] }));
        Assert.Equal(1L, await AdminScalarAsync<long>(
            "SELECT count(*) FROM fixes.decision_memory WHERE shop_id = $1 AND segment_hash = $2 AND decision = 'remove'", shop.ShopId, Hash));
    }

    [Fact]
    public async Task OnlyOnThisPage_AcceptsOneProposal_AndTheGroupStaysOpen()
    {
        var (factory, owner, shop) = await ShopAsync(filled: true);
        await using var _ = factory;
        using var __ = owner;
        var path = $"{S(owner, shop.ShopId)}/fix-groups/{shop.GroupId}";

        using (var individual = await SendAsync(owner, HttpMethod.Post, path + "/approve-page", new { pageId = shop.Individual[0] }, null))
        {
            await ProblemAsync(individual, HttpStatusCode.Conflict, "group.page_needs_individual_fix");
        }

        using var page = await SendAsync(owner, HttpMethod.Post, path + "/approve-page", new { pageId = shop.Pages[5] }, null);

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var proposal = await ApiClient.JsonAsync(page);
        Assert.Equal("accepted", proposal.GetProperty("status").GetString());
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM fixes.fix_proposals WHERE group_id = $1", shop.GroupId));
        Assert.Equal("draft", await AdminScalarAsync<string>("SELECT status FROM fixes.fix_groups WHERE id = $1", shop.GroupId));
        Assert.Equal("proposed", await AdminScalarAsync<string>("SELECT status FROM checks.findings WHERE id = $1", shop.FindingId));
    }

    [Fact]
    public async Task Unapprove_BeforePublication_ReturnsTheProposals_AfterItIsRefused()
    {
        var (factory, owner, shop) = await ShopAsync(filled: true);
        await using var _ = factory;
        using var __ = owner;
        var path = $"{S(owner, shop.ShopId)}/fix-groups/{shop.GroupId}";
        using (var approved = await SendAsync(owner, HttpMethod.Post, path + "/approve", null, await ETagAsync(owner, path)))
        {
            Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        }

        using (var back = await SendAsync(owner, HttpMethod.Post, path + "/unapprove", null, await ETagAsync(owner, path)))
        {
            var body = await ApiClient.JsonAsync(back);
            Assert.Equal(HttpStatusCode.OK, back.StatusCode);
            Assert.Equal("draft", body.GetProperty("status").GetString());
        }

        Assert.Equal(36L, await AdminScalarAsync<long>("SELECT count(*) FROM fixes.fix_proposals WHERE group_id = $1 AND status = 'proposed'", shop.GroupId));
        Assert.Equal(0L, await AdminScalarAsync<long>(
            "SELECT count(*) FROM fixes.decision_memory WHERE shop_id = $1 AND segment_hash = $2 AND superseded_at IS NULL", shop.ShopId, Hash));

        using (var again = await SendAsync(owner, HttpMethod.Post, path + "/approve", null, await ETagAsync(owner, path)))
        {
            Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        }

        await AdminAsync("UPDATE fixes.fix_proposals SET status = 'published' WHERE id = (SELECT id FROM fixes.fix_proposals WHERE group_id = $1 LIMIT 1)", shop.GroupId);
        using var refused = await SendAsync(owner, HttpMethod.Post, path + "/unapprove", null, await ETagAsync(owner, path));

        await ProblemAsync(refused, HttpStatusCode.Conflict, "group.already_published");
        Assert.Equal(35L, await AdminScalarAsync<long>("SELECT count(*) FROM fixes.fix_proposals WHERE group_id = $1 AND status = 'accepted'", shop.GroupId));
    }

    [Fact]
    public async Task List_CountsTheGroupsAndPages_AndAStaleVersionIsAConflict()
    {
        var (factory, owner, shop) = await ShopAsync();
        await using var _ = factory;
        using var __ = owner;
        var path = $"{S(owner, shop.ShopId)}/fix-groups/{shop.GroupId}";

        using (var list = await owner.Browser.GetAsync($"{S(owner, shop.ShopId)}/fix-groups?status=needs_value"))
        {
            var body = await ApiClient.JsonAsync(list);
            var group = Assert.Single(body.GetProperty("groups").EnumerateArray());
            Assert.Equal((38, 36, 2, 1), (group.GetProperty("pageCount").GetInt32(), group.GetProperty("fitsCount").GetInt32(),
                group.GetProperty("individualCount").GetInt32(), group.GetProperty("missingValues").GetInt32()));
            Assert.Equal((1, 38), (body.GetProperty("totals").GetProperty("groups").GetInt32(), body.GetProperty("totals").GetProperty("pages").GetInt32()));
        }

        using (var none = await owner.Browser.GetAsync($"{S(owner, shop.ShopId)}/fix-groups?status=approved"))
        {
            Assert.Empty((await ApiClient.JsonAsync(none)).GetProperty("groups").EnumerateArray());
        }

        var stale = await ETagAsync(owner, path);
        using (var first = await SendAsync(owner, HttpMethod.Put, path + "/mode", new { mode = "remove" }, stale))
        {
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        }

        using var second = await SendAsync(owner, HttpMethod.Put, path + "/mode", new { mode = "replace" }, stale);

        await ProblemAsync(second, HttpStatusCode.Conflict, "concurrency.conflict");
    }

    private sealed record GroupShop(Guid ShopId, Guid GroupId, Guid FindingId, Guid[] Pages, Guid[] Individual);

    /// <summary>
    /// An active e-shop with 38 pages carrying the sentence (each followed by another block), one finding of the segment with 38
    /// occurrences (one row per sentence, change 8) and its group (<c>fit.individual</c>: the first 2 pages, as the grouping marked them).
    /// </summary>
    private static async Task<(ApiFactory Factory, Person Owner, GroupShop Shop)> ShopAsync(bool filled = false)
    {
        var factory = Factory();
        var owner = await People.OwnerAsync(factory);
        var shopId = (await CreateShopAsync(owner)).GetProperty("id").GetGuid();
        await ExecuteAsync("UPDATE shop.shops SET status = 'active' WHERE id = $1", shopId);
        var seed = new ShopSeed(owner.TenantId, shopId, factory.Services.GetRequiredService<IBlobStore>());
        await seed.RunAsync();
        var eco = await RuleSetAsync("eco", "eco");
        var pages = new List<Guid>();
        for (var i = 1; i <= 38; i++)
        {
            var text = string.Join('\n', $"Bylinný produkt {i:00}", "Ručne zbierané bylinky z Liptova.", Sentence, "Okamžite po objednávke expedujeme.");
            pages.Add((await seed.PageAsync($"Bylinný produkt {i:00}", $"/produkt-{i:00}/", "sk", text)).PageId);
        }

        var finding = await seed.FindingAsync(eco, "eco_generic_claim", "eco", Verdicts(Verdict("sk", "text", "high")), "open", Sentence, Hash, pages);
        var individual = pages.Take(2).ToArray();
        var fit = JsonSerializer.Serialize(new { individual = individual.Select(p => new { page_id = p, reason_code = "sentence_in_paragraph" }) });
        var group = await seed.GroupAsync("repeated_text", Hash, Sentence, "Všetky naše produkty balíme do [materiál obalu].", 38,
            placeholders: """["materiál obalu"]""", status: filled ? "draft" : "needs_value", fit: fit,
            filledValues: filled ? JsonSerializer.Serialize(new Dictionary<string, string> { ["materiál obalu"] = Fact }) : null, recheck: filled ? "ok" : null);
        return (factory, owner, new GroupShop(shopId, group, finding, [.. pages], individual));
    }

    private static Task RecheckOkAsync(Guid groupId) => AdminAsync(
        "UPDATE fixes.fix_groups SET recheck_status = 'ok', recheck_result = '{\"jurisdictions\":{\"sk\":\"ok\"}}'::jsonb WHERE id = $1", groupId);
}
