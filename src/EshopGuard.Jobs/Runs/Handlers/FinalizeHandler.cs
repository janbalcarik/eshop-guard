using System.Diagnostics.Metrics;
using System.Text.Json;
using System.Text.Json.Nodes;
using EshopGuard.Core.Markets;
using EshopGuard.Core.Pipeline;
using EshopGuard.Data.Entities.Checks;
using EshopGuard.Data.Entities.Ops;
using EshopGuard.Jobs.Processing;
using EshopGuard.Jobs.Runs.Storage;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;

namespace EshopGuard.Jobs.Runs.Handlers;

/// <summary>
/// <c>run.finalize</c>: what was not checked (<see cref="UncheckedReport"/>) decides between <c>finished</c>, <c>partial</c>
/// and <c>failed</c> (no page checked); the numbers go to <c>runs.stats.unchecked</c>, the full list to the file store. A free
/// sample stores its scope basis (<c>estimate.basis</c>). The real cost is compared with the internal estimate (over
/// <c>Runs:CostAlertRatio</c> an operational warning; the run never stops for its cost). The e-shop, the notification and the
/// e-mail of the end (<c>ops.outbox</c>, when a member wants it) and the final event follow.
/// </summary>
internal sealed class FinalizeHandler(RunHandlerContext context) : RunJobHandler(context)
{
    private static readonly Meter Meter = new("EshopGuard.Runs");
    private static readonly Counter<long> CostOverEstimate = Meter.CreateCounter<long>("eshopguard.run.cost_over_estimate");

    public override string Kind => RunJobKinds.Finalize;

    public override JobResourceClass ResourceClass => JobResourceClass.System;

    protected override IReadOnlyCollection<RunStatus> Statuses { get; } = [RunStatus.Rewriting];

    protected override async Task<JobResult> RunAsync(RunJob job, CancellationToken ct)
    {
        var run = job.Run;
        var (urls, scopes, cost) = await InTenantAsync(run.TenantId, async (c, t) =>
        {
            var u = await RunPages.LoadUrlsAsync(c, t, run.Id, null, ct).ConfigureAwait(false);
            var s = await RunScopeStore.LoadAllAsync(c, t, run.Id, ct).ConfigureAwait(false);
            await using var sum = new NpgsqlCommand("SELECT coalesce(sum(cost_usd), 0) FROM usage.usage_records WHERE run_id = $1", c, t)
            {
                Parameters = { new NpgsqlParameter { Value = run.Id } },
            };
            return (u, s, (decimal)(await sum.ExecuteScalarAsync(ct).ConfigureAwait(false))!);
        }, ct).ConfigureAwait(false);

        var scan = RunStore.Section(run.Stats, "scan");
        var report = UncheckedReport.Build(urls, scopes.Select(s => s.Frontier.Counters).ToList(),
            (int)RunStore.Long(scan, "jev_errors"),
            (int)(RunStore.Long(scan, "sieve_errors") + RunStore.Long(scan, "sieve_too_long")),
            (int)RunStore.Long(scan, "pages_without_profile"));
        await RunFiles.PutAsync(Ctx.Blobs, job.Scope, "result", "unchecked", report.Items, ct).ConfigureAwait(false);
        var status = report.PagesChecked == 0 ? RunStatus.Failed : report.IsPartial ? RunStatus.Partial : RunStatus.Finished;
        var failure = status == RunStatus.Failed ? report.FailureCode(urls) : null;

        JsonObject? basis = null;
        if (run.IsSample)
        {
            var markets = await RunFiles.GetTextAsync(Ctx.Blobs, RunFiles.Key(job.Scope, RunFiles.Discovery, "markets.json.gz"), ct).ConfigureAwait(false);
            var analysis = markets is null ? null : JsonSerializer.Deserialize<MarketsAnalysisResult>(markets, PipelineJson.Options);
            var sitemap = await RunFiles.GetAsync<DiscoveryResult>(Ctx.Blobs, job.Scope, RunFiles.Discovery, DiscoverHandler.Scope(0), ct).ConfigureAwait(false);
            basis = ScopeBasisBuilder.Build(analysis, run.Id, sitemap?.SitemapEntries.Count ?? 0, Ctx.Guard.Markets.CountedMinOwnShare, Ctx.Time.GetUtcNow());
        }

        var estimated = RunStore.Decimal(RunStore.Section(run.Estimate, "internal"), "total_usd");
        if (estimated > 0 && cost > estimated * (decimal)Ctx.Runs.CostAlertRatio)
        {
            // An operational warning only: the price of the customer is guaranteed by the order, the run is not stopped.
            CostOverEstimate.Add(1);
            Logger.LogWarning("run.cost_over_estimate {RunId} {TenantId} {Ratio}", run.Id, run.TenantId, Math.Round(cost / estimated, 2));
        }

        await CompleteAsync(job, async (tx, locked) =>
        {
            var (connection, transaction) = (tx.Connection, tx.Transaction);
            locked.Stats["unchecked"] = report.ToJson();
            locked.Stats["pages_checked"] = report.PagesChecked;
            await RunStore.SetJsonAsync(connection, transaction, run.Id, "stats", locked.Stats, ct).ConfigureAwait(false);
            if (basis is not null)
            {
                locked.Estimate["basis"] = basis;
                await RunStore.SetJsonAsync(connection, transaction, run.Id, "estimate", locked.Estimate, ct).ConfigureAwait(false);
            }

            if (!await RunStateMachine.TryTransitionAsync(connection, transaction, run.TenantId, run.Id, RunStatus.Rewriting, status, ct, failure).ConfigureAwait(false))
            {
                return;
            }

            var (code, data) = status switch
            {
                RunStatus.Failed => (RunCodes.EventFailed, new JsonObject { ["code"] = failure }),
                RunStatus.Partial => (RunCodes.EventPartial, new JsonObject { ["unchecked"] = report.ToJson() }),
                _ => (RunCodes.EventFinished, new JsonObject { ["findings_by_severity"] = locked.Stats["findings_by_severity"]?.DeepClone() ?? new JsonObject() }),
            };
            await RunEventWriter.WriteAsync(connection, transaction, run.TenantId, run.Id, status == RunStatus.Failed ? "error" : "info", code, data, ct).ConfigureAwait(false);
            await UpdateShopAsync(connection, transaction, locked, report, ct).ConfigureAwait(false);
            await NotifyAsync(connection, transaction, locked, code, ct).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);
        Logger.LogInformation("run.finished {RunId} {TenantId} {JobId} {Status} {PagesChecked}", run.Id, run.TenantId, job.Job.Id, status, report.PagesChecked);
        return JobResult.Done;
    }

    private static async Task UpdateShopAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, RunRow run, UncheckedReport report, CancellationToken ct)
    {
        var products = RunStore.Section(RunStore.Section(run.Stats, "scan"), "pages_by_type");
        await using var command = new NpgsqlCommand(
            """
            UPDATE shop.shops SET last_run_at = now(),
                page_count = CASE WHEN $3 THEN $4 ELSE page_count END,
                product_count = CASE WHEN $3 THEN $5 ELSE product_count END,
                last_full_run_id = CASE WHEN $3 THEN $2 ELSE last_full_run_id END,
                status = CASE WHEN $3 AND status IN ('draft', 'sample', 'awaiting_payment', 'analyzing') THEN 'active' ELSE status END,
                updated_at = now()
            WHERE id = $1
            """, connection, transaction)
        {
            Parameters =
            {
                new NpgsqlParameter { Value = run.ShopId },
                new NpgsqlParameter { Value = run.Id },
                new NpgsqlParameter { Value = run.Kind == RunKind.FullAnalysis && report.PagesChecked > 0 },
                new NpgsqlParameter { Value = report.PagesChecked },
                new NpgsqlParameter { Value = (int)RunStore.Long(products, "product") },
            },
        };
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    /// <summary>A notification in the application and, for the members who want it, an e-mail through the outbox (change 11 sends it).</summary>
    private static async Task NotifyAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, RunRow run, string code, CancellationToken ct)
    {
        var parameters = new JsonObject { ["run_id"] = run.Id.ToString("D"), ["kind"] = JsonValue.Create(run.Kind.ToString()) };
        await using (var notification = new NpgsqlCommand(
            "INSERT INTO iam.notifications (id, tenant_id, shop_id, kind, params, created_at, updated_at) VALUES ($1, $2, $3, $4, $5, now(), now())",
            connection, transaction)
        {
            Parameters =
            {
                new NpgsqlParameter { Value = Guid.CreateVersion7() },
                new NpgsqlParameter { Value = run.TenantId },
                new NpgsqlParameter { Value = run.ShopId },
                new NpgsqlParameter { Value = code },
                new NpgsqlParameter { Value = parameters.ToJsonString(), NpgsqlDbType = NpgsqlDbType.Jsonb },
            },
        })
        {
            await notification.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        await using var email = new NpgsqlCommand(
            """
            INSERT INTO ops.outbox (tenant_id, kind, payload, attempts, created_at, updated_at)
            SELECT $1, 'email', jsonb_build_object('template', 'run_finished', 'run_id', $2::text, 'shop_id', $3::text, 'event', $4::text,
                       'user_ids', jsonb_agg(DISTINCT user_id)), 0, now(), now()
            FROM iam.notification_settings
            WHERE email_run_finished AND (shop_id IS NULL OR shop_id = $3)
            HAVING count(*) > 0
            """, connection, transaction)
        {
            Parameters =
            {
                new NpgsqlParameter { Value = run.TenantId },
                new NpgsqlParameter { Value = run.Id },
                new NpgsqlParameter { Value = run.ShopId },
                new NpgsqlParameter { Value = code },
            },
        };
        await email.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
}
