using System.Text.Json;
using System.Text.Json.Nodes;
using EshopGuard.Application.Problems;
using EshopGuard.Billing.Contracts;
using EshopGuard.Billing.Jobs;
using EshopGuard.Billing.Pricing;
using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Connections;
using EshopGuard.Data.Entities.Billing;
using EshopGuard.Jobs.Queue;
using Npgsql;

namespace EshopGuard.Billing.Admin;

/// <summary>The outcome of a command run by the worker: done, refused with a code, or still running.</summary>
public sealed record AdminCommandOutcome(long JobId, string State, string? ErrorCode)
{
    public bool Done => State == "succeeded";

    public bool Pending => State is "queued" or "running";
}

/// <summary>
/// The admin API of price lists (task 3.6) on the side of the API: reads with <c>eshopguard_app</c>, the changes go to the worker
/// as the job <c>billing.price_list_admin</c> (the API has no write on the global price lists). Checks of the request repeat the
/// rules of <see cref="PriceListAdminService"/>, so a wrong request is refused before a job is created.
/// </summary>
public sealed class PriceListAdminApi(PriceListReader reader, IJobQueue queue, EshopGuardDataSource dataSource, TimeProvider time)
{
    public async Task<IReadOnlyList<PriceListDto>> ListAsync(string? marketCode, CancellationToken ct)
    {
        var lists = await reader.ListAsync(marketCode, ct).ConfigureAwait(false);
        var result = new List<PriceListDto>(lists.Count);
        foreach (var list in lists)
        {
            result.Add(ToDto((await reader.GetAsync(list.Id, ct).ConfigureAwait(false))!));
        }

        return result;
    }

    public async Task<PriceListDto> GetAsync(Guid id, CancellationToken ct) =>
        ToDto(await reader.GetAsync(id, ct).ConfigureAwait(false) ?? throw new DomainException(BillingCodes.PriceListNotFound, 404));

    public async Task<PriceListImpactDto?> ImpactAsync(Guid id, CancellationToken ct)
    {
        var list = await reader.GetAsync(id, ct).ConfigureAwait(false) ?? throw new DomainException(BillingCodes.PriceListNotFound, 404);
        if (list.List.Impact is not { } impact)
        {
            return null;
        }

        var root = impact.RootElement;
        return new PriceListImpactDto(
            root.GetProperty("computed_at").GetDateTimeOffset(), root.GetProperty("increase").GetInt32(), root.GetProperty("decrease").GetInt32(),
            root.GetProperty("unchanged").GetInt32(), Date(root, "first_increase_at"), Date(root, "first_decrease_at"), root.GetProperty("notice_days").GetInt32());
    }

    public async Task<(Guid Id, long JobId)> CreateDraftAsync(Guid actor, string marketCode, string currency, string? name, Guid? copyFrom, CancellationToken ct)
    {
        if (copyFrom is { } source && await reader.GetAsync(source, ct).ConfigureAwait(false) is null)
        {
            throw new DomainException(BillingCodes.PriceListNotFound, 404);
        }

        var id = Guid.CreateVersion7();
        var jobId = await EnqueueAsync(new JsonObject
        {
            ["command"] = PriceListAdminService.CreateDraft,
            ["price_list_id"] = BillingJobs.Id(id),
            ["actor_user_id"] = BillingJobs.Id(actor),
            ["market_code"] = marketCode.Trim().ToLowerInvariant(),
            ["currency"] = currency.Trim().ToUpperInvariant(),
            ["name"] = name,
            ["copy_from"] = copyFrom is { } c ? BillingJobs.Id(c) : null,
        }, ct).ConfigureAwait(false);
        return (id, jobId);
    }

    public async Task<long> SetTiersAsync(Guid actor, Guid id, IReadOnlyList<PriceTierInput> tiers, int? noticeDays, decimal? fairUseFactor, CancellationToken ct)
    {
        await RequireDraftAsync(id, ct).ConfigureAwait(false);
        if (PriceListAdminService.ValidateTiers(tiers) is { } problem)
        {
            throw new DomainException(BillingCodes.PriceListTiersInvalid, 400, new Dictionary<string, object?> { ["reason"] = problem });
        }

        return await EnqueueAsync(new JsonObject
        {
            ["command"] = PriceListAdminService.SetTiers,
            ["price_list_id"] = BillingJobs.Id(id),
            ["actor_user_id"] = BillingJobs.Id(actor),
            ["tiers"] = JsonSerializer.SerializeToNode(tiers, PriceListAdminService.Json),
            ["notice_days"] = noticeDays,
            ["fair_use_factor"] = fairUseFactor,
        }, ct).ConfigureAwait(false);
    }

    public async Task<long> SetDiscountsAsync(Guid actor, Guid id, IReadOnlyList<VolumeDiscountInput> discounts, CancellationToken ct)
    {
        await RequireDraftAsync(id, ct).ConfigureAwait(false);
        return await EnqueueAsync(new JsonObject
        {
            ["command"] = PriceListAdminService.SetDiscounts,
            ["price_list_id"] = BillingJobs.Id(id),
            ["actor_user_id"] = BillingJobs.Id(actor),
            ["discounts"] = JsonSerializer.SerializeToNode(discounts, PriceListAdminService.Json),
        }, ct).ConfigureAwait(false);
    }

    public async Task<long> RequestImpactAsync(Guid actor, Guid id, CancellationToken ct)
    {
        _ = await reader.GetAsync(id, ct).ConfigureAwait(false) ?? throw new DomainException(BillingCodes.PriceListNotFound, 404);
        return await EnqueueAsync(new JsonObject
        {
            ["command"] = PriceListAdminService.Impact, ["price_list_id"] = BillingJobs.Id(id), ["actor_user_id"] = BillingJobs.Id(actor),
        }, ct).ConfigureAwait(false);
    }

    public async Task<long> PublishAsync(Guid actor, Guid id, DateTimeOffset validFrom, CancellationToken ct)
    {
        var list = await RequireDraftAsync(id, ct).ConfigureAwait(false);
        if (validFrom < time.GetUtcNow().AddMinutes(-5))
        {
            throw new DomainException(BillingCodes.PriceListValidFromInvalid, 400);
        }

        if (TierResolver.Problem(list.Tiers.ToList()) is { } problem)
        {
            throw new DomainException(BillingCodes.PriceListTiersInvalid, 400, new Dictionary<string, object?> { ["reason"] = problem });
        }

        return await EnqueueAsync(new JsonObject
        {
            ["command"] = PriceListAdminService.Publish,
            ["price_list_id"] = BillingJobs.Id(id),
            ["actor_user_id"] = BillingJobs.Id(actor),
            ["valid_from"] = validFrom,
            ["request_id"] = BillingJobs.Id(Guid.CreateVersion7()),
        }, ct).ConfigureAwait(false);
    }

    /// <summary>The state of the job of a command; a refusal becomes the <see cref="DomainException"/> of its code.</summary>
    public async Task<AdminCommandOutcome> OutcomeAsync(long jobId, CancellationToken ct)
    {
        await using var connection = await dataSource.Source.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var command = new NpgsqlCommand("SELECT state, last_error FROM ops.jobs WHERE id = $1", connection) { Parameters = { new NpgsqlParameter { Value = jobId } } };
        await using var row = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await row.ReadAsync(ct).ConfigureAwait(false))
        {
            return new AdminCommandOutcome(jobId, "succeeded", null);
        }

        var state = row.GetString(0);
        var error = row.IsDBNull(1) ? null : row.GetString(1).Split(':')[0].Trim();
        if (state is "failed" or "canceled")
        {
            throw new DomainException(error ?? BillingCodes.Unavailable, StatusOf(error));
        }

        return new AdminCommandOutcome(jobId, state, null);
    }

    /// <summary>The HTTP status of a code of refusal of a command.</summary>
    public static int StatusOf(string? code) => code switch
    {
        BillingCodes.PriceListNotFound => 404,
        BillingCodes.PriceListNotEditable => 409,
        BillingCodes.PriceListTiersInvalid or BillingCodes.PriceListValidFromInvalid or ProblemCodes.ValidationFailed => 400,
        _ => 503,
    };

    public static PriceListDto ToDto(PriceListSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var list = snapshot.List;
        return new PriceListDto(
            list.Id, list.Name, list.MarketCode, list.Currency, list.ValidFrom, list.PublishedAt, list.ActivatedAt,
            SnakeCaseEnumConverter<PriceListStatus>.ToText(list.Status), SnakeCaseEnumConverter<PriceListSyncStatus>.ToText(list.SyncStatus), list.SyncError,
            list.StripeMode is { } mode ? SnakeCaseEnumConverter<StripeMode>.ToText(mode) : null, list.NoticeDays, list.FairUseOtherPagesFactor,
            snapshot.Tiers.Select(t => new PriceTierDto(t.Code, t.MinProducts, t.MaxProducts, t.AnalysisPrice, t.MonitoringMonthly, t.MonitoringYearly,
                TierResolver.IsCustom(t), t.StripePriceMonthly is not null, t.ArchivedAt is not null)).ToList(),
            snapshot.Discounts.Select(d => new VolumeDiscountDto(d.FromShopNumber, d.Percent)).ToList());
    }

    private async Task<PriceListSnapshot> RequireDraftAsync(Guid id, CancellationToken ct)
    {
        var list = await reader.GetAsync(id, ct).ConfigureAwait(false) ?? throw new DomainException(BillingCodes.PriceListNotFound, 404);
        return list.List.Status == PriceListStatus.Draft ? list : throw new DomainException(BillingCodes.PriceListNotEditable, 409);
    }

    private async Task<long> EnqueueAsync(JsonObject command, CancellationToken ct)
    {
        await using var connection = await dataSource.Source.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        var result = await queue.EnqueueAsync(BillingJobs.PriceListAdmin(command), transaction, ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
        return result.JobId;
    }

    private static DateTimeOffset? Date(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetDateTimeOffset() : null;
}
