using System.Security.Cryptography;
using System.Text;
using EshopGuard.Core.Crawl;
using EshopGuard.Core.Languages;
using EshopGuard.Core.Models;
using EshopGuard.Core.Pipeline;
using EshopGuard.Core.Storage;
using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Checks;
using Npgsql;
using NpgsqlTypes;
using DataPageType = EshopGuard.Data.Entities.Content.PageType;

namespace EshopGuard.Jobs.Runs;

/// <summary>An address of a run with its outcome (<c>checks.run_urls</c>).</summary>
internal sealed record RunUrlRow(string ScopeKey, string Url, string? Language, RunUrlState State, string? Queue, int? Seq, int? BatchNo, string? ErrorCode, Guid? PageId, int? HttpStatus = null);

/// <summary>A page of the run read back for a step over the whole site: its address, page and extraction.</summary>
internal sealed record RunPage(string ScopeKey, string Url, Guid PageId, string? Language, ExtractedPageRecord Record);

/// <summary>Pages of a run: identity, writes of <c>content.pages</c>, <c>content.page_versions</c> and <c>checks.run_urls</c>, reading back in the order of the crawl.</summary>
internal static class RunPages
{
    /// <summary>
    /// 64-bit identity of a page (<c>pages.url_hash</c>, <c>run_urls.url_hash</c>): the first 8 bytes of SHA-256 of the final
    /// address. A version reached by a cookie or Accept-Language has the same addresses as another version, so its language is
    /// part of the identity.
    /// </summary>
    public static long UrlHash(string url, VersionCrawlScope? scope)
    {
        var identity = scope is { } s && (s.Cookies.Count > 0 || s.AcceptLanguage is not null) ? url + "\u001F" + (s.ExpectedLanguage ?? "") : url;
        return BitConverter.ToInt64(SHA256.HashData(Encoding.UTF8.GetBytes(identity)), 0);
    }

    /// <summary>The state of an address after its batch of downloads.</summary>
    public static (RunUrlState State, string? Code) Outcome(FetchedPage page) => page.Outcome switch
    {
        FetchOutcome.Ok when page.Extract is { Status: ExtractionStatus.NotProcessed } extract => (RunUrlState.ExtractTimeout, extract.NotProcessedReason ?? ExtractStep.TimeoutReason),
        FetchOutcome.Ok when page.Extract is { Info.TextNotLoaded: true } => (RunUrlState.NotLoaded, "text_not_loaded"),
        FetchOutcome.Ok => (RunUrlState.Extracted, null),
        FetchOutcome.NotModified => (RunUrlState.NotModified, null),
        FetchOutcome.NotHtml => (RunUrlState.NotHtml, "not_html"),
        FetchOutcome.RedirectOffSite => (RunUrlState.OffsiteRedirect, "offsite_redirect"),
        FetchOutcome.RobotsBlocked => (RunUrlState.RobotsBlocked, "robots_blocked"),
        FetchOutcome.Blocked => (RunUrlState.SsrfBlocked, "ssrf_blocked"),
        _ => (StepErrorPolicy.OfShopFailure(page.HttpStatus, page.FailureCode), page.FailureCode ?? "fetch_failed"),
    };

    /// <summary>Inserts or updates the page and returns its id.</summary>
    public static async Task<Guid> UpsertPageAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid tenantId, Guid shopId, long urlHash, ExtractedPageRecord page,
        string? etag, DateTimeOffset? lastModified, DateTimeOffset now, CancellationToken ct)
    {
        var info = page.Info;
        var url = new Uri(info.Url);
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO content.pages (id, tenant_id, shop_id, url, url_hash, path, language, hreflang_group, page_type, source, title, status,
                is_hidden_in_shop, first_seen_at, last_seen_at, last_fetched_at, http_etag, http_last_modified, rotation_bucket, created_at, updated_at)
            VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, 'crawl', $10, $11, false, $12, $12, $12, $13, $14, $15, $12, $12)
            ON CONFLICT (shop_id, url_hash) DO UPDATE SET url = excluded.url, path = excluded.path, language = excluded.language,
                hreflang_group = excluded.hreflang_group, page_type = excluded.page_type, title = excluded.title, status = excluded.status,
                last_seen_at = excluded.last_seen_at, last_fetched_at = excluded.last_fetched_at, http_etag = excluded.http_etag,
                http_last_modified = excluded.http_last_modified, updated_at = excluded.updated_at
            RETURNING id
            """, connection, transaction)
        {
            Parameters =
            {
                new NpgsqlParameter { Value = Guid.CreateVersion7() },
                new NpgsqlParameter { Value = tenantId },
                new NpgsqlParameter { Value = shopId },
                new NpgsqlParameter { Value = info.Url },
                new NpgsqlParameter { Value = urlHash },
                new NpgsqlParameter { Value = url.AbsolutePath },
                new NpgsqlParameter { Value = (object?)info.Language ?? DBNull.Value, NpgsqlDbType = NpgsqlDbType.Text },
                new NpgsqlParameter { Value = (object?)info.HreflangGroup ?? DBNull.Value, NpgsqlDbType = NpgsqlDbType.Text },
                new NpgsqlParameter { Value = SnakeCaseEnumConverter<DataPageType>.ToText(DataType(info.Type)) },
                new NpgsqlParameter { Value = (object?)Truncate(info.Title, 1000) ?? DBNull.Value, NpgsqlDbType = NpgsqlDbType.Text },
                new NpgsqlParameter { Value = info.TextNotLoaded ? "not_loaded" : "active" },
                new NpgsqlParameter { Value = now },
                new NpgsqlParameter { Value = (object?)etag ?? DBNull.Value, NpgsqlDbType = NpgsqlDbType.Text },
                new NpgsqlParameter { Value = (object?)lastModified?.ToString("R") ?? DBNull.Value, NpgsqlDbType = NpgsqlDbType.Text },
                new NpgsqlParameter { Value = (short)(Math.Abs(urlHash % 7)) },
            },
        };
        return (Guid)(await command.ExecuteScalarAsync(ct).ConfigureAwait(false))!;
    }

    /// <summary>Records the outcome of an address; a final state is never set back to <c>pending</c>.</summary>
    public static async Task UpsertUrlAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid tenantId, Guid runId, long urlHash, RunUrlRow row, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO checks.run_urls (tenant_id, run_id, scope_key, url_hash, url, language, state, queue, seq, batch_no, attempts, error_code, page_id, http_status, created_at, updated_at)
            VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, now(), now())
            ON CONFLICT (run_id, scope_key, url_hash) DO UPDATE SET
                state = CASE WHEN excluded.state = 'pending' THEN checks.run_urls.state ELSE excluded.state END,
                url = excluded.url,
                queue = coalesce(checks.run_urls.queue, excluded.queue),
                seq = coalesce(excluded.seq, checks.run_urls.seq),
                batch_no = coalesce(excluded.batch_no, checks.run_urls.batch_no),
                attempts = checks.run_urls.attempts + excluded.attempts,
                error_code = coalesce(excluded.error_code, checks.run_urls.error_code),
                page_id = coalesce(excluded.page_id, checks.run_urls.page_id),
                http_status = coalesce(excluded.http_status, checks.run_urls.http_status),
                updated_at = now()
            """, connection, transaction)
        {
            Parameters =
            {
                new NpgsqlParameter { Value = tenantId },
                new NpgsqlParameter { Value = runId },
                new NpgsqlParameter { Value = row.ScopeKey },
                new NpgsqlParameter { Value = urlHash },
                new NpgsqlParameter { Value = row.Url },
                new NpgsqlParameter { Value = (object?)row.Language ?? DBNull.Value, NpgsqlDbType = NpgsqlDbType.Text },
                new NpgsqlParameter { Value = SnakeCaseEnumConverter<RunUrlState>.ToText(row.State) },
                new NpgsqlParameter { Value = (object?)row.Queue ?? DBNull.Value, NpgsqlDbType = NpgsqlDbType.Text },
                new NpgsqlParameter { Value = (object?)row.Seq ?? DBNull.Value, NpgsqlDbType = NpgsqlDbType.Integer },
                new NpgsqlParameter { Value = (object?)row.BatchNo ?? DBNull.Value, NpgsqlDbType = NpgsqlDbType.Integer },
                new NpgsqlParameter { Value = (short)(row.State == RunUrlState.Pending ? 0 : 1) },
                new NpgsqlParameter { Value = (object?)row.ErrorCode ?? DBNull.Value, NpgsqlDbType = NpgsqlDbType.Text },
                new NpgsqlParameter { Value = (object?)row.PageId ?? DBNull.Value, NpgsqlDbType = NpgsqlDbType.Uuid },
                new NpgsqlParameter { Value = row.HttpStatus is { } status ? (short)status : DBNull.Value, NpgsqlDbType = NpgsqlDbType.Smallint },
            },
        };
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Addresses of the run, optionally only in the given states.</summary>
    public static async Task<List<RunUrlRow>> LoadUrlsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid runId, IReadOnlyCollection<RunUrlState>? states, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT scope_key, url, language, state, queue, seq, batch_no, error_code, page_id
            FROM checks.run_urls WHERE run_id = $1 AND ($2::text[] IS NULL OR state = ANY($2))
            ORDER BY scope_key COLLATE "C", seq NULLS LAST, url COLLATE "C"
            """, connection, transaction)
        {
            Parameters =
            {
                new NpgsqlParameter { Value = runId },
                new NpgsqlParameter
                {
                    Value = states is null ? DBNull.Value : states.Select(SnakeCaseEnumConverter<RunUrlState>.ToText).ToArray(),
                    NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text,
                },
            },
        };
        var rows = new List<RunUrlRow>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            rows.Add(new RunUrlRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                SnakeCaseEnumConverter<RunUrlState>.FromText(reader.GetString(3)),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetInt32(5),
                reader.IsDBNull(6) ? null : reader.GetInt32(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetGuid(8)));
        }

        return rows;
    }

    /// <summary>The states of addresses whose page was read (the pages of the analysis and of its report).</summary>
    public static readonly RunUrlState[] PageStates = [RunUrlState.Extracted, RunUrlState.NotLoaded];

    /// <summary>
    /// The pages of the run in the order of the crawl (scope by scope, batch by batch), their extraction read back from the
    /// content store: the steps over the whole site see them as the CLI sees its list of pages.
    /// </summary>
    public static async Task<List<RunPage>> LoadPagesAsync(IPageContentStore contents, IReadOnlyList<RunUrlRow> urls, CancellationToken ct)
    {
        var pages = new List<RunPage>(urls.Count);
        foreach (var url in urls.Where(u => PageStates.Contains(u.State) && u.PageId is not null))
        {
            var json = await contents.GetExtractAsync(new PageContentKey("", url.Url), ct).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"The extraction of a page of the run is missing ({url.State}).");
            pages.Add(new RunPage(url.ScopeKey, url.Url, url.PageId!.Value, url.Language, PipelineJson.Deserialize<ExtractedPageRecord>(json)));
        }

        return pages;
    }

    /// <summary>
    /// A new version of every page whose text changed (<c>text_hash</c>), with the fingerprints of its sentences; the earlier
    /// version stops being current. Writing the versions of the same run again changes nothing.
    /// </summary>
    public static async Task WriteVersionsAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, RunAmbientScope scope, IReadOnlyList<RunPage> pages,
        IReadOnlyDictionary<string, IReadOnlyList<long>> fingerprints, DateTimeOffset now, CancellationToken ct)
    {
        var current = new Dictionary<Guid, (Guid Id, byte[]? Hash)>();
        await using (var select = new NpgsqlCommand(
            "SELECT page_id, id, text_hash FROM content.page_versions WHERE shop_id = $1 AND is_current AND page_id = ANY($2)", connection, transaction)
        {
            Parameters =
            {
                new NpgsqlParameter { Value = scope.ShopId },
                new NpgsqlParameter { Value = pages.Select(p => p.PageId).Distinct().ToArray() },
            },
        })
        await using (var reader = await select.ExecuteReaderAsync(ct).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                current[reader.GetGuid(0)] = (reader.GetGuid(1), reader.IsDBNull(2) ? null : reader.GetFieldValue<byte[]>(2));
            }
        }

        foreach (var page in pages.DistinctBy(p => p.PageId))
        {
            var info = page.Record.Info;
            var hash = TextHashBytes(page.Record.TextHash);
            if (current.TryGetValue(page.PageId, out var existing) && existing.Hash is { } old && old.AsSpan().SequenceEqual(hash))
            {
                continue;
            }

            var versionId = Guid.CreateVersion7();
            await using var batch = new NpgsqlBatch(connection, transaction);
            batch.BatchCommands.Add(new NpgsqlBatchCommand(
                "UPDATE content.page_versions SET is_current = false, updated_at = now() WHERE shop_id = $1 AND page_id = $2 AND is_current AND run_id IS DISTINCT FROM $3")
            {
                Parameters =
                {
                    new NpgsqlParameter { Value = scope.ShopId },
                    new NpgsqlParameter { Value = page.PageId },
                    new NpgsqlParameter { Value = scope.RunId },
                },
            });
            batch.BatchCommands.Add(new NpgsqlBatchCommand(
                """
                INSERT INTO content.page_versions (id, tenant_id, shop_id, page_id, run_id, fetched_at, html_blob_key, extract_blob_key, text_hash,
                    visible_chars, checked_chars, navigation_chars, listing_chars, profile_skipped_chars, extraction_method, script_app,
                    text_not_loaded, segment_hashes, is_current, created_at, updated_at)
                VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18, true, $6, $6)
                ON CONFLICT (shop_id, page_id, run_id) DO NOTHING
                """)
            {
                Parameters =
                {
                    new NpgsqlParameter { Value = versionId },
                    new NpgsqlParameter { Value = scope.TenantId },
                    new NpgsqlParameter { Value = scope.ShopId },
                    new NpgsqlParameter { Value = page.PageId },
                    new NpgsqlParameter { Value = scope.RunId },
                    new NpgsqlParameter { Value = now },
                    new NpgsqlParameter { Value = Storage.BlobPageContentStore.HtmlKey(scope, page.Url).Value },
                    new NpgsqlParameter { Value = Storage.BlobPageContentStore.ExtractKey(scope, page.Url).Value },
                    new NpgsqlParameter { Value = hash },
                    new NpgsqlParameter { Value = info.VisibleTextChars },
                    new NpgsqlParameter { Value = info.CheckedTextChars },
                    new NpgsqlParameter { Value = info.NavigationTextChars },
                    new NpgsqlParameter { Value = info.ListingTextChars },
                    new NpgsqlParameter { Value = info.ProfileSkippedTextChars },
                    new NpgsqlParameter { Value = SnakeCase.Of(info.Extraction.ToString()) },
                    new NpgsqlParameter { Value = (object?)info.ScriptApp ?? DBNull.Value, NpgsqlDbType = NpgsqlDbType.Text },
                    new NpgsqlParameter { Value = info.TextNotLoaded },
                    new NpgsqlParameter { Value = (fingerprints.GetValueOrDefault(page.Url) ?? []).ToArray() },
                },
            });
            batch.BatchCommands.Add(new NpgsqlBatchCommand(
                """
                UPDATE content.pages SET current_version_id = (SELECT id FROM content.page_versions WHERE shop_id = $1 AND page_id = $2 AND run_id = $3),
                    updated_at = now()
                WHERE shop_id = $1 AND id = $2
                """)
            {
                Parameters =
                {
                    new NpgsqlParameter { Value = scope.ShopId },
                    new NpgsqlParameter { Value = page.PageId },
                    new NpgsqlParameter { Value = scope.RunId },
                },
            });
            await batch.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
    }

    /// <summary>The page type of the database for a page type of the library.</summary>
    public static DataPageType DataType(PageType type) => type switch
    {
        PageType.Home => DataPageType.Home,
        PageType.Product => DataPageType.Product,
        PageType.Legal => DataPageType.Legal,
        _ => DataPageType.Content,
    };

    private static byte[] TextHashBytes(string textHash)
    {
        var hex = textHash.StartsWith("sha256:", StringComparison.Ordinal) ? textHash[7..] : textHash;
        try
        {
            return Convert.FromHexString(hex);
        }
        catch (FormatException)
        {
            return SHA256.HashData(Encoding.UTF8.GetBytes(textHash));
        }
    }

    private static string? Truncate(string? text, int max) => text is null || text.Length <= max ? text : text[..max];
}
