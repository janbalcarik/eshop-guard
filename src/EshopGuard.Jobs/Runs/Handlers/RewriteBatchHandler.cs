using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using EshopGuard.Core.Fix;
using EshopGuard.Core.Pipeline;
using EshopGuard.Data.Entities.Checks;
using EshopGuard.Data.Entities.Ops;
using EshopGuard.Data.Entities.Usage;
using EshopGuard.Jobs.Processing;
using EshopGuard.Jobs.Runs.Storage;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using Finding = EshopGuard.Core.Models.Finding;

namespace EshopGuard.Jobs.Runs.Handlers;

/// <summary>
/// <c>run.rewrite</c>: a full analysis rewrites the pages with findings to rewrite in batches (<c>RewriteStep</c>, the new
/// text checked again by the rules, Jev included); the proposals go to <c>fixes.fix_proposals</c>. A free sample makes one
/// example fix: the most serious finding in a text is rewritten; when the rules still find it, the next one is tried, at
/// most <c>Runs:FreeSample:MaxExampleAttempts</c>; without success the summary says why.
/// </summary>
internal sealed class RewriteBatchHandler(RunHandlerContext context, RewriteStep rewrite) : RunJobHandler(context)
{
    public override string Kind => RunJobKinds.Rewrite;

    public override JobResourceClass ResourceClass => JobResourceClass.Llm;

    protected override IReadOnlyCollection<RunStatus> Statuses { get; } = [RunStatus.Rewriting];

    protected override async Task<JobResult> RunAsync(RunJob job, CancellationToken ct)
    {
        var run = job.Run;
        var payload = job.Job.Payload.RootElement;
        var batch = payload.GetProperty("batch").GetInt32();
        var input = await RunFiles.RequireAsync<RewriteInput>(Ctx.Blobs, job.Scope, RunFiles.Work, RulesHandler.RewriteFile(batch), ct).ConfigureAwait(false);
        var stored = await RunFiles.RequireAsync<List<StoredFinding>>(Ctx.Blobs, job.Scope, RunFiles.Work, RulesHandler.FindingIdsFile, ct).ConfigureAwait(false);
        var findingIds = stored.ToDictionary(s => s.Key, s => s.Id);
        job.Scope.JevOperation = UsageOperation.Recheck;

        var results = new List<RewriteResult>();
        RewriteResult? example = null;
        string? missing = null;
        if (run.IsSample)
        {
            foreach (var finding in input.Findings.Take(Ctx.Runs.FreeSample.MaxExampleAttempts))
            {
                var page = input.Pages.FirstOrDefault(p => finding.Urls.Contains(p.Url, StringComparer.Ordinal));
                if (page is null)
                {
                    continue;
                }

                var result = await rewrite.RewriteBatchAsync(new RewriteInput { Pages = [page], Findings = [finding], Country = input.Country, Jurisdictions = input.Jurisdictions }, null, ct).ConfigureAwait(false);
                results.Add(result);
                var change = result.Pages.FirstOrDefault()?.Changes.FirstOrDefault();
                if (result.Pages.FirstOrDefault()?.Error is not null || change is null)
                {
                    missing = RunCodes.RewriteFailed;
                    continue;
                }

                if (change.Status == RewriteStatus.StillFinding)
                {
                    missing = RunCodes.StillFinding;
                    continue;
                }

                example = new RewriteResult { Pages = [result.Pages[0]], Model = result.Model, PromptVersion = result.PromptVersion, Stats = result.Stats };
                missing = null;
                break;
            }

            missing ??= example is null ? RunCodes.NoRewritableFinding : null;
        }
        else
        {
            results.Add(await rewrite.RewriteBatchAsync(input, null, ct).ConfigureAwait(false));
        }

        var transient = results.SelectMany(r => r.Pages).Count(p => p.Error is not null && p.ErrorIsTransient);
        if ((!run.IsSample || example is null) && StepErrorPolicy.RepeatBatch(transient, job.Job))
        {
            // Rewrites received are in the cache: the repeated batch asks only for the pages without one.
            throw new TransientStepException(StepErrorPolicy.LlmUnavailable);
        }

        var merged = RewriteStep.Merge(results);
        await CompleteAsync(job, async (tx, locked) =>
        {
            var (connection, transaction) = (tx.Connection, tx.Transaction);
            var written = await ProposalWriter.WriteAsync(connection, transaction, job.Scope, run.IsSample ? example is null ? [] : [example] : results, findingIds, ct).ConfigureAwait(false);
            await Ctx.Usage.WriteAsync(connection, transaction, job.Scope,
                [new UsageEntry(UsageProvider.Openai, UsageOperation.Rewrite, merged.Model, Math.Max(0, merged.Stats.Pages - merged.Stats.FromCache - merged.Stats.Errors), merged.Stats.FromCache,
                    merged.Stats.InputTokens, merged.Stats.CachedTokens, merged.Stats.OutputTokens, 0, merged.Stats.CostUsd)], ct).ConfigureAwait(false);
            if (run.IsSample)
            {
                var sample = RunStore.Section(locked.Stats, "sample");
                sample["example_fix_proposal_id"] = written.FirstOrDefault() is { } id && id != Guid.Empty ? id.ToString("D") : null;
                sample["example_fix_missing_reason"] = written.Count > 0 ? null : missing ?? RunCodes.RewriteFailed;
                await RunStore.SetJsonAsync(connection, transaction, run.Id, "stats", locked.Stats, ct).ConfigureAwait(false);
            }

            if (await JevBarrier.AdvanceAsync(tx, locked, "rewrite", ct).ConfigureAwait(false))
            {
                await tx.EnqueueAsync(RunPlan.Finalize(locked), ct).ConfigureAwait(false);
            }
        }, ct).ConfigureAwait(false);
        Logger.LogInformation("run.rewrite_batch {RunId} {TenantId} {JobId} {Batch} {Pages} {Errors}", run.Id, run.TenantId, job.Job.Id, batch, merged.Stats.Pages, merged.Stats.Errors);
        return JobResult.Done;
    }
}

/// <summary>Writes the changes of rewrites as proposals (<c>fixes.fix_proposals</c>, one per changed passage of a page; a repeated batch adds nothing).</summary>
internal static class ProposalWriter
{
    /// <summary>Writes the proposals; returns their ids (the first is the example fix of a free sample).</summary>
    public static async Task<List<Guid>> WriteAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, RunAmbientScope scope, IReadOnlyList<RewriteResult> results,
        IReadOnlyDictionary<FindingKey, Guid> findingIds, CancellationToken ct)
    {
        var pages = results.SelectMany(r => r.Pages.Select(p => (Result: r, Page: p))).Where(p => p.Page.Error is null && p.Page.Changes.Count > 0).ToList();
        var ids = new List<Guid>();
        if (pages.Count == 0)
        {
            return ids;
        }

        // The page and its current version (the text the proposal was written for).
        var versions = new Dictionary<string, (Guid PageId, Guid VersionId)>(StringComparer.Ordinal);
        await using (var select = new NpgsqlCommand(
            """
            SELECT u.url, p.id, p.current_version_id FROM checks.run_urls u JOIN content.pages p ON p.id = u.page_id AND p.shop_id = $2
            WHERE u.run_id = $1 AND u.url = ANY($3) AND p.current_version_id IS NOT NULL
            """, connection, transaction)
        {
            Parameters =
            {
                new NpgsqlParameter { Value = scope.RunId },
                new NpgsqlParameter { Value = scope.ShopId },
                new NpgsqlParameter { Value = pages.Select(p => p.Page.Url).Distinct().ToArray() },
            },
        })
        await using (var reader = await select.ExecuteReaderAsync(ct).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                versions.TryAdd(reader.GetString(0), (reader.GetGuid(1), reader.GetGuid(2)));
            }
        }

        foreach (var (result, page) in pages)
        {
            if (!versions.TryGetValue(page.Url, out var version))
            {
                continue;
            }

            var findings = page.Findings.ToDictionary(f => f.Id, f => f.Finding, StringComparer.Ordinal);
            foreach (var change in page.Changes)
            {
                var (field, blockIndex) = Field(change.BlockIds.FirstOrDefault() ?? "");
                var linked = change.FindingIds.Select(id => findings.GetValueOrDefault(id)).OfType<Finding>()
                    .Select(f => findingIds.TryGetValue(FindingWriter.Key(f), out var id) ? id : (Guid?)null).OfType<Guid>().Distinct().ToArray();
                await using var insert = new NpgsqlCommand(
                    """
                    INSERT INTO fixes.fix_proposals (id, tenant_id, shop_id, page_id, page_version_id, finding_ids, field, block_index, original_text, proposed_text,
                        reason, placeholders, recheck_status, model, prompt_version, status, created_run_id, created_at, updated_at)
                    VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, 'proposed', $16, now(), now())
                    ON CONFLICT (shop_id, created_run_id, page_id, field, block_index) DO NOTHING
                    RETURNING id
                    """, connection, transaction)
                {
                    Parameters =
                    {
                        new NpgsqlParameter { Value = Guid.CreateVersion7() },
                        new NpgsqlParameter { Value = scope.TenantId },
                        new NpgsqlParameter { Value = scope.ShopId },
                        new NpgsqlParameter { Value = version.PageId },
                        new NpgsqlParameter { Value = version.VersionId },
                        new NpgsqlParameter { Value = linked },
                        new NpgsqlParameter { Value = field },
                        new NpgsqlParameter { Value = (object?)blockIndex ?? DBNull.Value, NpgsqlDbType = NpgsqlDbType.Integer },
                        new NpgsqlParameter { Value = change.Original },
                        new NpgsqlParameter { Value = change.Rewritten },
                        new NpgsqlParameter { Value = change.Reason },
                        new NpgsqlParameter { Value = JsonSerializer.Serialize(change.Placeholders, PipelineJson.Options), NpgsqlDbType = NpgsqlDbType.Jsonb },
                        new NpgsqlParameter { Value = Recheck(change.Status) },
                        new NpgsqlParameter { Value = result.Model },
                        new NpgsqlParameter { Value = result.PromptVersion },
                        new NpgsqlParameter { Value = scope.RunId },
                    },
                };
                if (await insert.ExecuteScalarAsync(ct).ConfigureAwait(false) is Guid id)
                {
                    ids.Add(id);
                }
            }
        }

        return ids;
    }

    /// <summary>The field of a block of the rewrite: main text blocks <c>B1…</c> with their index, title, meta and JSON-LD description, frame.</summary>
    public static (string Field, int? BlockIndex) Field(string blockId) => blockId switch
    {
        "TITLE" => ("name", null),
        "META" => ("short_description", null),
        "JSONLD" => ("description", null),
        _ when blockId.Length > 1 && blockId[0] == 'B' && int.TryParse(blockId.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var n) => ("block", n),
        _ => ("block", null),
    };

    private static string Recheck(RewriteStatus status) => status switch
    {
        RewriteStatus.Resolved or RewriteStatus.WaitingForFacts => "ok",
        RewriteStatus.StillFinding => "still_finding",
        _ => "pending",
    };
}
