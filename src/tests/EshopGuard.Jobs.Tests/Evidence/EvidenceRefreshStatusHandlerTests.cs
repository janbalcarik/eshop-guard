using EshopGuard.Data;
using EshopGuard.Data.Tenancy;
using EshopGuard.Jobs.Evidence;
using EshopGuard.Jobs.Tests.Runs.Support;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace EshopGuard.Jobs.Tests.Evidence;

/// <summary>
/// The daily refresh of the evidence (change 11, task 6.7; AD 10): the certificate BDIH valid until 20. 10. 2026 reminds once
/// while it expires, and its expiry reopens the 6 findings kept with it, supersedes their memory and notifies.
/// </summary>
public sealed class EvidenceRefreshStatusHandlerTests(JobsTestDatabase database) : JobsTestBase(database)
{
    [Fact]
    public async Task Bdih_RemindsOnce_AndItsExpiryReopensSixFindings()
    {
        var (shop, evidenceId, userId) = await SeedAsync();
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 1, 6, 0, 0, TimeSpan.Zero));
        var worker = await Workers.StartAsync("evidence", RunTests.WorkerSettings(Path.GetTempPath(), slots: 0), s => s.AddEvidenceJobs());

        var first = await RefreshAsync(worker.Host.Services, shop.TenantId, time.GetUtcNow());
        time.Advance(TimeSpan.FromDays(1));
        var second = await RefreshAsync(worker.Host.Services, shop.TenantId, time.GetUtcNow());

        Assert.Equal((1, 0, 0), (first.Reminded, first.Expired, first.FindingsReopened));
        Assert.Equal(0, second.Reminded);
        Assert.Equal("expiring", await RunTests.ScalarAsync<string>(Db, shop.TenantId, "SELECT status FROM fixes.evidence_items WHERE id = $1", evidenceId));
        Assert.Equal(1L, await RunTests.ScalarAsync<long>(Db, shop.TenantId,
            "SELECT count(*) FROM iam.notifications WHERE kind = 'evidence_expiring' AND user_id = $1 AND params->>'days' = '19'", userId));
        Assert.Equal(1L, await RunTests.ScalarAsync<long>(Db, shop.TenantId, "SELECT count(*) FROM ops.outbox WHERE payload->>'template' = 'evidence_expiring'"));

        time.SetUtcNow(new DateTimeOffset(2026, 10, 20, 21, 59, 0, TimeSpan.Zero));
        var lastDay = await RefreshAsync(worker.Host.Services, shop.TenantId, time.GetUtcNow());
        time.SetUtcNow(new DateTimeOffset(2026, 10, 20, 22, 30, 0, TimeSpan.Zero));
        var expired = await RefreshAsync(worker.Host.Services, shop.TenantId, time.GetUtcNow());

        // 20. 10. 23:59 in Bratislava is still the last day; 21. 10. 00:30 is after it.
        Assert.Equal(0, lastDay.Expired);
        Assert.Equal((1, 6), (expired.Expired, expired.FindingsReopened));
        Assert.Equal(6L, await RunTests.ScalarAsync<long>(Db, shop.TenantId, "SELECT count(*) FROM checks.findings WHERE shop_id = $1 AND status = 'open'", shop.ShopId));
        Assert.Equal(0L, await RunTests.ScalarAsync<long>(Db, shop.TenantId, "SELECT count(*) FROM fixes.decision_memory WHERE evidence_id = $1 AND superseded_at IS NULL", evidenceId));
        Assert.Equal(1L, await RunTests.ScalarAsync<long>(Db, shop.TenantId,
            "SELECT count(*) FROM iam.notifications WHERE kind = 'evidence_expired' AND params->>'findings' = '6'"));
        Assert.Equal(6L, await RunTests.ScalarAsync<long>(Db, shop.TenantId,
            "SELECT count(*) FROM ops.audit_log WHERE action = 'finding.status_changed' AND actor_kind = 'system' AND data->>'reasonCode' = 'evidence_expired'"));
    }

    private static async Task<EvidenceRefreshResult> RefreshAsync(IServiceProvider services, Guid tenantId, DateTimeOffset now)
    {
        await using var scope = services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<EshopGuardDb>();
        var refresher = scope.ServiceProvider.GetRequiredService<EvidenceStatusRefresher>();
        return await db.ExecuteInTenantTransactionAsync(() =>
            refresher.RefreshAsync(db, (Npgsql.NpgsqlTransaction)db.Database.CurrentTransaction!.GetDbTransaction(), tenantId, now, TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);
    }

    private async Task<(RunShop Shop, Guid EvidenceId, Guid UserId)> SeedAsync()
    {
        var shop = await RunTests.CreateShopAsync();
        var userId = Guid.CreateVersion7();
        var evidenceId = Guid.CreateVersion7();
        await RunTests.ScalarAsync<int>(Db, shop.TenantId,
            """
            WITH u AS (INSERT INTO iam.users (id, email, email_confirmed, access_failed_count, created_at, updated_at) VALUES ($1, $2, true, 0, now(), now()) RETURNING 1),
            m AS (INSERT INTO iam.memberships (tenant_id, user_id, role, created_at, updated_at) SELECT $3, $1, 'owner', now(), now() FROM u RETURNING 1)
            SELECT count(*)::int FROM m
            """, userId, $"jana-{userId:N}@example.invalid", shop.TenantId);
        var ruleSet = await RunTests.ScalarAsync<Guid>(Db, shop.TenantId,
            """
            WITH i AS (INSERT INTO checks.rule_sets (id, module, version, jurisdictions, question_language, question_set_hash, definition, texts, source_hash, enabled, created_at, updated_at)
                VALUES (uuidv7(), 'eco', 'test-evidence', '{sk}', 'en', '\x00', '{"name":"eco"}', '{}', '\x00', false, now(), now()) ON CONFLICT (module, version) DO NOTHING RETURNING id)
            SELECT id FROM i UNION ALL SELECT id FROM checks.rule_sets WHERE module = 'eco' AND version = 'test-evidence' LIMIT 1
            """);
        await RunTests.ScalarAsync<int>(Db, shop.TenantId,
            """
            WITH e AS (INSERT INTO fixes.evidence_items (id, tenant_id, claim_text, subject_kind, subject_label, kind, source, valid_until, status, created_at, updated_at)
                VALUES ($1, $2, 'Kontrolovaná prírodná kozmetika', 'brand', 'Kvitok', 'certificate', 'upload', '2026-10-20T00:00:00Z', 'valid', now(), now()) RETURNING 1)
            SELECT count(*)::int FROM e
            """, evidenceId, shop.TenantId);
        for (var i = 1; i <= 6; i++)
        {
            var pageId = Guid.CreateVersion7();
            var findingId = Guid.CreateVersion7();
            await RunTests.ScalarAsync<int>(Db, shop.TenantId,
                """
                WITH p AS (INSERT INTO content.pages (id, tenant_id, shop_id, url, url_hash, path, language, page_type, source, title, status, first_seen_at, rotation_bucket, created_at, updated_at)
                    VALUES ($1, $2, $3, $4, $5, $6, 'sk', 'product', 'crawl', 'Kvitok', 'active', now(), 0, now(), now()) RETURNING 1),
                f AS (INSERT INTO checks.findings (id, tenant_id, shop_id, rule_id, rule_set_id, module, checkability, severity, band, scope, segment_hash, page_id, text, verdicts, status, occurrences, created_at, updated_at)
                    VALUES ($7, $2, $3, 'eco_label_unrecognized', $8, 'eco', 'verify', 'medium', 'high', 'segment', $5, $1, 'Kontrolovaná prírodná kozmetika', '[]', 'kept_with_evidence', 1, now(), now()) RETURNING 1),
                l AS (INSERT INTO fixes.evidence_links (id, tenant_id, evidence_id, shop_id, page_id, finding_id, created_at) SELECT uuidv7(), $2, $9, $3, $1, $7, now() FROM f RETURNING 1),
                d AS (INSERT INTO fixes.decision_memory (id, tenant_id, shop_id, segment_hash, normalized_text, decision, evidence_id, auto_publish, created_at, updated_at)
                    SELECT uuidv7(), $2, $3, $5, 'Kontrolovaná prírodná kozmetika', 'keep_with_evidence', $9, false, now(), now() FROM f RETURNING 1)
                SELECT (SELECT count(*) FROM p)::int + (SELECT count(*) FROM l)::int + (SELECT count(*) FROM d)::int
                """, pageId, shop.TenantId, shop.ShopId, shop.BaseUrl + $"kvitok-{i}/", (long)(9100 + i), $"/kvitok-{i}/", findingId, ruleSet, evidenceId);
        }

        return (shop, evidenceId, userId);
    }
}
