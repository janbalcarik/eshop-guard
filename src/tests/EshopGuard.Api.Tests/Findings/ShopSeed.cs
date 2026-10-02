using EshopGuard.Application.Fixes;
using EshopGuard.Storage;
using EshopGuard.Tests.Shared;
using Npgsql;

namespace EshopGuard.Api.Tests.Findings;

/// <summary>
/// Rows of an e-shop as the worker of change 8 writes them (as <c>eshopguard_admin</c>, RLS bypassed): rule sets, pages with
/// their versions and extractions in the file store, findings with verdicts and occurrences, questions, proposals and groups.
/// </summary>
internal sealed class ShopSeed(Guid tenantId, Guid shopId, IBlobStore blobs)
{
    public Guid TenantId { get; } = tenantId;

    public Guid ShopId { get; } = shopId;

    public Guid RunId { get; private set; }

    /// <summary>A verdict of one jurisdiction in the shape of change 6 (<c>checks.findings.verdicts</c>).</summary>
    public static string Verdict(string jurisdiction, string checkability, string severity, string band = "high", string status = "finding") =>
        $$"""{"jurisdiction":"{{jurisdiction}}","status":"{{status}}","band":"{{band}}","score":0.9,"severity":"{{severity}}","checkability":"{{checkability}}","legal_refs":[{"ref":"{{(jurisdiction == "cz" ? "§ 5 zákona č. 634/1992 Sb." : "§ 7 zákona č. 108/2024 Z. z.")}}","status":"to_verify","jurisdiction":"{{jurisdiction}}"}],"rule_set":"eco","rule_set_version":"test"}""";

    public static string Verdicts(params string[] verdicts) => "[" + string.Join(",", verdicts) + "]";

    /// <summary>A row of <c>checks.rule_sets</c> for the texts of a rule set (shared by every test; the same row each time).</summary>
    public static async Task<Guid> RuleSetAsync(string name, string module)
    {
        var version = "test-" + name;
        await ExecuteAsync(
            """
            INSERT INTO checks.rule_sets (id, module, version, jurisdictions, question_language, question_set_hash, definition, texts, source_hash, enabled, published_at, created_at, updated_at)
            VALUES (uuidv7(), $1, $2, '{sk,cz}', 'en', '\x00', jsonb_build_object('name', $3::text), '{}'::jsonb, '\x00', false, now(), now(), now())
            ON CONFLICT (module, version) DO NOTHING
            """, module, version, name);
        return await ScalarAsync<Guid>("SELECT id FROM checks.rule_sets WHERE module = $1 AND version = $2", module, version);
    }

    public async Task<Guid> RunAsync(string kind = "full_analysis", string status = "finished", int pagesChecked = 120, int robotsBlocked = 7)
    {
        RunId = Guid.CreateVersion7();
        await ExecuteAsync(
            """
            INSERT INTO checks.runs (id, tenant_id, shop_id, kind, trigger, status, priority, jurisdictions, modules, stats, progress, started_at, finished_at, created_at, updated_at)
            VALUES ($1, $2, $3, $4, 'user', $5, 0, '{sk,cz}', '{eco,legal}', jsonb_build_object('pages_checked', $6::int, 'unchecked', jsonb_build_object('robots_blocked', $7::int)),
                '{"pages_planned":120,"pages_fetched":120,"pages_processed":120}'::jsonb, now() - interval '1 hour', now(), now(), now())
            """, RunId, TenantId, ShopId, kind, status, pagesChecked, robotsBlocked);
        return RunId;
    }

    /// <summary>A page with its current version; its extraction (the main text, one block per line) goes to the file store.</summary>
    public async Task<(Guid PageId, Guid VersionId)> PageAsync(
        string title, string path, string language, string mainText, string pageType = "product", string source = "crawl", string? externalId = null)
    {
        var pageId = Guid.CreateVersion7();
        var versionId = Guid.CreateVersion7();
        var key = BlobKey.ForShop(TenantId, ShopId, "runs", "seed", "pages", pageId.ToString("N") + ".extract.json.gz");
        await using (var content = new MemoryStream(ExtractContextReader.Compress(title, mainText)))
        {
            await blobs.PutAsync(key, content, "application/gzip", TestContext.Current.CancellationToken);
        }

        await ExecuteAsync(
            """
            INSERT INTO content.pages (id, tenant_id, shop_id, url, url_hash, path, language, page_type, source, external_id, title, status, first_seen_at, rotation_bucket, created_at, updated_at)
            VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, 'active', now(), 0, now(), now())
            """, pageId, TenantId, ShopId, "https://www.bylinkovo.sk" + path, (long)pageId.GetHashCode(), path, language, pageType, source, externalId, title);
        await ExecuteAsync(
            """
            INSERT INTO content.page_versions (id, tenant_id, shop_id, page_id, run_id, fetched_at, extract_blob_key, is_current, created_at, updated_at)
            VALUES ($1, $2, $3, $4, $5, now(), $6, true, now(), now())
            """, versionId, TenantId, ShopId, pageId, RunId == Guid.Empty ? null : RunId, key.Value);
        await ExecuteAsync("UPDATE content.pages SET current_version_id = $1 WHERE shop_id = $2 AND id = $3", versionId, ShopId, pageId);
        return (pageId, versionId);
    }

    /// <summary>A finding with its verdicts (the first is the strictest: its group, severity and band go to the columns) and occurrences.</summary>
    public async Task<Guid> FindingAsync(
        Guid ruleSetId, string ruleId, string module, string verdicts, string status, string? text, long? segmentHash, IReadOnlyList<Guid> pages, string scope = "segment")
    {
        var id = Guid.CreateVersion7();
        using var document = System.Text.Json.JsonDocument.Parse(verdicts);
        var strictest = document.RootElement[0];
        string Field(string name) => strictest.GetProperty(name).GetString()!;
        var checkability = Field("checkability") is "text" or "assess" ? Field("checkability") : "verify";
        await ExecuteAsync(
            """
            INSERT INTO checks.findings (id, tenant_id, shop_id, rule_id, rule_set_id, module, checkability, severity, band, scope, segment_hash, page_id, text, score,
                verdicts, legal_refs, params, status, occurrences, first_run_id, last_seen_run_id, created_at, updated_at)
            VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, 0.9, $14::jsonb, $15::jsonb, '{}'::jsonb, $16, $17, $18, $18, now(), now())
            """,
            id, TenantId, ShopId, ruleId, ruleSetId, module, checkability, Field("severity"), Field("band"), scope, segmentHash,
            pages.Count > 0 ? pages[0] : null, text, verdicts, strictest.GetProperty("legal_refs").GetRawText(), status, Math.Max(1, pages.Count),
            RunId == Guid.Empty ? null : RunId);
        foreach (var page in pages)
        {
            await ExecuteAsync(
                "INSERT INTO checks.finding_occurrences (tenant_id, finding_id, page_id, shop_id, created_at) VALUES ($1, $2, $3, $4, now())",
                TenantId, id, page, ShopId);
        }

        return id;
    }

    public async Task<Guid> QuestionAsync(Guid? findingId, string code, string? @params = null, string scope = "finding", string status = "open")
    {
        var id = Guid.CreateVersion7();
        await ExecuteAsync(
            "INSERT INTO checks.questions (id, tenant_id, shop_id, finding_id, scope, code, params, status, created_at, updated_at) VALUES ($1, $2, $3, $4, $5, $6, $7::jsonb, $8, now(), now())",
            id, TenantId, ShopId, findingId, scope, code, @params ?? "{}", status);
        return id;
    }

    public async Task<Guid> ProposalAsync(
        (Guid PageId, Guid VersionId) page, Guid[] findingIds, string original, string proposed, int? blockIndex, string recheck = "ok", string status = "proposed",
        string? alternatives = null, string? placeholders = null, Guid? groupId = null, string field = "block")
    {
        var id = Guid.CreateVersion7();
        await ExecuteAsync(
            """
            INSERT INTO fixes.fix_proposals (id, tenant_id, shop_id, page_id, page_version_id, group_id, finding_ids, field, block_index, original_text, proposed_text,
                alternatives, placeholders, recheck_status, status, created_run_id, created_at, updated_at)
            VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12::jsonb, $13::jsonb, $14, $15, $16, now(), now())
            """,
            id, TenantId, ShopId, page.PageId, page.VersionId, groupId, findingIds, field, blockIndex, original, proposed, alternatives, placeholders, recheck, status,
            RunId == Guid.Empty ? null : RunId);
        return id;
    }

    public async Task<Guid> GroupAsync(
        string kind, long? segmentHash, string original, string? template, int pageCount, string? placeholders = null, string status = "draft",
        string? recheck = null, string? fit = null, string? filledValues = null)
    {
        var id = Guid.CreateVersion7();
        await ExecuteAsync(
            """
            INSERT INTO fixes.fix_groups (id, tenant_id, shop_id, kind, segment_hash, original_text, replacement_template, placeholders, filled_values, status, page_count,
                mode, fit, recheck_status, created_at, updated_at)
            VALUES ($1, $2, $3, $4, $5, $6, $7, $8::jsonb, $9::jsonb, $10, $11, 'replace', $12::jsonb, $13, now(), now())
            """, id, TenantId, ShopId, kind, segmentHash, original, template, placeholders, filledValues, status, pageCount, fit, recheck);
        return id;
    }

    public Task ConnectorAsync(string platform = "shoptet", string access = "read_write", string status = "connected") => ExecuteAsync(
        "INSERT INTO shop.connectors (tenant_id, shop_id, platform, status, access, created_at, updated_at) VALUES ($1, $2, $3, $4, $5, now(), now())",
        TenantId, ShopId, platform, status, access);

    public static async Task ExecuteAsync(string sql, params object?[] parameters)
    {
        await using var connection = new NpgsqlConnection(TestConfiguration.ConnectionString("Admin"));
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var parameter in parameters)
        {
            command.Parameters.Add(parameter switch
            {
                null => new NpgsqlParameter { Value = DBNull.Value },
                Guid[] ids => new NpgsqlParameter { Value = ids, NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Uuid },
                _ => new NpgsqlParameter { Value = parameter },
            });
        }

        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    public static async Task<T> ScalarAsync<T>(string sql, params object?[] parameters)
    {
        await using var connection = new NpgsqlConnection(TestConfiguration.ConnectionString("Admin"));
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var parameter in parameters)
        {
            command.Parameters.Add(new NpgsqlParameter { Value = parameter ?? DBNull.Value });
        }

        return (T)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }
}
