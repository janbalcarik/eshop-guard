using System.Text.Json;
using System.Text.Json.Nodes;
using EshopGuard.Core.Languages;
using EshopGuard.Core.Pipeline;
using EshopGuard.Core.Rules;
using EshopGuard.Data.Entities.Checks;
using EshopGuard.Data.Entities.Ops;
using EshopGuard.Jobs.Processing;
using EshopGuard.Jobs.Runs.Storage;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace EshopGuard.Jobs.Runs.Handlers;

/// <summary>
/// <c>run.discover</c>: the rule sets are read first (invalid ones fail the run before anything is downloaded), then
/// robots.txt and the sitemaps of every crawl scope (the whole site for a free sample, the active language versions for a
/// full analysis), the frontiers into <c>checks.run_scopes</c> and the rough internal estimate. A free sample continues with
/// the places of sale (<c>run.markets</c>); a full analysis waits for its payment, or starts downloading at once when its
/// order is already paid. robots.txt forbidding everything or an address leading into an internal network fail the run.
/// </summary>
internal sealed class DiscoverHandler(RunHandlerContext context, IRuleSetProvider rules, DiscoveryStep discovery, IRunPaymentGate paymentGate)
    : RunJobHandler(context)
{
    public override string Kind => RunJobKinds.Discover;

    public override JobResourceClass ResourceClass => JobResourceClass.Fetch;

    protected override IReadOnlyCollection<RunStatus> Statuses { get; } = [RunStatus.Queued, RunStatus.Discovering];

    protected override async Task<JobResult> RunAsync(RunJob job, CancellationToken ct)
    {
        var run = job.Run;
        await rules.LoadAsync(ct).ConfigureAwait(false);
        if (run.Status == RunStatus.Queued)
        {
            await TransitionNowAsync(run, RunStatus.Queued, RunStatus.Discovering, ct).ConfigureAwait(false);
        }

        var (sites, skipped) = run.IsSample
            ? ([new SiteScope(new Uri(run.ShopBaseUrl))], [])
            : await InTenantAsync(run.TenantId, (c, t) => FullAnalysisScopesAsync(c, t, run, ct), ct).ConfigureAwait(false);
        var limits = run.IsSample
            ? new CrawlLimits(Ctx.Runs.FreeSample.MaxPages, Ctx.Runs.FreeSample.MaxPages, [], [], null)
            : new CrawlLimits(Ctx.Runs.FullAnalysis.MaxPages, Ctx.Runs.FullAnalysis.SampleProducts, [], [], null);

        var scopes = new List<RunScopeRow>();
        var results = new List<DiscoveryResult>();
        for (var i = 0; i < sites.Count; i++)
        {
            var site = sites[i];
            var result = await discovery.DiscoverAsync(new DiscoveryInput(site, limits), null, ct).ConfigureAwait(false);
            await RunFiles.PutAsync(Ctx.Blobs, job.Scope, RunFiles.Discovery, Scope(i), result, ct).ConfigureAwait(false);
            results.Add(result);
            scopes.Add(new RunScopeRow(site.SiteUrl.AbsoluteUri, site.SiteUrl.AbsoluteUri, site.Version?.ExpectedLanguage, site.Version, result.Robots, result.Frontier, result.Pace,
                result.Frontier.Stopped, 0));
        }

        // Nothing can be checked: every scope leads into an internal network, or robots.txt forbids it.
        if (results.All(r => r.HomeBlocked))
        {
            return await FailRunAsync(job, RunCodes.TargetNotAllowed, ct).ConfigureAwait(false);
        }

        if (results.All(r => r.HomeBlocked || r.Frontier.Stopped))
        {
            return await FailRunAsync(job, RunCodes.RobotsDisallowAll, ct).ConfigureAwait(false);
        }

        var planned = scopes.Sum(s => Math.Min(s.Frontier.MaxPages, s.Frontier.LegalQueue.Count + s.Frontier.ProductQueue.Count + s.Frontier.OtherQueue.Count) + 1);
        var estimate = InternalCostEstimator.Rough(planned, Ctx.Runs, Ctx.Guard, Ctx.Time.GetUtcNow());
        await CompleteAsync(job, async (tx, locked) =>
        {
            foreach (var scope in scopes)
            {
                await RunScopeStore.InsertAsync(tx.Connection, tx.Transaction, run.TenantId, run.Id, scope, ct).ConfigureAwait(false);
            }

            foreach (var (language, baseUrl, code) in skipped)
            {
                await RunEventWriter.WriteAsync(tx.Connection, tx.Transaction, run.TenantId, run.Id, "warning", RunCodes.EventVersionNotChecked,
                    new JsonObject { ["language"] = language, ["base_url"] = baseUrl, ["code"] = code }, ct).ConfigureAwait(false);
            }

            var progress = locked.Progress;
            progress["scopes_total"] = scopes.Count;
            progress["pages_planned"] = planned;
            progress["versions_not_checked"] = new JsonArray(skipped.Select(s => (JsonNode)new JsonObject { ["language"] = s.Language, ["base_url"] = s.BaseUrl, ["code"] = s.Code }).ToArray());
            await RunStore.SetJsonAsync(tx.Connection, tx.Transaction, run.Id, "progress", progress, ct).ConfigureAwait(false);
            var runEstimate = locked.Estimate;
            runEstimate["internal"] = estimate;
            await RunStore.SetJsonAsync(tx.Connection, tx.Transaction, run.Id, "estimate", runEstimate, ct).ConfigureAwait(false);

            if (locked.IsSample)
            {
                await tx.EnqueueAsync(RunPlan.Markets(locked), ct).ConfigureAwait(false);
                return;
            }

            if (await RunStateMachine.TryTransitionAsync(tx.Connection, tx.Transaction, run.TenantId, run.Id, RunStatus.Discovering, RunStatus.AwaitingPayment, ct).ConfigureAwait(false)
                && await paymentGate.IsPaidAsync(tx.Connection, tx.Transaction, run.Id, ct).ConfigureAwait(false))
            {
                // Paid while the discovery ran: the payment is not lost, the downloads start now.
                await RunTransitions.StartCrawlAsync(tx.Connection, tx.Transaction, Ctx.Queue, locked with { Status = RunStatus.AwaitingPayment }, RunStatus.AwaitingPayment, ct).ConfigureAwait(false);
            }
        }, ct).ConfigureAwait(false);
        Logger.LogInformation("run.discovered {RunId} {TenantId} {JobId} {Scopes} {Planned}", run.Id, run.TenantId, job.Job.Id, scopes.Count, planned);
        return JobResult.Done;
    }

    /// <summary>File name of the discovery of a scope.</summary>
    public static string Scope(int index) => $"scope-{index}";

    /// <summary>
    /// The active language versions of the e-shop (from its free sample, <c>shop.shop_languages</c>), otherwise the whole
    /// site. A version reached only by a cookie or Accept-Language has the addresses of another version; the worker of change
    /// 8 cannot keep two contents under one address, so such a version is not checked and the run says so.
    /// </summary>
    private static async Task<(List<SiteScope> Sites, List<(string Language, string BaseUrl, string Code)> Skipped)> FullAnalysisScopesAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, RunRow run, CancellationToken ct)
    {
        var versions = new List<(string Language, string BaseUrl, VersionCrawlScope? Scope)>();
        await using (var command = new NpgsqlCommand(
            "SELECT language, base_url, crawl_scope::text FROM shop.shop_languages WHERE shop_id = $1 AND status = 'active' ORDER BY base_url COLLATE \"C\", language",
            connection, transaction)
        {
            Parameters = { new NpgsqlParameter { Value = run.ShopId } },
        })
        await using (var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                versions.Add((reader.GetString(0), reader.GetString(1),
                    reader.IsDBNull(2) ? null : JsonSerializer.Deserialize<VersionCrawlScope>(reader.GetString(2), PipelineJson.Options)));
            }
        }

        if (versions.Count == 0)
        {
            return ([new SiteScope(new Uri(run.ShopBaseUrl))], []);
        }

        var sites = new List<SiteScope>();
        var skipped = new List<(string, string, string)>();
        foreach (var version in versions)
        {
            var sharesAddresses = version.Scope is { } scope && (scope.Cookies.Count > 0 || scope.AcceptLanguage is not null);
            if (sharesAddresses && versions.Count > 1)
            {
                skipped.Add((version.Language, version.BaseUrl, RunCodes.VersionSharedUrls));
                continue;
            }

            sites.Add(new SiteScope(new Uri(version.BaseUrl)) { Version = version.Scope });
        }

        return (sites, skipped);
    }
}
