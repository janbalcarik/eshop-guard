using EshopGuard.Data;
using EshopGuard.Data.Tenancy;
using EshopGuard.Jobs.Fixes;
using EshopGuard.Jobs.Queue;
using EshopGuard.Jobs.Tests.Runs.Support;
using EshopGuard.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace EshopGuard.Jobs.Tests.Fixes;

/// <summary>
/// The job <c>fix.recheck</c> (change 11, task 4.10; AD 5) with the deterministic Jev of the tests: a text without an
/// environmental word passes in SK and CZ, an „ekologická“ one still finds something, and a result for a text changed
/// meanwhile is not written.
/// </summary>
public sealed class FixRecheckHandlerTests(JobsTestDatabase database) : JobsTestBase(database)
{
    [Fact]
    public async Task TextWithoutTheClaim_IsOk_InBothCountries()
    {
        var (shop, proposalId, storage) = await SeedAsync("Rukoväť kefky je z bambusu namiesto plastu.");
        await StartWorkerAsync(storage);

        await EnqueueAsync(shop, proposalId);
        var (status, result) = await WaitAsync(shop, proposalId);

        Assert.Equal("ok", status);
        Assert.Contains("\"sk\": \"ok\"", result, StringComparison.Ordinal);
        Assert.Contains("\"cz\": \"ok\"", result, StringComparison.Ordinal);
        Assert.Equal(1L, await RunTests.ScalarAsync<long>(Db, shop.TenantId,
            "SELECT count(*) FROM usage.usage_records WHERE shop_id = $1 AND operation = 'recheck' AND run_id IS NULL", shop.ShopId));
    }

    [Fact]
    public async Task TextWithTheClaim_StillFinds_WithTheCountryAndRules()
    {
        var (shop, proposalId, storage) = await SeedAsync("Ekologická kefka šetrná k prírode.");
        await StartWorkerAsync(storage);

        await EnqueueAsync(shop, proposalId);
        var (status, result) = await WaitAsync(shop, proposalId);

        Assert.True(status == "still_finding", result);
        // A general environmental claim is forbidden in SK; in CZ the law has no explicit ban yet (the verdict is not decided by the text).
        Assert.Contains("\"sk\": \"still_finding\"", result, StringComparison.Ordinal);
        Assert.Contains("\"cz\": ", result, StringComparison.Ordinal);
        Assert.Contains("eco_", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResultOfAnOlderText_IsNotWritten()
    {
        var (shop, proposalId, storage) = await SeedAsync("Rukoväť kefky je z bambusu namiesto plastu.");
        await EnqueueAsync(shop, proposalId);
        // The merchant writes another text before the worker takes the job.
        await RunTests.ScalarAsync<int>(Db, shop.TenantId,
            "WITH u AS (UPDATE fixes.fix_proposals SET edited_text = 'Iný text bez tvrdenia.' WHERE id = $1 RETURNING 1) SELECT count(*)::int FROM u", proposalId);
        await StartWorkerAsync(storage);

        for (var i = 0; i < 300 && await RunTests.ScalarAsync<long>(Db, shop.TenantId,
            "SELECT count(*) FROM ops.jobs WHERE kind = 'fix.recheck' AND shop_id = $1 AND state IN ('queued', 'running')", shop.ShopId) > 0; i++)
        {
            await Task.Delay(100, Ct);
        }

        Assert.Equal("pending", await RunTests.ScalarAsync<string>(Db, shop.TenantId, "SELECT recheck_status FROM fixes.fix_proposals WHERE id = $1", proposalId));
        Assert.Null(await RunTests.ScalarAsync<string?>(Db, shop.TenantId, "SELECT recheck_result::text FROM fixes.fix_proposals WHERE id = $1", proposalId));
    }

    private async Task<(RunShop Shop, Guid ProposalId, string Storage)> SeedAsync(string editedText)
    {
        var shop = await RunTests.CreateShopAsync();
        var storage = Directory.CreateTempSubdirectory("eshopguard-recheck-").FullName;
        var pageId = Guid.CreateVersion7();
        var versionId = Guid.CreateVersion7();
        var findingId = Guid.CreateVersion7();
        var proposalId = Guid.CreateVersion7();
        var key = BlobKey.ForShop(shop.TenantId, shop.ShopId, "runs", "seed", "pages", pageId.ToString("N") + ".extract.json.gz");
        await using (var content = new MemoryStream(ExtractContextReader.Compress("Kefka", "Kefka\nBambusová kefka – ekologická alternatíva\nCena 3 €.")))
        {
            await new FileSystemBlobStore(storage).PutAsync(key, content, "application/gzip", Ct);
        }

        foreach (var sql in new[]
        {
            "INSERT INTO shop.shop_markets (tenant_id, shop_id, country_code, is_home, status, source, created_at, updated_at) VALUES ($1, $2, 'SK', true, 'active', 'detected', now(), now())",
            "INSERT INTO shop.shop_markets (tenant_id, shop_id, country_code, is_home, status, source, created_at, updated_at) VALUES ($1, $2, 'CZ', false, 'active', 'detected', now(), now())",
        })
        {
            await RunTests.ScalarAsync<int>(Db, shop.TenantId, $"WITH i AS ({sql} RETURNING 1) SELECT count(*)::int FROM i", shop.TenantId, shop.ShopId);
        }

        var ruleSet = await RunTests.ScalarAsync<Guid>(Db, shop.TenantId,
            """
            WITH i AS (INSERT INTO checks.rule_sets (id, module, version, jurisdictions, question_language, question_set_hash, definition, texts, source_hash, enabled, created_at, updated_at)
                VALUES (uuidv7(), 'eco', 'test-recheck', '{sk,cz}', 'en', '\x00', '{"name":"eco"}', '{}', '\x00', false, now(), now()) ON CONFLICT (module, version) DO NOTHING RETURNING id)
            SELECT id FROM i UNION ALL SELECT id FROM checks.rule_sets WHERE module = 'eco' AND version = 'test-recheck' LIMIT 1
            """);
        await RunTests.ScalarAsync<int>(Db, shop.TenantId,
            """
            WITH p AS (INSERT INTO content.pages (id, tenant_id, shop_id, url, url_hash, path, language, page_type, source, title, status, first_seen_at, rotation_bucket, created_at, updated_at)
                VALUES ($1, $2, $3, $4, 1, '/kefka/', 'sk', 'product', 'crawl', 'Kefka', 'active', now(), 0, now(), now()) RETURNING 1),
            v AS (INSERT INTO content.page_versions (id, tenant_id, shop_id, page_id, fetched_at, extract_blob_key, is_current, created_at, updated_at)
                VALUES ($5, $2, $3, $1, now(), $6, true, now(), now()) RETURNING 1)
            SELECT (SELECT count(*) FROM p)::int + (SELECT count(*) FROM v)::int
            """, pageId, shop.TenantId, shop.ShopId, shop.BaseUrl + "kefka/", versionId, key.Value);
        await RunTests.ScalarAsync<int>(Db, shop.TenantId,
            """
            WITH f AS (INSERT INTO checks.findings (id, tenant_id, shop_id, rule_id, rule_set_id, module, checkability, severity, band, scope, segment_hash, page_id, text, verdicts, status, occurrences, created_at, updated_at)
                VALUES ($1, $2, $3, 'eco_generic_claim', $4, 'eco', 'text', 'high', 'high', 'segment', 11, $5, 'Bambusová kefka – ekologická alternatíva', '[]', 'open', 1, now(), now()) RETURNING 1),
            pr AS (INSERT INTO fixes.fix_proposals (id, tenant_id, shop_id, page_id, page_version_id, finding_ids, field, block_index, original_text, proposed_text, edited_text, recheck_status, status, created_at, updated_at)
                VALUES ($6, $2, $3, $5, $7, ARRAY[$1]::uuid[], 'block', 2, 'Bambusová kefka – ekologická alternatíva', 'Bambusová kefka', $8, 'pending', 'edited', now(), now()) RETURNING 1)
            SELECT (SELECT count(*) FROM f)::int + (SELECT count(*) FROM pr)::int
            """, findingId, shop.TenantId, shop.ShopId, ruleSet, pageId, proposalId, versionId, editedText);
        return (shop, proposalId, storage);
    }

    private async Task StartWorkerAsync(string storage)
    {
        var services = RunTests.Services(RunTests.SlovakSite(new Uri("https://unused.example/")), new DeterministicTestJevClient());
        await Workers.StartAsync("recheck", RunTests.WorkerSettings(storage), s =>
        {
            services(s);
            s.AddFixJobs();
        });
    }

    private async Task EnqueueAsync(RunShop shop, Guid proposalId)
    {
        await using var scope = (await Workers.StartAsync("enqueue", RunTests.WorkerSettings(Path.GetTempPath(), slots: 0), _ => { })).Host.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(shop.TenantId);
        var db = scope.ServiceProvider.GetRequiredService<EshopGuardDb>();
        var queue = scope.ServiceProvider.GetRequiredService<IJobQueue>();
        await db.ExecuteInTenantTransactionAsync(async () =>
        {
            var proposal = await db.FixProposals.AsNoTracking().FirstAsync(p => p.Id == proposalId, Ct);
            await queue.EnqueueAsync(FixJobs.Recheck(shop.TenantId, shop.ShopId, FixJobs.TargetProposal, proposalId, ProposalText.Hash(ProposalText.Text(proposal)), ["cz", "sk"]),
                (Npgsql.NpgsqlTransaction)db.Database.CurrentTransaction!.GetDbTransaction(), Ct);
        }, Ct);
    }

    private async Task<(string Status, string Result)> WaitAsync(RunShop shop, Guid proposalId)
    {
        for (var i = 0; i < 300; i++)
        {
            var rows = await RunTests.RowsAsync(Db, shop.TenantId, "SELECT recheck_status, recheck_result::text FROM fixes.fix_proposals WHERE id = $1", proposalId);
            if ((string)rows[0][0]! != "pending")
            {
                return ((string)rows[0][0]!, (string)rows[0][1]!);
            }

            await Task.Delay(100, Ct);
        }

        throw new TimeoutException(await Db.DumpJobsAsync());
    }
}
