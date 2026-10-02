using System.Net;
using System.Text.Json;

namespace EshopGuard.Api.Tests.Shops;

/// <summary>The result of a sample over the data of change 8 (change 10, task 4.6; specification „Stav a výsledky ukázky“).</summary>
public sealed class SampleResultTests : ShopTestBase
{
    [Fact]
    public async Task FinishedSample_HasCountsTopFindingsAsCodesAndTheExampleFix()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        var domain = NewDomain("bylinkovo");
        var shopId = (await CreateShopAsync(owner, "https://" + domain)).GetProperty("id").GetGuid();
        var runId = await SeedBylinkovoAsync(owner.TenantId, shopId, domain);
        var (findings, proposal) = await SeedFindingsAsync(owner.TenantId, shopId, runId, domain, recheck: "ok");

        var sample = await GetJsonAsync(owner, $"/api/t/{owner.TenantId}/shops/{shopId}/sample");

        Assert.Equal("finished", sample.GetProperty("status").GetString());
        var result = sample.GetProperty("result");
        Assert.Equal(100, result.GetProperty("pagesChecked").GetInt32());
        var counts = result.GetProperty("findingCounts");
        Assert.Equal(11, counts.GetProperty("total").GetInt32());
        Assert.Equal(4, counts.GetProperty("byCheckability").GetProperty("text").GetInt32());
        Assert.Equal(3, counts.GetProperty("bySeverity").GetProperty("high").GetInt32());
        var top = result.GetProperty("topFindings").EnumerateArray().ToList();
        Assert.Equal(findings, top.Select(f => f.GetProperty("findingId").GetGuid()));
        Assert.All(top, f =>
        {
            Assert.StartsWith("rule_", f.GetProperty("ruleId").GetString(), StringComparison.Ordinal);
            Assert.Equal(JsonValueKind.Object, f.GetProperty("params").ValueKind);
            Assert.Equal(2, f.GetProperty("verdicts").GetArrayLength());
            Assert.Equal("sk", f.GetProperty("strictest").GetProperty("jurisdiction").GetString());
            Assert.False(f.TryGetProperty("explanation", out _));
        });
        Assert.Equal(["cs", "sk"], result.GetProperty("versions").EnumerateArray().Select(v => v.GetString()));
        Assert.Equal(["sk", "cz"], result.GetProperty("jurisdictions").EnumerateArray().Select(v => v.GetString()));
        var fix = result.GetProperty("exampleFix");
        Assert.Equal(proposal, fix.GetProperty("proposalId").GetGuid());
        Assert.Equal("ok", fix.GetProperty("recheckStatus").GetString());
        Assert.Equal("Pôvodný text", fix.GetProperty("originalText").GetString());
    }

    [Fact]
    public async Task ExampleThatDidNotPassTheRecheck_IsNotShown()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        var domain = NewDomain();
        var shopId = (await CreateShopAsync(owner, "https://" + domain)).GetProperty("id").GetGuid();
        var runId = await SeedBylinkovoAsync(owner.TenantId, shopId, domain);
        await SeedFindingsAsync(owner.TenantId, shopId, runId, domain, recheck: "still_finding");

        var result = (await GetJsonAsync(owner, $"/api/t/{owner.TenantId}/shops/{shopId}/sample")).GetProperty("result");

        Assert.Equal(JsonValueKind.Null, result.GetProperty("exampleFix").ValueKind);
        Assert.Equal("recheck_not_ok", result.GetProperty("exampleFixMissingReason").GetString());
    }

    [Fact]
    public async Task PartialSample_ListsWhatWasNotChecked()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        var domain = NewDomain();
        var shopId = (await CreateShopAsync(owner, "https://" + domain)).GetProperty("id").GetGuid();
        await SeedBylinkovoAsync(owner.TenantId, shopId, domain, runStatus: "partial");

        var sample = await GetJsonAsync(owner, $"/api/t/{owner.TenantId}/shops/{shopId}/sample");

        Assert.Equal("partial", sample.GetProperty("status").GetString());
        var notChecked = sample.GetProperty("result").GetProperty("notChecked");
        Assert.Equal(7, notChecked.GetProperty("robotsBlocked").GetInt32());
        Assert.Equal(3, notChecked.GetProperty("textNotLoaded").GetInt32());
        Assert.Equal(40, notChecked.GetProperty("other").GetInt32());
        Assert.Equal(40, notChecked.GetProperty("byReason").GetProperty("over_limit").GetInt32());
    }

    [Fact]
    public async Task FailedSample_SaysWhy_AndHasNoResult()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        var shopId = (await CreateShopAsync(owner)).GetProperty("id").GetGuid();
        var runId = await SeedRunAsync(owner.TenantId, shopId, "free_sample", "failed");
        await AdminAsync("UPDATE checks.runs SET error = 'sample_budget_exceeded' WHERE id = $1", runId);

        var sample = await GetJsonAsync(owner, $"/api/t/{owner.TenantId}/shops/{shopId}/sample");

        Assert.Equal("failed", sample.GetProperty("status").GetString());
        Assert.Equal("sample_budget_exceeded", sample.GetProperty("error").GetString());
        Assert.Equal(JsonValueKind.Null, sample.GetProperty("result").ValueKind);
    }

    [Fact]
    public async Task RunningSample_HasProgressAndNoResult()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        var domain = NewDomain();
        var shopId = (await CreateShopAsync(owner, "https://" + domain)).GetProperty("id").GetGuid();
        await SeedBylinkovoAsync(owner.TenantId, shopId, domain, runStatus: "crawling");

        var sample = await GetJsonAsync(owner, $"/api/t/{owner.TenantId}/shops/{shopId}/sample");

        Assert.Equal("crawling", sample.GetProperty("status").GetString());
        Assert.Equal(100, sample.GetProperty("progress").GetProperty("pagesFetched").GetInt64());
        Assert.Equal(JsonValueKind.Null, sample.GetProperty("result").ValueKind);
    }

    /// <summary>5 findings in the order of strictness, a page, a version and a proposal of a fix; returns the ids.</summary>
    private static async Task<(List<Guid> Findings, Guid Proposal)> SeedFindingsAsync(Guid tenantId, Guid shopId, Guid runId, string domain, string recheck)
    {
        var ruleSet = Guid.CreateVersion7();
        await AdminAsync(
            "INSERT INTO checks.rule_sets (id, module, version, jurisdictions, question_language, question_set_hash, definition, texts, source_hash, enabled, created_at, updated_at) " +
            "VALUES ($1, 'legal', $2, '{sk,cz}', 'en', '\\x00', '{}'::jsonb, '{}'::jsonb, '\\x00', true, now(), now())", ruleSet, "test-" + ruleSet.ToString("N"));
        var page = Guid.CreateVersion7();
        await AdminAsync(
            "INSERT INTO content.pages (id, tenant_id, shop_id, url, url_hash, language, title, source, status, first_seen_at, rotation_bucket, created_at, updated_at) " +
            "VALUES ($1, $2, $3, $4, 1, 'sk', 'Bylinný čaj', 'crawl', 'active', now(), 0, now(), now())", page, tenantId, shopId, $"https://{domain}/caj");
        var version = Guid.CreateVersion7();
        await AdminAsync(
            "INSERT INTO content.page_versions (id, tenant_id, shop_id, page_id, fetched_at, created_at, updated_at) VALUES ($1, $2, $3, $4, now(), now(), now())",
            version, tenantId, shopId, page);
        var ids = new List<Guid>();
        for (var i = 0; i < 5; i++)
        {
            var id = Guid.CreateVersion7();
            ids.Add(id);
            await AdminAsync(
                """
                INSERT INTO checks.findings (id, tenant_id, shop_id, rule_id, rule_set_id, module, checkability, severity, band, scope, page_id, text, verdicts, legal_refs,
                    params, status, occurrences, first_run_id, created_at, updated_at)
                VALUES ($1, $2, $3, $4, $5, 'legal', 'text', 'high', 'high', 'segment', $6, 'Najlepší čaj na svete',
                    '[{"jurisdiction":"sk","checkability":"text","severity":"high","band":"high","legal_refs":[]},{"jurisdiction":"cz","checkability":"assess","severity":"medium","band":"review","legal_refs":[]}]'::jsonb,
                    '[]'::jsonb, '{"claim":"najlepší"}'::jsonb, 'open', 1, $7, now(), now())
                """, id, tenantId, shopId, $"rule_{i}", ruleSet, page, runId);
        }

        var proposal = Guid.CreateVersion7();
        await AdminAsync(
            """
            INSERT INTO fixes.fix_proposals (id, tenant_id, shop_id, page_id, page_version_id, finding_ids, field, original_text, proposed_text, recheck_status, status,
                created_run_id, created_at, updated_at)
            VALUES ($1, $2, $3, $4, $5, ARRAY[$6]::uuid[], 'description', 'Pôvodný text', 'Navrhnutý text', $7, 'proposed', $8, now(), now())
            """, proposal, tenantId, shopId, page, version, ids[0], recheck, runId);
        var top = string.Join(",", ids.Select(i => $"\"{i:D}\""));
        await AdminAsync(
            $$"""UPDATE checks.runs SET stats = jsonb_set(jsonb_set(stats, '{sample,top_finding_ids}', '[{{top}}]'::jsonb), '{sample,example_fix_proposal_id}', to_jsonb($2::text)) WHERE id = $1""",
            runId, proposal.ToString("D"));
        return (ids, proposal);
    }
}
