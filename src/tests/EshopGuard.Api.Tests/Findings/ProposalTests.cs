using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace EshopGuard.Api.Tests.Findings;

/// <summary>Proposals of fixes (change 11, task 4.9; AD 5): own wording, recheck, acceptance only after the recheck in every country.</summary>
public sealed class ProposalTests : FindingsTestBase
{
    [Fact]
    public async Task Edit_Is202_QueuesTheRecheck_AndAcceptancePassesAfterAnOkInBothCountries()
    {
        var (factory, owner, data) = await BylinkovoAsync();
        await using var _ = factory;
        using var __ = owner;
        var id = data.Proposals["zubna.3"];
        var path = $"{S(owner, data.ShopId)}/proposals/{id}";

        using var edited = await SendAsync(owner, HttpMethod.Put, path + "/text", new { text = "Bambusová kefka – rukoväť z bambusu namiesto plastu." }, await ETagAsync(owner, path));

        Assert.Equal(HttpStatusCode.Accepted, edited.StatusCode);
        var body = await ApiClient.JsonAsync(edited);
        Assert.Equal("pending", body.GetProperty("recheck").GetProperty("status").GetString());
        Assert.Equal("edited", body.GetProperty("status").GetString());
        Assert.Equal((short)0, await AdminScalarAsync<short>("SELECT priority FROM ops.jobs WHERE kind = 'fix.recheck' AND shop_id = $1", data.ShopId));

        using (var early = await SendAsync(owner, HttpMethod.Post, path + "/accept", null, await ETagAsync(owner, path)))
        {
            await ProblemAsync(early, HttpStatusCode.Conflict, "proposal.recheck_pending");
        }

        await RecheckAsync(id, "ok", """{"sk":"ok","cz":"ok"}""");
        using var accepted = await SendAsync(owner, HttpMethod.Post, path + "/accept", null, await ETagAsync(owner, path));

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal("accepted", (await ApiClient.JsonAsync(accepted)).GetProperty("status").GetString());
        Assert.Equal("approved", await AdminScalarAsync<string>("SELECT status FROM checks.findings WHERE id = $1", data.Findings["zubna.3"]));
        Assert.Equal(1L, await AdminScalarAsync<long>(
            "SELECT count(*) FROM fixes.decision_memory WHERE source_proposal_id = $1 AND decision = 'replace' AND superseded_at IS NULL", id));
    }

    [Fact]
    public async Task StillFindingInCzech_IsRefused_WithTheCountryAndRules()
    {
        var (factory, owner, data) = await BylinkovoAsync();
        await using var _ = factory;
        using var __ = owner;
        var id = data.Proposals["zubna.3"];
        var path = $"{S(owner, data.ShopId)}/proposals/{id}";
        using (var edited = await SendAsync(owner, HttpMethod.Put, path + "/text", new { text = "Ekologická bambusová kefka." }, await ETagAsync(owner, path)))
        {
            Assert.Equal(HttpStatusCode.Accepted, edited.StatusCode);
        }

        await RecheckAsync(id, "still_finding", """{"sk":"ok","cz":"still_finding"}""", """["eco_generic_claim"]""");
        using var accepted = await SendAsync(owner, HttpMethod.Post, path + "/accept", null, await ETagAsync(owner, path));

        var problem = await ProblemAsync(accepted, HttpStatusCode.Conflict, "proposal.recheck_failed");
        Assert.Equal(["cz"], problem.GetProperty("params").GetProperty("jurisdictions").EnumerateArray().Select(j => j.GetString()));
        Assert.Equal("eco_generic_claim", problem.GetProperty("params").GetProperty("ruleIds")[0].GetString());
        Assert.Equal("edited", await AdminScalarAsync<string>("SELECT status FROM fixes.fix_proposals WHERE id = $1", id));
    }

    [Fact]
    public async Task MissingFact_IsRefused()
    {
        var (factory, owner, data) = await BylinkovoAsync();
        await using var _ = factory;
        using var __ = owner;
        var path = $"{S(owner, data.ShopId)}/proposals/{data.Proposals["group"]}";

        using var accepted = await SendAsync(owner, HttpMethod.Post, path + "/accept", null, await ETagAsync(owner, path));

        var problem = await ProblemAsync(accepted, HttpStatusCode.Conflict, "proposal.placeholder_missing");
        Assert.Equal("materiál obalu", problem.GetProperty("params").GetProperty("keys")[0].GetString());
    }

    [Fact]
    public async Task FillingTheFact_ChangesTheText_AndQueuesTheRecheck()
    {
        var (factory, owner, data) = await BylinkovoAsync();
        await using var _ = factory;
        using var __ = owner;
        var path = $"{S(owner, data.ShopId)}/proposals/{data.Proposals["group"]}";

        using var filled = await SendAsync(owner, HttpMethod.Put, path + "/placeholders",
            new { values = new Dictionary<string, string> { ["materiál obalu"] = "papierovej krabice bez plastovej výplne" } }, await ETagAsync(owner, path));

        Assert.Equal(HttpStatusCode.Accepted, filled.StatusCode);
        var body = await ApiClient.JsonAsync(filled);
        Assert.Equal("Všetky naše produkty balíme do papierovej krabice bez plastovej výplne.", body.GetProperty("text").GetString());
        Assert.Equal("pending", body.GetProperty("recheck").GetProperty("status").GetString());
    }

    [Fact]
    public async Task ConcurrentEdits_TheSecondIsAConflict()
    {
        var (factory, owner, data) = await BylinkovoAsync();
        await using var _ = factory;
        using var __ = owner;
        var path = $"{S(owner, data.ShopId)}/proposals/{data.Proposals["zubna.5"]}";
        var etag = await ETagAsync(owner, path);

        using var first = await SendAsync(owner, HttpMethod.Put, path + "/text", new { text = "Set pre zákazníkov, ktorí chcú kefku bez plastu." }, etag);
        using var second = await SendAsync(owner, HttpMethod.Put, path + "/text", new { text = "Iné znenie." }, etag);

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        await ProblemAsync(second, HttpStatusCode.Conflict, "concurrency.conflict");
    }

    [Fact]
    public async Task ChangeWithoutIfMatch_IsAValidationError()
    {
        var (factory, owner, data) = await BylinkovoAsync();
        await using var _ = factory;
        using var __ = owner;

        using var response = await owner.Browser.PostAsync($"{S(owner, data.ShopId)}/proposals/{data.Proposals["zubna.1"]}/accept");

        var problem = await ProblemAsync(response, HttpStatusCode.BadRequest, "validation.failed");
        Assert.True(problem.GetProperty("errors").TryGetProperty("If-Match", out var _));
    }

    [Fact]
    public async Task Variant_Reject_And_TakingAnAcceptanceBack()
    {
        var (factory, owner, data) = await BylinkovoAsync();
        await using var _ = factory;
        using var __ = owner;
        var path = $"{S(owner, data.ShopId)}/proposals/{data.Proposals["zubna.1"]}";

        using (var variant = await SendAsync(owner, HttpMethod.Put, path + "/alternative", new { key = "without_word" }, await ETagAsync(owner, path)))
        {
            Assert.Equal(HttpStatusCode.OK, variant.StatusCode);
            Assert.Equal("Okamžitý komfort pre citlivé zuby.", (await ApiClient.JsonAsync(variant)).GetProperty("text").GetString());
        }

        using (var unknown = await SendAsync(owner, HttpMethod.Put, path + "/alternative", new { key = "nonsense" }, await ETagAsync(owner, path)))
        {
            await ProblemAsync(unknown, HttpStatusCode.BadRequest, "proposal.alternative_unknown");
        }

        using (var accepted = await SendAsync(owner, HttpMethod.Post, path + "/accept", null, await ETagAsync(owner, path)))
        {
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        }

        using (var back = await SendAsync(owner, HttpMethod.Post, path + "/unaccept", null, await ETagAsync(owner, path)))
        {
            Assert.Equal(HttpStatusCode.OK, back.StatusCode);
            Assert.Equal("proposed", (await ApiClient.JsonAsync(back)).GetProperty("status").GetString());
        }

        Assert.Equal("proposed", await AdminScalarAsync<string>("SELECT status FROM checks.findings WHERE id = $1", data.Findings["zubna.1"]));
        Assert.Equal(1L, await AdminScalarAsync<long>("SELECT count(*) FROM fixes.decision_memory WHERE source_proposal_id = $1 AND superseded_at IS NOT NULL", data.Proposals["zubna.1"]));

        using var rejected = await SendAsync(owner, HttpMethod.Post, path + "/reject", null, await ETagAsync(owner, path));
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        Assert.Equal("open", await AdminScalarAsync<string>("SELECT status FROM checks.findings WHERE id = $1", data.Findings["zubna.1"]));
    }

    internal static async Task<EntityTagHeaderValue> ETagAsync(Person person, string path)
    {
        using var response = await person.Browser.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return response.Headers.ETag!;
    }

    internal static async Task<HttpResponseMessage> SendAsync(Person person, HttpMethod method, string path, object? body, EntityTagHeaderValue? etag)
    {
        var csrf = await person.Browser.CsrfAsync();
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-CSRF-TOKEN", csrf);
        if (etag is not null)
        {
            request.Headers.IfMatch.Add(etag);
        }

        if (body is not null)
        {
            request.Content = JsonContent(body);
        }

        return await person.Browser.Http.SendAsync(request, Ct);
    }

    private static StringContent JsonContent(object body) =>
        new(JsonSerializer.Serialize(body, new JsonSerializerOptions(JsonSerializerDefaults.Web)), System.Text.Encoding.UTF8, "application/json");

    /// <summary>What <c>fix.recheck</c> writes (its own test runs it with the Jev of the tests).</summary>
    private static Task RecheckAsync(Guid proposalId, string status, string jurisdictions, string rules = "[]") => AdminAsync(
        "UPDATE fixes.fix_proposals SET recheck_status = $2, recheck_result = jsonb_build_object('jurisdictions', $3::jsonb, 'rule_ids', $4::jsonb) WHERE id = $1",
        proposalId, status, jurisdictions, rules);
}
