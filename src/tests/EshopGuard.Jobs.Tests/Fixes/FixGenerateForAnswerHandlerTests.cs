using EshopGuard.Data;
using EshopGuard.Data.Tenancy;
using EshopGuard.Jobs.Fixes;
using EshopGuard.Jobs.Queue;
using EshopGuard.Jobs.Tests.Runs.Support;
using EshopGuard.Storage;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace EshopGuard.Jobs.Tests.Fixes;

/// <summary>
/// The job <c>fix.generate_for_answer</c> (change 11, task 5.7; AD 6) with the mock rewrite (it drops generic environmental
/// words) and the deterministic Jev of the tests: a text without the claim becomes the selected variant <c>answer_no</c> and
/// the finding <c>proposed</c>; a text the rules still find stays a variant with the failed check and the finding
/// <c>open</c>; a changed answer makes the job a no-op.
/// </summary>
public sealed class FixGenerateForAnswerHandlerTests(JobsTestDatabase database) : JobsTestBase(database)
{
    [Fact]
    public async Task VariantPassesTheCheck_IsSelected_AndTheFindingIsProposed()
    {
        var seed = await SeedAsync("Ekologická sviečka z vosku", withProposal: true);

        await RunAsync(seed);

        var proposal = await RunTests.RowsAsync(Db, seed.Shop.TenantId, AnswerNoSql, seed.ProposalId!.Value);
        Assert.Equal(["answer_no", "sviečka z vosku", "ok", "proposed"], proposal[0]);
        Assert.Equal("proposed", await StatusAsync(seed));
        Assert.Equal(1L, await RunTests.ScalarAsync<long>(Db, seed.Shop.TenantId,
            "SELECT count(*) FROM usage.usage_records WHERE shop_id = $1 AND provider = 'openai' AND operation = 'rewrite' AND run_id IS NULL", seed.Shop.ShopId));
        Assert.Equal(1L, await RunTests.ScalarAsync<long>(Db, seed.Shop.TenantId,
            "SELECT count(*) FROM ops.audit_log WHERE tenant_id = $1 AND action = 'finding.status_changed' AND entity_id = $2 AND actor_kind = 'system'",
            seed.Shop.TenantId, seed.FindingId.ToString("D")));
    }

    [Fact]
    public async Task FindingWithoutAProposal_GetsOne()
    {
        var seed = await SeedAsync("Ekologická sviečka z vosku", withProposal: false);

        await RunAsync(seed);

        var rows = await RunTests.RowsAsync(Db, seed.Shop.TenantId,
            "SELECT original_text, proposed_text, block_index, selected_alternative, recheck_status FROM fixes.fix_proposals WHERE $1 = ANY(finding_ids)", seed.FindingId);
        var row = Assert.Single(rows);
        Assert.Equal(["Ekologická sviečka z vosku", "sviečka z vosku", 2, "answer_no", "ok"], row);
        Assert.Equal("proposed", await StatusAsync(seed));
    }

    [Fact]
    public async Task TextTheRulesStillFind_StaysAVariant_AndTheFindingOpen()
    {
        var seed = await SeedAsync("Ekologická sviečka so zelenou pečaťou EcoCert", withProposal: true);

        await RunAsync(seed);

        var proposal = await RunTests.RowsAsync(Db, seed.Shop.TenantId, AnswerNoSql, seed.ProposalId!.Value);
        Assert.Null(proposal[0][0]);
        Assert.Equal("still_finding", proposal[0][2]);
        Assert.Equal("open", await StatusAsync(seed));
    }

    [Fact]
    public async Task ChangedAnswer_MakesTheJobANoOp()
    {
        var seed = await SeedAsync("Ekologická sviečka z vosku", withProposal: true);
        await RunTests.ScalarAsync<int>(Db, seed.Shop.TenantId,
            "WITH u AS (UPDATE checks.questions SET answer = 'yes' WHERE id = $1 RETURNING 1) SELECT count(*)::int FROM u", seed.QuestionId);

        await RunAsync(seed);

        Assert.Null((await RunTests.RowsAsync(Db, seed.Shop.TenantId, "SELECT alternatives FROM fixes.fix_proposals WHERE id = $1", seed.ProposalId!.Value))[0][0]);
        Assert.Equal("open", await StatusAsync(seed));
        Assert.Equal(0L, await RunTests.ScalarAsync<long>(Db, seed.Shop.TenantId,
            "SELECT count(*) FROM usage.usage_records WHERE shop_id = $1 AND operation = 'rewrite'", seed.Shop.ShopId));
    }

    private const string AnswerNoSql = """
        SELECT p.selected_alternative, a->>'text', a->>'recheck_status', p.status
        FROM fixes.fix_proposals p, jsonb_array_elements(p.alternatives) a
        WHERE p.id = $1 AND a->>'key' = 'answer_no'
        """;

    private sealed record Seed(RunShop Shop, string Storage, Guid FindingId, Guid QuestionId, Guid? ProposalId);

    private async Task<Seed> SeedAsync(string claim, bool withProposal)
    {
        var shop = await RunTests.CreateShopAsync();
        var storage = Directory.CreateTempSubdirectory("eshopguard-answer-").FullName;
        var pageId = Guid.CreateVersion7();
        var versionId = Guid.CreateVersion7();
        var findingId = Guid.CreateVersion7();
        var questionId = Guid.CreateVersion7();
        var proposalId = withProposal ? Guid.CreateVersion7() : (Guid?)null;
        var key = BlobKey.ForShop(shop.TenantId, shop.ShopId, "runs", "seed", "pages", pageId.ToString("N") + ".extract.json.gz");
        await using (var content = new MemoryStream(ExtractContextReader.Compress("Sviečka", $"Sviečka\n{claim}\nCena 5 €.")))
        {
            await new FileSystemBlobStore(storage).PutAsync(key, content, "application/gzip", Ct);
        }

        await RunTests.ScalarAsync<int>(Db, shop.TenantId,
            "WITH i AS (INSERT INTO shop.shop_markets (tenant_id, shop_id, country_code, is_home, status, source, created_at, updated_at) VALUES ($1, $2, 'SK', true, 'active', 'detected', now(), now()) RETURNING 1) SELECT count(*)::int FROM i",
            shop.TenantId, shop.ShopId);
        var ruleSet = await RunTests.ScalarAsync<Guid>(Db, shop.TenantId,
            """
            WITH i AS (INSERT INTO checks.rule_sets (id, module, version, jurisdictions, question_language, question_set_hash, definition, texts, source_hash, enabled, created_at, updated_at)
                VALUES (uuidv7(), 'eco', 'test-answer', '{sk}', 'en', '\x00', '{"name":"eco"}', '{}', '\x00', false, now(), now()) ON CONFLICT (module, version) DO NOTHING RETURNING id)
            SELECT id FROM i UNION ALL SELECT id FROM checks.rule_sets WHERE module = 'eco' AND version = 'test-answer' LIMIT 1
            """);
        const string Verdicts = """[{"jurisdiction":"sk","status":"finding","band":"high","score":0.9,"severity":"medium","checkability":"verify","legal_refs":[],"rule_set":"eco","rule_set_version":"test"}]""";
        await RunTests.ScalarAsync<int>(Db, shop.TenantId,
            """
            WITH p AS (INSERT INTO content.pages (id, tenant_id, shop_id, url, url_hash, path, language, page_type, source, title, status, first_seen_at, rotation_bucket, created_at, updated_at)
                VALUES ($1, $2, $3, $4, 1, '/sviecka/', 'sk', 'product', 'crawl', 'Sviečka', 'active', now(), 0, now(), now()) RETURNING 1),
            v AS (INSERT INTO content.page_versions (id, tenant_id, shop_id, page_id, fetched_at, extract_blob_key, is_current, created_at, updated_at)
                VALUES ($5, $2, $3, $1, now(), $6, true, now(), now()) RETURNING 1),
            f AS (INSERT INTO checks.findings (id, tenant_id, shop_id, rule_id, rule_set_id, module, checkability, severity, band, scope, segment_hash, page_id, text, verdicts, status, occurrences, created_at, updated_at)
                VALUES ($7, $2, $3, 'eco_label_unrecognized', $8, 'eco', 'verify', 'medium', 'high', 'segment', 21, $1, $9, $10::jsonb, 'open', 1, now(), now()) RETURNING 1),
            q AS (INSERT INTO checks.questions (id, tenant_id, shop_id, finding_id, scope, code, params, status, answer, answered_at, created_at, updated_at)
                VALUES ($11, $2, $3, $7, 'finding', 'certificate_evidence', '{"certificate":"EcoCert"}', 'answered', 'no', now(), now(), now()) RETURNING 1)
            SELECT (SELECT count(*) FROM p)::int + (SELECT count(*) FROM v)::int + (SELECT count(*) FROM f)::int + (SELECT count(*) FROM q)::int
            """, pageId, shop.TenantId, shop.ShopId, shop.BaseUrl + "sviecka/", versionId, key.Value, findingId, ruleSet, claim, Verdicts, questionId);
        await RunTests.ScalarAsync<int>(Db, shop.TenantId,
            "WITH u AS (UPDATE content.pages SET current_version_id = $1 WHERE id = $2 RETURNING 1) SELECT count(*)::int FROM u", versionId, pageId);
        if (proposalId is { } id)
        {
            await RunTests.ScalarAsync<int>(Db, shop.TenantId,
                """
                WITH pr AS (INSERT INTO fixes.fix_proposals (id, tenant_id, shop_id, page_id, page_version_id, finding_ids, field, block_index, original_text, proposed_text, recheck_status, status, created_at, updated_at)
                    VALUES ($1, $2, $3, $4, $5, ARRAY[$6]::uuid[], 'block', 2, $7, 'Sviečka z vosku (podľa výrobcu)', 'ok', 'proposed', now(), now()) RETURNING 1)
                SELECT count(*)::int FROM pr
                """, id, shop.TenantId, shop.ShopId, pageId, versionId, findingId, claim);
        }

        return new Seed(shop, storage, findingId, questionId, proposalId);
    }

    private async Task RunAsync(Seed seed)
    {
        await using (var scope = (await Workers.StartAsync("enqueue", RunTests.WorkerSettings(Path.GetTempPath(), slots: 0), _ => { })).Host.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(seed.Shop.TenantId);
            var db = scope.ServiceProvider.GetRequiredService<EshopGuardDb>();
            var queue = scope.ServiceProvider.GetRequiredService<IJobQueue>();
            await db.ExecuteInTenantTransactionAsync(async () =>
                await queue.EnqueueAsync(FixJobs.GenerateForAnswer(seed.Shop.TenantId, seed.Shop.ShopId, seed.QuestionId, seed.FindingId),
                    (Npgsql.NpgsqlTransaction)db.Database.CurrentTransaction!.GetDbTransaction(), Ct), Ct);
        }

        var services = RunTests.Services(RunTests.SlovakSite(new Uri("https://unused.example/")), new DeterministicTestJevClient());
        await Workers.StartAsync("answer", RunTests.WorkerSettings(seed.Storage), s =>
        {
            services(s);
            s.AddFixJobs();
        });
        for (var i = 0; i < 300; i++)
        {
            var state = await RunTests.ScalarAsync<string?>(Db, seed.Shop.TenantId,
                "SELECT state FROM ops.jobs WHERE kind = 'fix.generate_for_answer' AND shop_id = $1 ORDER BY id DESC LIMIT 1", seed.Shop.ShopId);
            if (state is "succeeded")
            {
                return;
            }

            Assert.NotEqual("failed", state);
            await Task.Delay(100, Ct);
        }

        throw new TimeoutException(await Db.DumpJobsAsync());
    }

    private Task<string> StatusAsync(Seed seed) =>
        RunTests.ScalarAsync<string>(Db, seed.Shop.TenantId, "SELECT status FROM checks.findings WHERE id = $1", seed.FindingId);
}
