using System.Net;
using EshopGuard.Api.Tests.Fakes;
using EshopGuard.Application.Fixes;
using Microsoft.Extensions.DependencyInjection;
using static EshopGuard.Api.Tests.Findings.ProposalTests;
using static EshopGuard.Api.Tests.Findings.ShopSeed;

namespace EshopGuard.Api.Tests.Findings;

/// <summary>
/// Publication through the connector and „Kopírovať text“ (change 11, task 8.5; AD 8): queued for change 15 with the job
/// <c>publish.fix</c>, idempotent, refused without a connector or a publisher, a template only to copy.
/// </summary>
public sealed class PublicationTests : FindingsTestBase
{
    private const string Description = "Ekologický šampón pre celú rodinu. Bez parabénov a silikónov.";
    private const string Fixed = "Šampón pre celú rodinu. Bez parabénov a silikónov.";

    [Fact]
    public async Task Shoptet_IsQueued_WithAJobP1_AndAnEqualRequestReturnsTheSamePublication()
    {
        var (factory, owner, data, page) = await ShopAsync();
        await using var _ = factory;
        using var __ = owner;
        var path = $"{S(owner, data.ShopId)}/publications";

        using var first = await SendAsync(owner, HttpMethod.Post, path, new { pageIds = new[] { page } }, null);

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        var body = await ApiClient.JsonAsync(first);
        var publication = Assert.Single(body.GetProperty("publications").EnumerateArray());
        Assert.Equal(("queued", "description"), (publication.GetProperty("status").GetString(), publication.GetProperty("field").GetString()));
        Assert.Empty(body.GetProperty("skipped").EnumerateArray());
        var id = publication.GetProperty("id").GetGuid();
        var job = Assert.Single(await AdminRowsAsync(
            "SELECT priority, resource_class, concurrency_key FROM ops.jobs WHERE kind = 'publish.fix' AND shop_id = $1", data.ShopId));
        Assert.Equal(((short)1, "io", $"connector:{data.ShopId:D}"), ((short)job[0]!, (string)job[1]!, (string)job[2]!));

        using var again = await SendAsync(owner, HttpMethod.Post, path, new { pageIds = new[] { page } }, null);

        Assert.Equal(id, (await ApiClient.JsonAsync(again)).GetProperty("publications")[0].GetProperty("id").GetGuid());
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM fixes.publications WHERE shop_id = $1", data.ShopId));
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.jobs WHERE kind = 'publish.fix' AND shop_id = $1", data.ShopId));
        using var detail = await owner.Browser.GetAsync($"{path}/{id}");
        Assert.Equal(Fixed, (await ApiClient.JsonAsync(detail)).GetProperty("newValue").GetString());
        using var list = await owner.Browser.GetAsync($"{path}?status=queued");
        var item = Assert.Single((await ApiClient.JsonAsync(list)).GetProperty("items").EnumerateArray());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, item.GetProperty("newValue").ValueKind);
        // Every request is audited, the equal one too.
        Assert.Equal(2L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.audit_log WHERE tenant_id = $1 AND action = 'publication.requested'", owner.TenantId));
    }

    [Fact]
    public async Task WebShop_CannotPublish_ButTheTextCanBeCopied()
    {
        var (factory, owner, data, page) = await ShopAsync();
        await using var _ = factory;
        using var __ = owner;
        await AdminAsync("UPDATE shop.shops SET source_mode = 'web' WHERE id = $1", data.ShopId);

        using var published = await SendAsync(owner, HttpMethod.Post, $"{S(owner, data.ShopId)}/publications", new { pageIds = new[] { page } }, null);
        var problem = await ProblemAsync(published, HttpStatusCode.Conflict, "publication.not_available");
        Assert.Equal("no_connector", problem.GetProperty("params").GetProperty("reason").GetString());
        Assert.Equal(0L, await AdminScalarAsync<long>("SELECT count(*) FROM fixes.publications WHERE shop_id = $1", data.ShopId));

        using var text = await owner.Browser.GetAsync($"{S(owner, data.ShopId)}/pages/{page}/fixed-text?field=description");
        var body = await ApiClient.JsonAsync(text);
        Assert.Equal(HttpStatusCode.OK, text.StatusCode);
        Assert.Equal(Fixed, body.GetProperty("text").GetString());
        Assert.Single(body.GetProperty("appliedProposalIds").EnumerateArray());

        using var none = await owner.Browser.GetAsync($"{S(owner, data.ShopId)}/pages/{page}/fixed-text?field=name");
        await ProblemAsync(none, HttpStatusCode.Conflict, "page.no_accepted_changes");
    }

    [Fact]
    public async Task Template_IsOnlyToCopy()
    {
        var (factory, owner, data, _) = await ShopAsync();
        await using var _f = factory;
        using var __ = owner;
        var cart = data.Pages["approve.1"];
        await data.Seed.RunAsync();
        await data.Seed.ProposalAsync(cart, [], "Ekologický obchod s prírodnou kozmetikou", "Obchod s prírodnou kozmetikou", null, status: "accepted",
            groupId: data.Groups["template"]);

        using var published = await SendAsync(owner, HttpMethod.Post, $"{S(owner, data.ShopId)}/publications", new { groupId = data.Groups["template"] }, null);

        Assert.Equal(HttpStatusCode.Accepted, published.StatusCode);
        var body = await ApiClient.JsonAsync(published);
        Assert.Empty(body.GetProperty("publications").EnumerateArray());
        var skipped = Assert.Single(body.GetProperty("skipped").EnumerateArray());
        Assert.Equal((cart.PageId, "copy_only"), (skipped.GetProperty("pageId").GetGuid(), skipped.GetProperty("reasonCode").GetString()));
        Assert.Equal(0L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.jobs WHERE kind = 'publish.fix' AND shop_id = $1", data.ShopId));
    }

    [Fact]
    public async Task ChangeNoLongerInThePage_IsNotPublished()
    {
        var (factory, owner, data, page) = await ShopAsync();
        await using var _ = factory;
        using var __ = owner;
        var version = await AdminScalarAsync<Guid>("SELECT current_version_id FROM content.pages WHERE id = $1", page);
        await data.Seed.RunAsync();
        await data.Seed.ProposalAsync((page, version), [], "Hypoalergénny šampón.", "Šampón.", null, status: "accepted", field: "description");

        using var published = await SendAsync(owner, HttpMethod.Post, $"{S(owner, data.ShopId)}/publications", new { pageIds = new[] { page } }, null);

        var skipped = Assert.Single((await ApiClient.JsonAsync(published)).GetProperty("skipped").EnumerateArray());
        Assert.Equal("not_located", skipped.GetProperty("reasonCode").GetString());
        Assert.Equal(0L, await AdminScalarAsync<long>("SELECT count(*) FROM fixes.publications WHERE shop_id = $1", data.ShopId));
    }

    [Fact]
    public async Task Viewer_CannotPublish_AndAQueuedPublicationCannotBeRolledBack()
    {
        var (factory, owner, data, page) = await ShopAsync();
        await using var _ = factory;
        using var __ = owner;
        using var viewer = await People.MemberAsync(factory, owner, "viewer");
        using (var refused = await SendAsync(viewer, HttpMethod.Post, $"{S(owner, data.ShopId)}/publications", new { pageIds = new[] { page } }, null))
        {
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        }

        using var published = await SendAsync(owner, HttpMethod.Post, $"{S(owner, data.ShopId)}/publications", new { pageIds = new[] { page } }, null);
        var id = (await ApiClient.JsonAsync(published)).GetProperty("publications")[0].GetProperty("id").GetGuid();

        using var rollback = await SendAsync(owner, HttpMethod.Post, $"{S(owner, data.ShopId)}/publications/{id}/rollback", null, null);
        var problem = await ProblemAsync(rollback, HttpStatusCode.Conflict, "publication.not_rollbackable");
        Assert.Equal("queued", problem.GetProperty("params").GetProperty("status").GetString());

        await AdminAsync("UPDATE fixes.publications SET status = 'published', published_at = now() WHERE id = $1", id);
        using var back = await SendAsync(owner, HttpMethod.Post, $"{S(owner, data.ShopId)}/publications/{id}/rollback", null, null);
        Assert.Equal(HttpStatusCode.Accepted, back.StatusCode);
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.jobs WHERE kind = 'publish.rollback' AND shop_id = $1 AND priority = 1", data.ShopId));
    }

    [Fact]
    public async Task WithoutAPublisher_OrVerifiedOwnership_NothingIsQueued()
    {
        var factory = Factory();
        await using var _ = factory;
        using var owner = await People.OwnerAsync(factory);
        var data = await BylinkovoSeed.SeedAsync(factory, owner);
        await ExecuteAsync("UPDATE shop.shops SET ownership_verified_at = now(), verification_method = 'connector' WHERE id = $1", data.ShopId);
        using (var missing = await SendAsync(owner, HttpMethod.Post, $"{S(owner, data.ShopId)}/publications", new { pageIds = new[] { data.Pages["zubna"].PageId } }, null))
        {
            await ProblemAsync(missing, HttpStatusCode.Conflict, "publication.connector_unavailable");
        }

        var (withPublisher, other, otherData, page) = await ShopAsync(verified: false);
        await using var __ = withPublisher;
        using var ___ = other;
        using var unverified = await SendAsync(other, HttpMethod.Post, $"{S(other, otherData.ShopId)}/publications", new { pageIds = new[] { page } }, null);

        var problem = await ProblemAsync(unverified, HttpStatusCode.Conflict, "publication.not_available");
        Assert.Equal("ownership_not_verified", problem.GetProperty("params").GetProperty("reason").GetString());
        Assert.Equal(0L, await AdminScalarAsync<long>("SELECT count(*) FROM ops.jobs WHERE kind = 'publish.fix' AND shop_id = ANY($1)", new[] { data.ShopId, otherData.ShopId }));
    }

    /// <summary>„bylinkovo.sk“ (Shoptet, connector <c>read_write</c>) with <see cref="FakeFixPublisher"/> and a page whose description has one accepted change.</summary>
    private static async Task<(ApiFactory Factory, Person Owner, BylinkovoSeed Data, Guid PageId)> ShopAsync(bool verified = true)
    {
        var factory = Factory(services: s => s.AddSingleton<IFixPublisher, FakeFixPublisher>());
        var owner = await People.OwnerAsync(factory);
        var data = await BylinkovoSeed.SeedAsync(factory, owner);
        if (verified)
        {
            await ExecuteAsync("UPDATE shop.shops SET ownership_verified_at = now(), verification_method = 'connector' WHERE id = $1", data.ShopId);
        }

        var page = await data.Seed.PageAsync("Bylinkový šampón", "/bylinkovy-sampon/", "sk", "Bylinkový šampón\nS výťažkom z rozmarínu.", source: "connector",
            externalId: "2431", description: Description);
        var eco = await RuleSetAsync("eco", "eco");
        var finding = await data.Seed.FindingAsync(eco, "eco_generic_claim", "eco", Verdicts(Verdict("sk", "text", "high")), "approved",
            "Ekologický šampón pre celú rodinu.", 7_301, [page.PageId]);
        await data.Seed.ProposalAsync(page, [finding], "Ekologický šampón pre celú rodinu.", "Šampón pre celú rodinu.", null, status: "accepted", field: "description");
        // One proposal per field and run (the worker's key): the one not decided yet comes from the next run.
        await data.Seed.RunAsync();
        await data.Seed.ProposalAsync(page, [finding], "Bez parabénov a silikónov.", "Bez parabénov.", null, status: "proposed", field: "description");
        return (factory, owner, data, page.PageId);
    }
}
