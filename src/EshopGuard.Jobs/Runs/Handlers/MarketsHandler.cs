using System.Text.Json;
using System.Text.Json.Nodes;
using EshopGuard.Core.Classify;
using EshopGuard.Core.Crawl;
using EshopGuard.Core.Languages;
using EshopGuard.Core.Markets;
using EshopGuard.Core.Pipeline;
using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Checks;
using EshopGuard.Data.Entities.Ops;
using EshopGuard.Data.Entities.Shops;
using EshopGuard.Data.Entities.Usage;
using EshopGuard.Jobs.Processing;
using EshopGuard.Jobs.Runs.Storage;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;

namespace EshopGuard.Jobs.Runs.Handlers;

/// <summary>
/// <c>run.markets</c> (free sample only): the places of sale and the language versions of change 7, with the estimate of the
/// model stored and checked against the cap of the sample before its first call; the suggested places of sale
/// (<c>shop.shop_markets</c>), the versions (<c>shop.shop_languages</c>), the jurisdictions of the run, and the plan of the
/// sample: at most <c>markets.sample_pages</c> pages of the checked versions (pairs, mandatory pages, random products), legal
/// pages of every version on top. Versions for unsupported markets are never downloaded. Then the downloads start.
/// </summary>
internal sealed class MarketsHandler(
    RunHandlerContext context,
    IMarketsAnalyzer analyzer,
    DiscoveryStep discovery,
    PageClassifier classifier,
    IRunScopeResolver scopeResolver,
    ILogger<MarketsHandler> logger) : RunJobHandler(context)
{
    public override string Kind => RunJobKinds.Markets;

    public override JobResourceClass ResourceClass => JobResourceClass.Llm;

    protected override IReadOnlyCollection<RunStatus> Statuses { get; } = [RunStatus.Discovering];

    protected override async Task<JobResult> RunAsync(RunJob job, CancellationToken ct)
    {
        var run = job.Run;
        var overBudget = false;
        var request = new MarketsAnalysisRequest(new Uri(run.ShopBaseUrl)) { Seed = Seed(run.Id) };
        var result = await analyzer.AnalyzeAsync(request, async (estimate, token) =>
        {
            // Stored before the first call; over the cap of the sample nothing is paid and the run fails below.
            var stored = await InTenantAsync(run.TenantId, async (c, t) =>
            {
                var locked = (await RunStore.LoadAsync(c, t, run.Id, forUpdate: true, token).ConfigureAwait(false))!;
                var runEstimate = locked.Estimate;
                var internalEstimate = InternalCostEstimator.OpenAi(RunStore.Section(runEstimate, "internal"), "market_usd", estimate.CostUsd, Ctx.Time.GetUtcNow());
                await RunStore.SetJsonAsync(c, t, run.Id, "estimate", runEstimate, token).ConfigureAwait(false);
                return internalEstimate;
            }, token).ConfigureAwait(false);
            overBudget = !InternalCostEstimator.WithinSampleBudget(stored, Ctx.Runs);
            return !overBudget;
        }, null, ct).ConfigureAwait(false);
        if (overBudget)
        {
            return await FailRunAsync(job, RunCodes.SampleBudgetExceeded, ct).ConfigureAwait(false);
        }

        await RunFiles.PutTextAsync(Ctx.Blobs, RunFiles.Key(job.Scope, RunFiles.Discovery, "markets.json.gz"), result.ToJson(), ct).ConfigureAwait(false);
        var plan = result.Plan;
        var checkedVersions = plan?.Checked ?? [];
        var rows = result.Versions.ToDictionary(v => VersionMarketPlanner.Key(v.Language, v.BaseUrl), StringComparer.Ordinal);
        var sharedUrls = checkedVersions.Count > 1
            ? checkedVersions.Where(v => rows.GetValueOrDefault(VersionMarketPlanner.Key(v.Language, v.BaseUrl))?.Scope is { } s && (s.Cookies.Count > 0 || s.AcceptLanguage is not null)).ToList()
            : [];
        var crawled = checkedVersions.Except(sharedUrls).ToList();

        // The scopes of the sample: the discovery of the run is reused for the whole site, other versions are discovered now.
        var mainDiscovery = await RunFiles.RequireAsync<DiscoveryResult>(Ctx.Blobs, job.Scope, RunFiles.Discovery, DiscoverHandler.Scope(0), ct).ConfigureAwait(false);
        var scopes = new List<(RunScopeRow Row, List<RunUrlRow> Planned)>();
        var budget = Math.Max(1, Ctx.Runs.FreeSample.MaxPages / Math.Max(1, crawled.Count));
        if (crawled.Count == 0)
        {
            // No version to check (the analysis failed or found none): the sample checks the site as given.
            scopes.Add((Row(new Uri(run.ShopBaseUrl).AbsoluteUri, null, null, mainDiscovery, Limit(mainDiscovery.Frontier, Ctx.Runs.FreeSample.MaxPages, mainDiscovery.Frontier.MaxProducts)), []));
        }

        foreach (var version in crawled)
        {
            var key = VersionMarketPlanner.Key(version.Language, version.BaseUrl);
            var scope = rows.GetValueOrDefault(key)?.Scope;
            var site = new SiteScope(new Uri(version.BaseUrl)) { Version = scope };
            var found = scope is null && UrlTools.IsSameSite(site.Home, mainDiscovery.Frontier.Home)
                ? mainDiscovery
                : await discovery.DiscoverAsync(new DiscoveryInput(site, new CrawlLimits(budget, budget, [], [], null)), null, ct).ConfigureAwait(false);
            var urls = result.SamplePlan?.Urls.Where(u => u.VersionKey == key).ToList() ?? [];
            var (frontier, planned) = urls.Any(u => u.Kind != "mandatory")
                ? Planned(found, urls, version.BaseUrl, version.Language)
                : (Limit(found.Frontier, budget, budget), []);
            scopes.Add((Row(site.SiteUrl.AbsoluteUri, version.Language, scope, found, frontier), planned));
        }

        var usage = result.Usage;
        var choice = default(RunScopeChoice);
        await CompleteAsync(job, async (tx, locked) =>
        {
            var (connection, transaction) = (tx.Connection, tx.Transaction);
            await WriteMarketsAsync(connection, transaction, run, result, ct).ConfigureAwait(false);
            await WriteLanguagesAsync(connection, transaction, run, result, crawled, ct).ConfigureAwait(false);
            choice = await scopeResolver.ResolveAsync(connection, transaction, run.ShopId, plan?.ActiveMarkets ?? [], ct).ConfigureAwait(false);
            await RunStore.SetScopeAsync(connection, transaction, run.Id, choice.Jurisdictions, choice.Modules, ct).ConfigureAwait(false);

            await using (var delete = new NpgsqlCommand("DELETE FROM checks.run_scopes WHERE run_id = $1", connection, transaction)
            {
                Parameters = { new NpgsqlParameter { Value = run.Id } },
            })
            {
                await delete.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }

            foreach (var (row, planned) in scopes)
            {
                await RunScopeStore.InsertAsync(connection, transaction, run.TenantId, run.Id, row, ct).ConfigureAwait(false);
                foreach (var url in planned)
                {
                    await RunPages.UpsertUrlAsync(connection, transaction, run.TenantId, run.Id, RunPages.UrlHash(url.Url, row.Scope), url, ct).ConfigureAwait(false);
                }
            }

            foreach (var version in sharedUrls)
            {
                await RunEventWriter.WriteAsync(connection, transaction, run.TenantId, run.Id, "warning", RunCodes.EventVersionNotChecked,
                    new JsonObject { ["language"] = version.Language, ["base_url"] = version.BaseUrl, ["code"] = RunCodes.VersionSharedUrls }, ct).ConfigureAwait(false);
            }

            var progress = locked.Progress;
            progress["scopes_total"] = scopes.Count;
            progress["pages_planned"] = scopes.Sum(s => s.Planned.Count > 0 ? s.Planned.Count + 1 : s.Row.Frontier.MaxPages);
            progress["versions_not_checked"] = new JsonArray(sharedUrls.Select(v => (JsonNode)new JsonObject { ["language"] = v.Language, ["base_url"] = v.BaseUrl, ["code"] = RunCodes.VersionSharedUrls }).ToArray());
            await RunStore.SetJsonAsync(connection, transaction, run.Id, "progress", progress, ct).ConfigureAwait(false);
            await Ctx.Usage.WriteAsync(connection, transaction, job.Scope,
                [
                    new UsageEntry(UsageProvider.Openai, UsageOperation.MarketAnalysis, result.Model, usage.Calls, 0, usage.InputTokens, usage.CachedTokens, usage.OutputTokens, 0, usage.CostUsd),
                    new UsageEntry(UsageProvider.Crawl, UsageOperation.Fetch, null, usage.Requests, 0, 0, 0, 0, 0, 0m),
                ], ct).ConfigureAwait(false);
            await RunTransitions.StartCrawlAsync(connection, transaction, Ctx.Queue, locked, RunStatus.Discovering, ct).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);
        logger.LogInformation("run.markets_analyzed {RunId} {TenantId} {JobId} {Versions} {Jurisdictions}", run.Id, run.TenantId, job.Job.Id, crawled.Count, choice?.Jurisdictions.Count ?? 0);
        return JobResult.Done;
    }

    /// <summary>A stable seed of the random sample of a run.</summary>
    private static int Seed(Guid runId) => BitConverter.ToInt32(runId.ToByteArray(), 0) & int.MaxValue;

    private static RunScopeRow Row(string key, string? language, VersionCrawlScope? scope, DiscoveryResult found, UrlFrontierState frontier) =>
        new(key, key, language, scope, found.Robots, frontier, found.Pace, frontier.Stopped, 0);

    /// <summary>
    /// The frontier of the sample of one version: the legal pages found so far, then the planned pages only (pairs and random
    /// products as products, mandatory pages as other pages) and no other links than legal ones from the home page.
    /// </summary>
    private (UrlFrontierState Frontier, List<RunUrlRow> Planned) Planned(DiscoveryResult found, IReadOnlyList<SampleUrl> urls, string scopeKey, string language)
    {
        var products = urls.Count(u => u.Kind != "mandatory");
        var state = Copy(found.Frontier, urls.Count + 1, products, linkMode: false);
        var frontier = new UrlFrontier(state, found.Robots.ToRobots(), Ctx.Guard.Crawl.ExcludeUrlPatterns, classifier, logger);
        var planned = new List<RunUrlRow>();
        foreach (var url in urls)
        {
            var queue = url.Kind switch { "pair" => "sample_pair", "mandatory" => "sample_mandatory", _ => "sample_random" };
            frontier.Consider(new Uri(url.Url), 0, productHint: url.Kind != "mandatory", foundOn: "plan");
            planned.Add(new RunUrlRow(scopeKey, url.Url, language, RunUrlState.Pending, queue, null, null, null, null));
        }

        return (state, planned);
    }

    /// <summary>The frontier of the discovery with other limits (they are fixed when a frontier is created).</summary>
    private static UrlFrontierState Limit(UrlFrontierState state, int maxPages, int maxProducts) => Copy(state, maxPages, maxProducts, state.LinkMode, keepQueues: true);

    private static UrlFrontierState Copy(UrlFrontierState state, int maxPages, int maxProducts, bool linkMode, bool keepQueues = false)
    {
        var copy = new UrlFrontierState
        {
            Home = state.Home,
            Scope = state.Scope,
            Cookies = new Dictionary<string, string>(state.Cookies),
            LinkMode = linkMode,
            HomeDone = state.HomeDone,
            Stopped = state.Stopped,
            MaxPages = maxPages,
            MaxProducts = maxProducts,
            MaxLinkDepth = state.MaxLinkDepth,
            Include = state.Include,
            Exclude = state.Exclude,
            LegalQueue = new Queue<FrontierItem>(state.LegalQueue),
            ProductQueue = keepQueues ? new Queue<FrontierItem>(state.ProductQueue) : new Queue<FrontierItem>(),
            OtherQueue = keepQueues ? new Queue<FrontierItem>(state.OtherQueue) : new Queue<FrontierItem>(),
            Visited = [.. state.Visited],
            Queued = keepQueues ? [.. state.Queued] : [.. state.LegalQueue.Select(i => UrlTools.Key(i.Url))],
            UncheckedKeys = [.. state.UncheckedKeys],
            Counters = state.Counters,
        };
        return copy;
    }

    private static async Task WriteMarketsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, RunRow run, MarketsAnalysisResult result, CancellationToken ct)
    {
        foreach (var market in result.Markets)
        {
            await using var command = new NpgsqlCommand(
                """
                INSERT INTO shop.shop_markets (tenant_id, shop_id, country_code, is_home, status, evidence_level, source, evidence, detection_run_id, created_at, updated_at)
                VALUES ($1, $2, $3, $4, $5, $6, 'detected', $7, $8, now(), now())
                ON CONFLICT (shop_id, country_code) DO UPDATE SET is_home = excluded.is_home, evidence_level = excluded.evidence_level,
                    evidence = excluded.evidence, detection_run_id = excluded.detection_run_id,
                    status = CASE WHEN shop.shop_markets.source = 'user' THEN shop.shop_markets.status ELSE excluded.status END,
                    updated_at = now()
                """, connection, transaction)
            {
                Parameters =
                {
                    new NpgsqlParameter { Value = run.TenantId },
                    new NpgsqlParameter { Value = run.ShopId },
                    new NpgsqlParameter { Value = market.CountryCode },
                    new NpgsqlParameter { Value = market.IsHome },
                    new NpgsqlParameter { Value = market.Status == "unsupported" ? "unsupported" : "suggested" },
                    new NpgsqlParameter { Value = market.EvidenceLevel is EvidenceLevels.Strong or EvidenceLevels.Delivery or EvidenceLevels.Generic ? market.EvidenceLevel : DBNull.Value, NpgsqlDbType = NpgsqlDbType.Text },
                    new NpgsqlParameter { Value = JsonSerializer.Serialize(market.Evidence, PipelineJson.Options), NpgsqlDbType = NpgsqlDbType.Jsonb },
                    new NpgsqlParameter { Value = run.Id },
                },
            };
            await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
    }

    private static async Task WriteLanguagesAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, RunRow run, MarketsAnalysisResult result, IReadOnlyList<PlannedVersion> crawled, CancellationToken ct)
    {
        var checkedKeys = crawled.Select(v => VersionMarketPlanner.Key(v.Language, v.BaseUrl)).ToHashSet(StringComparer.Ordinal);
        foreach (var version in result.Versions.OrderByDescending(v => v.IsMain).DistinctBy(v => v.Language ?? "und"))
        {
            var isChecked = checkedKeys.Contains(VersionMarketPlanner.Key(version.Language, version.BaseUrl));

            // "active" is what a full analysis downloads: the versions the plan checks; an active version not needed for the
            // markets is kept as excluded until the client ticks its market (change 10 recalculates the plan).
            var status = isChecked ? "active" : version.Status == VersionStatus.Active ? "excluded" : version.Status;
            await using var command = new NpgsqlCommand(
                """
                INSERT INTO shop.shop_languages (tenant_id, shop_id, language, base_url, switch_method, source, status, own_text_share, language_share,
                    comparison, sample_run_id, counted, product_count, crawl_scope, created_at, updated_at)
                VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, now(), now())
                ON CONFLICT (shop_id, language) DO UPDATE SET base_url = excluded.base_url, switch_method = excluded.switch_method,
                    source = excluded.source, own_text_share = excluded.own_text_share, language_share = excluded.language_share,
                    comparison = excluded.comparison, sample_run_id = excluded.sample_run_id, counted = excluded.counted,
                    product_count = excluded.product_count, crawl_scope = excluded.crawl_scope,
                    status = CASE WHEN shop.shop_languages.source = 'user' THEN shop.shop_languages.status ELSE excluded.status END,
                    updated_at = now()
                """, connection, transaction)
            {
                Parameters =
                {
                    new NpgsqlParameter { Value = run.TenantId },
                    new NpgsqlParameter { Value = run.ShopId },
                    new NpgsqlParameter { Value = version.Language ?? "und" },
                    new NpgsqlParameter { Value = version.BaseUrl },
                    new NpgsqlParameter { Value = version.SwitchMethod == SwitchMethods.Unknown ? DBNull.Value : version.SwitchMethod, NpgsqlDbType = NpgsqlDbType.Text },
                    new NpgsqlParameter { Value = version.Source },
                    new NpgsqlParameter { Value = status },
                    new NpgsqlParameter { Value = (object?)(float?)version.OwnTextShare ?? DBNull.Value, NpgsqlDbType = NpgsqlDbType.Real },
                    new NpgsqlParameter { Value = JsonSerializer.Serialize(version.LanguageShare, PipelineJson.Options), NpgsqlDbType = NpgsqlDbType.Jsonb },
                    new NpgsqlParameter { Value = version.Comparison is null ? DBNull.Value : JsonSerializer.Serialize(version.Comparison, PipelineJson.Options), NpgsqlDbType = NpgsqlDbType.Jsonb },
                    new NpgsqlParameter { Value = run.Id },
                    new NpgsqlParameter { Value = version.Counted },
                    new NpgsqlParameter { Value = (object?)version.ProductCount ?? DBNull.Value, NpgsqlDbType = NpgsqlDbType.Integer },
                    new NpgsqlParameter { Value = version.Scope is null ? DBNull.Value : JsonSerializer.Serialize(version.Scope, PipelineJson.Options), NpgsqlDbType = NpgsqlDbType.Jsonb },
                },
            };
            await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
    }
}
