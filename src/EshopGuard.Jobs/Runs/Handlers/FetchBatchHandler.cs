using System.Diagnostics;
using System.Text.Json.Nodes;
using EshopGuard.Core.Pipeline;
using EshopGuard.Data.Entities.Checks;
using EshopGuard.Data.Entities.Ops;
using EshopGuard.Data.Entities.Usage;
using EshopGuard.Jobs.Processing;
using EshopGuard.Jobs.Workers;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace EshopGuard.Jobs.Runs.Handlers;

/// <summary>
/// <c>run.fetch</c>: one batch of downloads of one crawl scope (at most <c>Runs:FetchBatchPages</c> pages or
/// <c>Runs:FetchBatchSeconds</c>), with the lease of the domain in <c>ops.domains</c> (one job per domain across all runs
/// and tenants, the pace of the last batch kept there), robots.txt with Crawl-delay, the User-Agent of the settings and the
/// SSRF protection of the library. Every page is extracted right away (the HTML and the extraction go to the file store) and
/// its outcome goes to <c>checks.run_urls</c>; the frontier and the pace to <c>checks.run_scopes</c>. The batch that empties
/// the last scope moves the run to the profiles. While the home page is unreachable the batch waits
/// (<c>Runs:SiteOutageRetrySeconds</c>) up to <c>Runs:SiteOutageMaxHours</c>, then the scope ends and the run goes on.
/// </summary>
internal sealed class FetchBatchHandler(RunHandlerContext context, FetchStep fetch, ProfileStep profiles, IWorkerStore workers)
    : RunJobHandler(context)
{
    public override string Kind => RunJobKinds.Fetch;

    public override JobResourceClass ResourceClass => JobResourceClass.Fetch;

    protected override IReadOnlyCollection<RunStatus> Statuses { get; } = [RunStatus.Crawling];

    protected override async Task<JobResult> RunAsync(RunJob job, CancellationToken ct)
    {
        var run = job.Run;
        var payload = job.Job.Payload.RootElement;
        var scopeKey = payload.GetProperty("scope").GetString()!;
        var index = payload.GetProperty("index").GetInt32();
        var batchNo = payload.GetProperty("batch").GetInt32();
        var scope = (await InTenantAsync(run.TenantId, (c, t) => RunScopeStore.LoadAllAsync(c, t, run.Id, ct), ct).ConfigureAwait(false))
            .FirstOrDefault(s => s.ScopeKey == scopeKey);
        if (scope is null || scope.Exhausted)
        {
            return JobResult.Done;
        }

        var leaseTime = TimeSpan.FromSeconds(Ctx.Runs.DomainLeaseSeconds);
        var lease = await workers.TryAcquireDomainAsync(scope.Domain, job.Job.Id, leaseTime, ct).ConfigureAwait(false);
        if (lease is null)
        {
            await InTenantAsync(run.TenantId, (c, t) => RunEventWriter.WriteAsync(c, t, run.TenantId, run.Id, "info", RunCodes.EventWaitingDomain, new JsonObject(), ct), ct).ConfigureAwait(false);
            return new JobResult.Defer(TimeSpan.FromSeconds(5), RunCodes.EventWaitingDomain);
        }

        FetchBatchResult? result = null;
        try
        {
            var stored = await profiles.LoadStoredAsync(scope.Site, ct).ConfigureAwait(false);
            var pace = scope.Pace;
            if (lease.State is { LastRequestAt: { } last, Rate: > 0 } politeness)
            {
                // Another run downloaded the domain a moment ago: its pace holds across runs and tenants.
                var next = last + TimeSpan.FromSeconds(1 / politeness.Rate.Value);
                pace = pace with { NextRequestAt = pace.NextRequestAt is { } own && own > next ? own : next };
            }

            var requestsBefore = scope.Frontier.Counters.Requests;
            // The first batch lists also what the discovery left out (sitemap addresses forbidden by robots.txt).
            var robotsBefore = batchNo == 1 ? 0 : scope.Frontier.Counters.RobotsBlocked.Count;
            var ssrfBefore = batchNo == 1 ? 0 : scope.Frontier.Counters.SsrfBlocked.Count;
            var throttledBefore = pace.Throttled;
            var clock = Stopwatch.StartNew();
            result = await fetch.FetchBatchAsync(
                new FetchBatchInput(scope.Site, scope.Robots, scope.Frontier, pace, Ctx.Runs.FetchBatchPages, TimeSpan.FromSeconds(Ctx.Runs.FetchBatchSeconds), ExtractInline: true)
                {
                    StoredProfiles = stored,
                    PersistExtracts = true,
                },
                null, ct).ConfigureAwait(false);
            var seconds = clock.Elapsed.TotalSeconds;

            var homeDown = result.Pages.Any(p => p.IsHome && p.Outcome == FetchOutcome.Failed) && !result.Pages.Any(p => p.Outcome == FetchOutcome.Ok);
            if (homeDown && await WaitForSiteAsync(run, scopeKey, ct).ConfigureAwait(false))
            {
                return new JobResult.Defer(TimeSpan.FromSeconds(Ctx.Runs.SiteOutageRetrySeconds), RunCodes.EventSiteUnreachable);
            }

            var exhausted = result.FrontierExhausted || result.Frontier.Stopped || homeDown;
            if (homeDown)
            {
                result.Frontier.Stopped = true;
            }

            var requests = result.Frontier.Counters.Requests - requestsBefore;
            await CompleteAsync(job, async (tx, locked) =>
            {
                var (connection, transaction) = (tx.Connection, tx.Transaction);
                var now = Ctx.Time.GetUtcNow();
                var fetched = 0;
                for (var i = 0; i < result.Pages.Count; i++)
                {
                    var page = result.Pages[i];
                    if (page.Outcome == FetchOutcome.Duplicate)
                    {
                        continue;
                    }

                    fetched += await WriteUrlAsync(connection, transaction, scope, page, batchNo, i, now, ct).ConfigureAwait(false);
                }

                // Addresses the crawl left out without a request (robots.txt, internal network) are listed too (fail-closed).
                foreach (var (urls, before, state) in new[]
                {
                    (result.Frontier.Counters.RobotsBlocked, robotsBefore, RunUrlState.RobotsBlocked),
                    (result.Frontier.Counters.SsrfBlocked, ssrfBefore, RunUrlState.SsrfBlocked),
                })
                {
                    foreach (var url in urls.Skip(before).Distinct(StringComparer.Ordinal))
                    {
                        await RunPages.UpsertUrlAsync(connection, transaction, run.TenantId, run.Id, RunPages.UrlHash(url, scope.Scope),
                            new RunUrlRow(scope.ScopeKey, url, scope.Language, state, null, null, batchNo, state == RunUrlState.RobotsBlocked ? "robots_blocked" : "ssrf_blocked", null), ct).ConfigureAwait(false);
                    }
                }

                await RunScopeStore.SaveBatchAsync(connection, transaction, run.Id, scopeKey, result.Frontier, result.Pace, exhausted, ct).ConfigureAwait(false);
                var progress = locked.Progress;
                progress["pages_fetched"] = RunStore.Long(progress, "pages_fetched") + fetched;
                progress["pages_processed"] = RunStore.Long(progress, "pages_processed") + fetched;
                progress["crawl_requests"] = RunStore.Long(progress, "crawl_requests") + requests;
                progress["crawl_seconds"] = Math.Round(RunStore.Decimal(progress, "crawl_seconds") + (decimal)seconds, 1);
                await RunStore.SetJsonAsync(connection, transaction, run.Id, "progress", progress, ct).ConfigureAwait(false);
                await Ctx.Usage.WriteAsync(connection, transaction, job.Scope,
                    [new UsageEntry(UsageProvider.Crawl, UsageOperation.Fetch, null, requests, 0, 0, 0, 0, 0, 0m)], ct).ConfigureAwait(false);
                await RunEventWriter.WriteAsync(connection, transaction, run.TenantId, run.Id, "info", RunCodes.EventProgress,
                    new JsonObject { ["step"] = "fetch", ["done"] = progress["pages_fetched"]!.DeepClone(), ["total"] = progress["pages_planned"]?.DeepClone() }, ct).ConfigureAwait(false);
                if (result.Pace.Throttled > throttledBefore)
                {
                    await RunEventWriter.WriteAsync(connection, transaction, run.TenantId, run.Id, "warning", RunCodes.EventThrottled,
                        new JsonObject { ["retry_after_s"] = result.Pace.NextRequestAt is { } at ? Math.Max(0, Math.Round((at - now).TotalSeconds)) : null }, ct).ConfigureAwait(false);
                }

                if (!exhausted)
                {
                    await tx.EnqueueAsync(RunPlan.Fetch(locked, index, scopeKey, scope.Domain, batchNo + 1), ct).ConfigureAwait(false);
                    return;
                }

                // The last scope emptied: the barrier of the crawl (the run row is locked, so exactly one batch gets here).
                await using var open = new NpgsqlCommand("SELECT count(*) FROM checks.run_scopes WHERE run_id = $1 AND NOT exhausted", connection, transaction)
                {
                    Parameters = { new NpgsqlParameter { Value = run.Id } },
                };
                if ((long)(await open.ExecuteScalarAsync(ct).ConfigureAwait(false))! == 0
                    && await RunStateMachine.TryTransitionAsync(connection, transaction, run.TenantId, run.Id, RunStatus.Crawling, RunStatus.Profiling, ct).ConfigureAwait(false))
                {
                    await tx.EnqueueAsync(RunPlan.Profile(locked), ct).ConfigureAwait(false);
                }
            }, ct).ConfigureAwait(false);
            Logger.LogInformation("run.fetch_batch {RunId} {TenantId} {JobId} {Scope} {Batch} {Pages} {Requests} {Exhausted}",
                run.Id, run.TenantId, job.Job.Id, index, batchNo, result.Pages.Count, requests, exhausted);
            return JobResult.Done;
        }
        finally
        {
            var rate = result?.Pace.Rate ?? scope.Pace.Rate;
            var delay = result?.Frontier.Counters.CrawlDelaySeconds ?? scope.Frontier.Counters.CrawlDelaySeconds;
            await workers.ReleaseDomainAsync(scope.Domain, job.Job.Id,
                lease.State with { LastRequestAt = Ctx.Time.GetUtcNow(), Rate = rate, CrawlDelayMs = delay is { } d ? (int)(d * 1000) : lease.State.CrawlDelayMs },
                CancellationToken.None).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Writes the outcome of one address (and its page when it was read); returns 1 for a page downloaded. A planned address
    /// that redirected is marked <c>fetched</c>: its page is recorded under the final address.
    /// </summary>
    private static async Task<int> WriteUrlAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, RunScopeRow scope, FetchedPage page, int batchNo, int index, DateTimeOffset now, CancellationToken ct)
    {
        var scopeContext = RunAmbient.Required;
        var (state, code) = RunPages.Outcome(page);
        var read = page.Outcome == FetchOutcome.Ok;
        var url = read ? page.FinalUrl.AbsoluteUri : page.RequestedUrl.AbsoluteUri;
        var hash = RunPages.UrlHash(url, scope.Scope);
        Guid? pageId = null;
        if (read && page.Extract is { Status: ExtractionStatus.Ok } extract)
        {
            pageId = await RunPages.UpsertPageAsync(connection, transaction, scopeContext.TenantId, scopeContext.ShopId, hash, extract, page.ETag, page.LastModified, now, ct).ConfigureAwait(false);
        }

        var seq = checked((batchNo * 100_000) + index);
        await RunPages.UpsertUrlAsync(connection, transaction, scopeContext.TenantId, scopeContext.RunId, hash,
            new RunUrlRow(scope.ScopeKey, url, scope.Language ?? page.Extract?.Info.Language, state, page.IsHome ? "home" : null, seq, batchNo, code, pageId, page.HttpStatus), ct).ConfigureAwait(false);
        if (page.RequestedUrl.AbsoluteUri != url)
        {
            await using var redirected = new NpgsqlCommand(
                "UPDATE checks.run_urls SET state = 'fetched', page_id = $4, batch_no = $5, updated_at = now() WHERE run_id = $1 AND scope_key = $2 AND url_hash = $3 AND state = 'pending'",
                connection, transaction)
            {
                Parameters =
                {
                    new NpgsqlParameter { Value = scopeContext.RunId },
                    new NpgsqlParameter { Value = scope.ScopeKey },
                    new NpgsqlParameter { Value = RunPages.UrlHash(page.RequestedUrl.AbsoluteUri, scope.Scope) },
                    new NpgsqlParameter { Value = (object?)pageId ?? DBNull.Value, NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Uuid },
                    new NpgsqlParameter { Value = batchNo },
                },
            };
            await redirected.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        return read ? 1 : 0;
    }

    /// <summary>
    /// The home page is unreachable: the first time the run says so; true while the outage is shorter than
    /// <c>Runs:SiteOutageMaxHours</c> (the batch waits and is repeated), false afterwards (the scope ends, its rest unchecked).
    /// </summary>
    private async Task<bool> WaitForSiteAsync(RunRow run, string scopeKey, CancellationToken ct)
    {
        var now = Ctx.Time.GetUtcNow();
        var since = await InTenantAsync(run.TenantId, async (c, t) =>
        {
            var locked = (await RunStore.LoadAsync(c, t, run.Id, forUpdate: true, ct).ConfigureAwait(false))!;
            var outages = RunStore.Section(locked.Progress, "site_unreachable_since");
            if (outages[scopeKey] is JsonValue value && value.TryGetValue<DateTimeOffset>(out var first))
            {
                return first;
            }

            outages[scopeKey] = now;
            await RunStore.SetJsonAsync(c, t, run.Id, "progress", locked.Progress, ct).ConfigureAwait(false);
            await RunEventWriter.WriteAsync(c, t, run.TenantId, run.Id, "warning", RunCodes.EventSiteUnreachable, new JsonObject { ["since"] = now }, ct).ConfigureAwait(false);
            return now;
        }, ct).ConfigureAwait(false);
        var waiting = now - since < TimeSpan.FromHours(Ctx.Runs.SiteOutageMaxHours);
        Logger.LogWarning("run.site_unreachable {RunId} {TenantId} {Since} {Waiting}", run.Id, run.TenantId, since, waiting);
        return waiting;
    }
}
