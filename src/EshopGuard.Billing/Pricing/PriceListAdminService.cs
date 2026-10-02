using System.Text.Json;
using System.Text.Json.Nodes;
using EshopGuard.Application.Problems;
using EshopGuard.Billing.Jobs;
using EshopGuard.Data.Connections;
using EshopGuard.Data.Entities.Billing;
using EshopGuard.Data.Tenancy;
using EshopGuard.Jobs.Queue;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace EshopGuard.Billing.Pricing;

/// <summary>A tier of a command <c>set_tiers</c>.</summary>
public sealed record PriceTierInput(string Code, int MinProducts, int? MaxProducts, decimal? AnalysisPrice, decimal? MonitoringMonthly, decimal? MonitoringYearly);

/// <summary>A volume discount of a command <c>set_discounts</c>.</summary>
public sealed record VolumeDiscountInput(int FromShopNumber, decimal Percent);

/// <summary>
/// The changes of global price lists (tasks 3.2, 3.3 and 3.6), run by the worker as the job <c>billing.price_list_admin</c>:
/// the API reads price lists only (<c>eshopguard_app</c> has no write on them) and the impact needs the subscriptions of all
/// tenants. A draft is a copy of the active price list; tiers and discounts change only in a draft; every change is audited
/// (<c>price_list.*</c>, without a tenant).
/// </summary>
public sealed class PriceListAdminService(EshopGuardDataSource dataSource, IJobQueue queue, TimeProvider time, ILogger<PriceListAdminService> logger)
{
    public const string CreateDraft = "create_draft";
    public const string SetTiers = "set_tiers";
    public const string SetDiscounts = "set_discounts";
    public const string Impact = "impact";
    public const string Publish = "publish";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    /// <summary>Runs one command of the payload; refusals are <see cref="DomainException"/> with the code.</summary>
    public async Task RunAsync(JsonDocument payload, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var root = payload.RootElement;
        var command = root.GetProperty("command").GetString();
        var id = BillingJobs.GuidOf(payload, "price_list_id");
        Guid? actor = root.TryGetProperty("actor_user_id", out var a) && a.ValueKind == JsonValueKind.String ? Guid.Parse(a.GetString()!) : null;
        switch (command)
        {
            case CreateDraft:
                await CreateDraftAsync(id, root.GetProperty("market_code").GetString()!, root.GetProperty("currency").GetString()!,
                    root.TryGetProperty("name", out var n) ? n.GetString() : null,
                    root.TryGetProperty("copy_from", out var c) && c.ValueKind == JsonValueKind.String ? Guid.Parse(c.GetString()!) : null, actor, ct).ConfigureAwait(false);
                break;
            case SetTiers:
                await SetTiersAsync(id, root.GetProperty("tiers").Deserialize<List<PriceTierInput>>(Json)!,
                    root.TryGetProperty("notice_days", out var d) && d.ValueKind == JsonValueKind.Number ? d.GetInt32() : null,
                    root.TryGetProperty("fair_use_factor", out var f) && f.ValueKind == JsonValueKind.Number ? f.GetDecimal() : null, actor, ct).ConfigureAwait(false);
                break;
            case SetDiscounts:
                await SetDiscountsAsync(id, root.GetProperty("discounts").Deserialize<List<VolumeDiscountInput>>(Json)!, actor, ct).ConfigureAwait(false);
                break;
            case Impact:
                await ComputeImpactAsync(id, ct).ConfigureAwait(false);
                break;
            case Publish:
                await RequestPublishAsync(id, root.GetProperty("valid_from").GetDateTimeOffset(), BillingJobs.GuidOf(payload, "request_id"), actor, ct).ConfigureAwait(false);
                break;
            default:
                throw new DomainException(ProblemCodes.ValidationFailed, 400);
        }
    }

    /// <summary>A draft for the market and currency: a copy of the given price list, else of the active one, else of the newest one.</summary>
    public async Task CreateDraftAsync(Guid id, string marketCode, string currency, string? name, Guid? copyFrom, Guid? actor, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        await using var connection = await dataSource.Source.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = await BillingSql.BeginGlobalAsync(connection, actor, ct).ConfigureAwait(false);
        if (await BillingSql.ScalarAsync<bool>(transaction, "SELECT EXISTS (SELECT 1 FROM billing.price_lists WHERE id = $1)", ct, id).ConfigureAwait(false))
        {
            return;
        }

        var source = copyFrom ?? await BillingSql.ScalarAsync<Guid?>(transaction,
            """
            SELECT id FROM billing.price_lists WHERE market_code = $1 AND currency = $2
            ORDER BY (status = 'published' AND valid_from <= $3) DESC, valid_from DESC, created_at DESC LIMIT 1
            """, ct, marketCode, currency, now).ConfigureAwait(false);
        var inserted = await BillingSql.ExecuteAsync(transaction,
            """
            INSERT INTO billing.price_lists (id, name, market_code, currency, valid_from, status, notice_days, sync_status, fair_use_other_pages_factor, created_at, updated_at)
            SELECT $1, $2, $3, $4, $5, 'draft', coalesce(s.notice_days, 30), 'pending', coalesce(s.fair_use_other_pages_factor, 2), $5, $5
            FROM (SELECT 1) one LEFT JOIN billing.price_lists s ON s.id = $6
            WHERE EXISTS (SELECT 1 FROM ref.markets m WHERE m.code = $3)
            """, ct, id, name ?? $"{marketCode.ToUpperInvariant()} {now:yyyy-MM-dd}", marketCode, currency, now, source).ConfigureAwait(false);
        if (inserted == 0)
        {
            throw new DomainException(ProblemCodes.ValidationFailed, 400, new Dictionary<string, object?> { ["field"] = "marketCode" });
        }

        if (source is { } from)
        {
            await BillingSql.ExecuteAsync(transaction,
                """
                INSERT INTO billing.price_tiers (price_list_id, code, min_products, max_products, analysis_price, monitoring_monthly, monitoring_yearly,
                                                 lookup_key_analysis, lookup_key_monthly, lookup_key_yearly, created_at, updated_at)
                SELECT $1, code, min_products, max_products, analysis_price, monitoring_monthly, monitoring_yearly,
                       lookup_key_analysis, lookup_key_monthly, lookup_key_yearly, $3, $3
                FROM billing.price_tiers WHERE price_list_id = $2
                """, ct, id, from, now).ConfigureAwait(false);
            await BillingSql.ExecuteAsync(transaction,
                "INSERT INTO billing.volume_discounts (price_list_id, from_shop_number, percent, created_at, updated_at) SELECT $1, from_shop_number, percent, $3, $3 FROM billing.volume_discounts WHERE price_list_id = $2",
                ct, id, from, now).ConfigureAwait(false);
        }

        await AuditAsync(transaction, actor, "price_list.created", id, new JsonObject { ["market"] = marketCode, ["currency"] = currency, ["copy_from"] = source?.ToString("D") }, now, ct)
            .ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Replaces the tiers of a draft (and its notice and fair use); the tiers must follow each other without a gap.</summary>
    public async Task SetTiersAsync(Guid id, IReadOnlyList<PriceTierInput> tiers, int? noticeDays, decimal? fairUseFactor, Guid? actor, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(tiers);
        var now = time.GetUtcNow();
        await using var connection = await dataSource.Source.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = await BillingSql.BeginGlobalAsync(connection, actor, ct).ConfigureAwait(false);
        var (market, currency) = await RequireDraftAsync(transaction, id, ct).ConfigureAwait(false);
        if (ValidateTiers(tiers) is { } problem)
        {
            throw new DomainException(BillingCodes.PriceListTiersInvalid, 400, new Dictionary<string, object?> { ["reason"] = problem });
        }

        if (noticeDays is < 0 or > 365 || fairUseFactor is <= 0 or > 100)
        {
            throw new DomainException(ProblemCodes.ValidationFailed, 400);
        }

        await BillingSql.ExecuteAsync(transaction, "DELETE FROM billing.price_tiers WHERE price_list_id = $1", ct, id).ConfigureAwait(false);
        foreach (var tier in tiers)
        {
            var priced = tier.AnalysisPrice is not null;
            await BillingSql.ExecuteAsync(transaction,
                """
                INSERT INTO billing.price_tiers (price_list_id, code, min_products, max_products, analysis_price, monitoring_monthly, monitoring_yearly,
                                                 lookup_key_analysis, lookup_key_monthly, lookup_key_yearly, created_at, updated_at)
                VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $11)
                """, ct, id, tier.Code, tier.MinProducts, tier.MaxProducts, tier.AnalysisPrice, tier.MonitoringMonthly, tier.MonitoringYearly,
                priced ? LookupKey(market, currency, tier.Code, PriceKinds.Analysis) : null,
                priced ? LookupKey(market, currency, tier.Code, PriceKinds.Monthly) : null,
                priced && tier.MonitoringYearly is not null ? LookupKey(market, currency, tier.Code, PriceKinds.Yearly) : null, now).ConfigureAwait(false);
        }

        await BillingSql.ExecuteAsync(transaction,
            "UPDATE billing.price_lists SET notice_days = coalesce($2, notice_days), fair_use_other_pages_factor = coalesce($3, fair_use_other_pages_factor), impact = NULL, impact_at = NULL, updated_at = $4 WHERE id = $1",
            ct, id, noticeDays, fairUseFactor, now).ConfigureAwait(false);
        await AuditAsync(transaction, actor, "price_list.tiers_set", id, new JsonObject { ["tiers"] = tiers.Count, ["notice_days"] = noticeDays }, now, ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Replaces the volume discounts of a draft (from the n-th e-shop, n ≥ 2, 0 &lt; percent ≤ 100).</summary>
    public async Task SetDiscountsAsync(Guid id, IReadOnlyList<VolumeDiscountInput> discounts, Guid? actor, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(discounts);
        if (discounts.Any(d => d.FromShopNumber < 2 || d.Percent is <= 0 or > 100) || discounts.Select(d => d.FromShopNumber).Distinct().Count() != discounts.Count)
        {
            throw new DomainException(ProblemCodes.ValidationFailed, 400, new Dictionary<string, object?> { ["field"] = "discounts" });
        }

        var now = time.GetUtcNow();
        await using var connection = await dataSource.Source.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = await BillingSql.BeginGlobalAsync(connection, actor, ct).ConfigureAwait(false);
        await RequireDraftAsync(transaction, id, ct).ConfigureAwait(false);
        await BillingSql.ExecuteAsync(transaction, "DELETE FROM billing.volume_discounts WHERE price_list_id = $1", ct, id).ConfigureAwait(false);
        foreach (var discount in discounts)
        {
            await BillingSql.ExecuteAsync(transaction,
                "INSERT INTO billing.volume_discounts (price_list_id, from_shop_number, percent, created_at, updated_at) VALUES ($1, $2, $3, $4, $4)",
                ct, id, discount.FromShopNumber, discount.Percent, now).ConfigureAwait(false);
        }

        await BillingSql.ExecuteAsync(transaction, "UPDATE billing.price_lists SET impact = NULL, impact_at = NULL, updated_at = $2 WHERE id = $1", ct, id, now).ConfigureAwait(false);
        await AuditAsync(transaction, actor, "price_list.discounts_set", id, new JsonObject { ["discounts"] = discounts.Count }, now, ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The impact of a draft on the running subscriptions of its market and currency (task 3.3): how many get more expensive,
    /// cheaper or stay, and the nearest dates of effect (an increase after <c>notice_days</c> and the locked price of founders, a
    /// decrease from the next period). Nothing changes in Stripe or in the subscriptions; the result is stored in the price list.
    /// </summary>
    public async Task ComputeImpactAsync(Guid id, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        await using var connection = await dataSource.Source.OpenConnectionAsync(ct).ConfigureAwait(false);
        string market;
        string currency;
        DateTimeOffset validFrom;
        int noticeDays;
        Dictionary<string, decimal?> prices;
        List<(Guid Tenant, DateTimeOffset? FounderUntil)> tenants;
        await using (var read = await connection.BeginTransactionAsync(ct).ConfigureAwait(false))
        {
            var head = await BillingSql.ListAsync(read, "SELECT market_code, currency, valid_from, notice_days FROM billing.price_lists WHERE id = $1",
                r => (r.GetString(0), r.GetString(1).Trim(), r.GetFieldValue<DateTimeOffset>(2), r.GetInt32(3)), ct, id).ConfigureAwait(false);
            if (head.Count == 0)
            {
                throw new DomainException(BillingCodes.PriceListNotFound, 404);
            }

            (market, currency, validFrom, noticeDays) = head[0];
            prices = (await BillingSql.ListAsync(read, "SELECT code, monitoring_monthly FROM billing.price_tiers WHERE price_list_id = $1",
                r => (r.GetString(0), r.Get<decimal?>(1)), ct, id).ConfigureAwait(false)).ToDictionary(t => t.Item1, t => t.Item2, StringComparer.Ordinal);
            tenants = await BillingSql.ListAsync(read, "SELECT id, founder_until FROM iam.tenants WHERE market_code = $1 AND deleted_at IS NULL",
                r => (r.GetGuid(0), r.Get<DateTimeOffset?>(1)), ct, market).ConfigureAwait(false);
            await read.CommitAsync(ct).ConfigureAwait(false);
        }

        int up = 0, down = 0, same = 0;
        DateTimeOffset? firstUp = null, firstDown = null;
        var start = validFrom > now ? validFrom : now;
        foreach (var (tenantId, founderUntil) in tenants)
        {
            await using var transaction = await TenantSql.BeginAsync(connection, tenantId, null, ct).ConfigureAwait(false);
            var subscriptions = await BillingSql.ListAsync(transaction,
                """
                SELECT s.tier_code, s.unit_price, coalesce(s.current_period_end, s.trial_end), coalesce(s.trial_end, s.current_period_start, s.created_at)
                FROM billing.subscriptions s JOIN billing.price_lists l ON l.id = s.price_list_id
                WHERE s.status IN ('trialing', 'active', 'past_due') AND l.market_code = $1 AND l.currency = $2
                """, r => (r.GetString(0), r.GetDecimal(1), r.Get<DateTimeOffset?>(2), r.GetFieldValue<DateTimeOffset>(3)), ct, market, currency).ConfigureAwait(false);
            await transaction.CommitAsync(ct).ConfigureAwait(false);
            foreach (var (tier, unitPrice, nextStart, anchor) in subscriptions)
            {
                var price = prices.GetValueOrDefault(tier);
                var next = nextStart ?? now;
                if (price is not { } newPrice || newPrice == unitPrice)
                {
                    same++;
                    continue;
                }

                if (newPrice > unitPrice)
                {
                    up++;
                    var threshold = Max(start, now.AddDays(noticeDays));
                    if (founderUntil is { } founder && founder > threshold)
                    {
                        threshold = founder;
                    }

                    var at = BillingPeriods.FirstStartOnOrAfter(anchor, next, threshold);
                    firstUp = firstUp is null || at < firstUp ? at : firstUp;
                }
                else
                {
                    down++;
                    var at = BillingPeriods.FirstStartOnOrAfter(anchor, next, start);
                    firstDown = firstDown is null || at < firstDown ? at : firstDown;
                }
            }
        }

        var impact = new JsonObject
        {
            ["computed_at"] = now,
            ["increase"] = up,
            ["decrease"] = down,
            ["unchanged"] = same,
            ["first_increase_at"] = firstUp,
            ["first_decrease_at"] = firstDown,
            ["notice_days"] = noticeDays,
        };
        await using (var write = await connection.BeginTransactionAsync(ct).ConfigureAwait(false))
        {
            await BillingSql.ExecuteAsync(write, "UPDATE billing.price_lists SET impact = $2, impact_at = $3 WHERE id = $1", ct, id, BillingSql.Json(impact), now).ConfigureAwait(false);
            await write.CommitAsync(ct).ConfigureAwait(false);
        }

        logger.LogInformation("price_list.impact {PriceListId} {Increase} {Decrease} {Unchanged}", id, up, down, same);
    }

    /// <summary>A request to publish a draft from <paramref name="validFrom"/>: the synchronization to Stripe as a job.</summary>
    public async Task RequestPublishAsync(Guid id, DateTimeOffset validFrom, Guid requestId, Guid? actor, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        if (validFrom < now.AddMinutes(-5))
        {
            throw new DomainException(BillingCodes.PriceListValidFromInvalid, 400);
        }

        await using var connection = await dataSource.Source.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = await BillingSql.BeginGlobalAsync(connection, actor, ct).ConfigureAwait(false);
        await RequireDraftAsync(transaction, id, ct).ConfigureAwait(false);
        var tiers = await BillingSql.ListAsync(transaction,
            "SELECT code, min_products, max_products, analysis_price, monitoring_monthly, monitoring_yearly FROM billing.price_tiers WHERE price_list_id = $1",
            r => new PriceTierInput(r.GetString(0), r.GetInt32(1), r.Get<int?>(2), r.Get<decimal?>(3), r.Get<decimal?>(4), r.Get<decimal?>(5)), ct, id).ConfigureAwait(false);
        if (ValidateTiers(tiers) is { } problem)
        {
            throw new DomainException(BillingCodes.PriceListTiersInvalid, 400, new Dictionary<string, object?> { ["reason"] = problem });
        }

        await BillingSql.ExecuteAsync(transaction,
            "UPDATE billing.price_lists SET valid_from = $2, sync_status = 'pending', sync_error = NULL, updated_at = $3 WHERE id = $1", ct, id, validFrom, now)
            .ConfigureAwait(false);
        await queue.EnqueueAsync(BillingJobs.SyncPriceList(id, requestId), transaction, ct).ConfigureAwait(false);
        await AuditAsync(transaction, actor, "price_list.publish_requested", id, new JsonObject { ["valid_from"] = validFrom }, now, ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
    }

    /// <summary>The code of the first problem of the tiers, or null (see <see cref="TierResolver.Problem"/>).</summary>
    public static string? ValidateTiers(IReadOnlyList<PriceTierInput> tiers)
    {
        ArgumentNullException.ThrowIfNull(tiers);
        return TierResolver.Problem(tiers.Select(t => new PriceTier
        {
            Code = t.Code, MinProducts = t.MinProducts, MaxProducts = t.MaxProducts, AnalysisPrice = t.AnalysisPrice, MonitoringMonthly = t.MonitoringMonthly,
            MonitoringYearly = t.MonitoringYearly,
        }).ToList());
    }

    /// <summary><c>{market}_{currency}_{tier}_{analysis|monthly|yearly}</c>, e.g. <c>sk_eur_t2000_monthly</c>.</summary>
    public static string LookupKey(string market, string currency, string tier, string kind) =>
        $"{market}_{currency}_{tier}_{kind}".ToLowerInvariant();

    private static async Task<(string Market, string Currency)> RequireDraftAsync(NpgsqlTransaction transaction, Guid id, CancellationToken ct)
    {
        var rows = await BillingSql.ListAsync(transaction, "SELECT market_code, currency, status FROM billing.price_lists WHERE id = $1 FOR UPDATE",
            r => (r.GetString(0), r.GetString(1).Trim(), r.GetString(2)), ct, id).ConfigureAwait(false);
        if (rows.Count == 0)
        {
            throw new DomainException(BillingCodes.PriceListNotFound, 404);
        }

        return rows[0].Item3 == "draft" ? (rows[0].Item1, rows[0].Item2) : throw new DomainException(BillingCodes.PriceListNotEditable, 409);
    }

    private static Task AuditAsync(NpgsqlTransaction transaction, Guid? actor, string action, Guid id, JsonObject data, DateTimeOffset at, CancellationToken ct) =>
        BillingSql.AuditAsync(transaction, null, actor, actor is null ? BillingSql.System : BillingSql.Admin, action, "price_list", id.ToString("D"), data, at, ct);

    private static DateTimeOffset Max(DateTimeOffset a, DateTimeOffset b) => a > b ? a : b;
}

/// <summary>The three prices of a tier.</summary>
public static class PriceKinds
{
    public const string Analysis = "analysis";
    public const string Monthly = "monthly";
    public const string Yearly = "yearly";
}
