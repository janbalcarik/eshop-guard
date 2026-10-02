using System.Text.Json.Nodes;
using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Connections;
using EshopGuard.Data.Entities.Checks;
using EshopGuard.Data.Entities.Ops;
using EshopGuard.Data.Tenancy;
using EshopGuard.Jobs.Queue;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;

namespace EshopGuard.Jobs.Runs;

/// <summary>Result of an operation of <see cref="IRunService"/>: the run, or the code of the error.</summary>
public sealed record RunServiceResult(Guid? RunId, string? ErrorCode)
{
    public bool Succeeded => ErrorCode is null;

    public static RunServiceResult Ok(Guid runId) => new(runId, null);

    public static RunServiceResult Error(string code) => new(null, code);
}

/// <summary>
/// Creates and drives runs for the API (change 10), the payments (change 12) and the administration. Every run is created
/// together with its first job in one transaction of the tenant (<see cref="ITenantContext"/> of the caller).
/// </summary>
public interface IRunService
{
    /// <summary>A free sample: once per domain across all tenants (<c>shop.claim_free_sample</c>), first job <c>run.discover</c> P0.</summary>
    Task<RunServiceResult> CreateFreeSampleAsync(Guid shopId, Guid? requestedBy, CancellationToken ct = default);

    /// <summary>A full analysis: verified ownership, jurisdictions and modules, scope basis of the last sample; then waits for payment.</summary>
    Task<RunServiceResult> CreateFullAnalysisAsync(Guid shopId, Guid? requestedBy, CancellationToken ct = default);

    /// <summary>The order was paid (Stripe, change 12): a run waiting for payment starts downloading; repeated calls change nothing.</summary>
    Task<RunServiceResult> MarkOrderPaidAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>Pilot: an administrator starts a full analysis without payment; written to the audit.</summary>
    Task<RunServiceResult> ApproveWithoutPaymentAsync(Guid runId, Guid adminId, string reason, CancellationToken ct = default);

    /// <summary>Requests the cancellation; the run ends <c>canceled</c> at its next step (at once when nothing of it runs).</summary>
    Task<RunServiceResult> RequestCancelAsync(Guid runId, Guid? userId, CancellationToken ct = default);

    /// <summary>Only creates a run <c>connector_check</c> for change 15 (its steps: K rozhodnutí 15).</summary>
    Task<RunServiceResult> CreateConnectorCheckAsync(Guid shopId, CancellationToken ct = default);
}

/// <inheritdoc cref="IRunService"/>
public sealed class RunService(
    EshopGuardDataSource dataSource,
    ITenantContext tenant,
    IJobQueue queue,
    IShopOwnershipPolicy ownership,
    IRunScopeResolver scopeResolver,
    IRunPaymentGate paymentGate,
    IOptions<RunsOptions> options,
    ILogger<RunService> logger) : IRunService
{
    /// <summary>The domain as the claim of the free sample keys it: lower case, without <c>www.</c> and a trailing dot.</summary>
    public static string NormalizeDomain(string domain)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        var normalized = domain.Trim().TrimEnd('.').ToLowerInvariant();
        return normalized.StartsWith("www.", StringComparison.Ordinal) ? normalized[4..] : normalized;
    }

    public async Task<RunServiceResult> CreateFreeSampleAsync(Guid shopId, Guid? requestedBy, CancellationToken ct = default)
    {
        var tenantId = tenant.RequireTenantId();
        await using var connection = await dataSource.Source.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = await TenantSql.BeginAsync(connection, tenantId, tenant.UserId, ct).ConfigureAwait(false);
        if (await ShopDomainAsync(connection, transaction, shopId, ct).ConfigureAwait(false) is not { } domain)
        {
            return RunServiceResult.Error(RunCodes.ShopNotFound);
        }

        await using (var claim = new NpgsqlCommand("SELECT shop.claim_free_sample($1, $2, $3)", connection, transaction)
        {
            Parameters =
            {
                new NpgsqlParameter { Value = NormalizeDomain(domain) },
                new NpgsqlParameter { Value = tenantId },
                new NpgsqlParameter { Value = shopId },
            },
        })
        {
            if (!(bool)(await claim.ExecuteScalarAsync(ct).ConfigureAwait(false))!)
            {
                // No details about who used the domain: another tenant's data is never revealed.
                return RunServiceResult.Error(RunCodes.SampleAlreadyUsed);
            }
        }

        var runId = await InsertRunAsync(connection, transaction, tenantId, shopId, RunKind.FreeSample, 0, requestedBy, [], options.Value.DefaultModules, null, ct).ConfigureAwait(false);
        await SetShopStatusAsync(connection, transaction, shopId, "sample", onlyFrom: ["draft"], ct).ConfigureAwait(false);
        var run = (await RunStore.LoadAsync(connection, transaction, runId, forUpdate: false, ct).ConfigureAwait(false))!;
        await queue.EnqueueAsync(RunPlan.Discover(run), transaction, ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
        logger.LogInformation("run.created {RunId} {TenantId} {Kind}", runId, tenantId, "free_sample");
        return RunServiceResult.Ok(runId);
    }

    public async Task<RunServiceResult> CreateFullAnalysisAsync(Guid shopId, Guid? requestedBy, CancellationToken ct = default)
    {
        var tenantId = tenant.RequireTenantId();
        if (await ownership.CheckAsync(tenantId, shopId, ct).ConfigureAwait(false) is { } refused)
        {
            return RunServiceResult.Error(refused);
        }

        await using var connection = await dataSource.Source.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = await TenantSql.BeginAsync(connection, tenantId, tenant.UserId, ct).ConfigureAwait(false);
        if (await ShopDomainAsync(connection, transaction, shopId, ct).ConfigureAwait(false) is null)
        {
            return RunServiceResult.Error(RunCodes.ShopNotFound);
        }

        // One full analysis of an e-shop at a time (the lock of the e-shop row orders concurrent requests).
        await using (var active = new NpgsqlCommand(
            """
            SELECT EXISTS (SELECT 1 FROM checks.runs WHERE shop_id = $1 AND kind = 'full_analysis'
                           AND status NOT IN ('finished', 'partial', 'failed', 'canceled'))
            FROM shop.shops WHERE id = $1 FOR UPDATE
            """, connection, transaction)
        {
            Parameters = { new NpgsqlParameter { Value = shopId } },
        })
        {
            if (await active.ExecuteScalarAsync(ct).ConfigureAwait(false) is true)
            {
                return RunServiceResult.Error(RunCodes.RunAlreadyActive);
            }
        }

        var scope = await scopeResolver.ResolveAsync(connection, transaction, shopId, null, ct).ConfigureAwait(false);
        var estimate = await LastSampleBasisAsync(connection, transaction, shopId, ct).ConfigureAwait(false);
        var runId = await InsertRunAsync(connection, transaction, tenantId, shopId, RunKind.FullAnalysis, 2, requestedBy, scope.Jurisdictions, scope.Modules, estimate, ct).ConfigureAwait(false);
        var run = (await RunStore.LoadAsync(connection, transaction, runId, forUpdate: false, ct).ConfigureAwait(false))!;
        await queue.EnqueueAsync(RunPlan.Discover(run), transaction, ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
        logger.LogInformation("run.created {RunId} {TenantId} {Kind}", runId, tenantId, "full_analysis");
        return RunServiceResult.Ok(runId);
    }

    public async Task<RunServiceResult> MarkOrderPaidAsync(Guid orderId, CancellationToken ct = default)
    {
        var tenantId = tenant.RequireTenantId();
        await using var connection = await dataSource.Source.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = await TenantSql.BeginAsync(connection, tenantId, tenant.UserId, ct).ConfigureAwait(false);
        Guid? runId;
        string status;
        await using (var order = new NpgsqlCommand("SELECT run_id, status FROM billing.orders WHERE id = $1", connection, transaction)
        {
            Parameters = { new NpgsqlParameter { Value = orderId } },
        })
        await using (var reader = await order.ExecuteReaderAsync(ct).ConfigureAwait(false))
        {
            if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                return RunServiceResult.Error(RunCodes.OrderRunMismatch);
            }

            runId = reader.IsDBNull(0) ? null : reader.GetGuid(0);
            status = reader.GetString(1);
        }

        if (status != "paid")
        {
            return RunServiceResult.Error(RunCodes.OrderNotPaid);
        }

        if (runId is not { } id || await RunStore.LoadAsync(connection, transaction, id, forUpdate: true, ct).ConfigureAwait(false) is not { } run
            || run.Kind != RunKind.FullAnalysis)
        {
            return RunServiceResult.Error(RunCodes.OrderRunMismatch);
        }

        // Paid before the end of the discovery: the gate is checked again when the discovery ends, nothing is lost.
        if (run.Status == RunStatus.AwaitingPayment && !run.CancelRequested)
        {
            await RunTransitions.StartCrawlAsync(connection, transaction, queue, run, RunStatus.AwaitingPayment, ct).ConfigureAwait(false);
        }

        await transaction.CommitAsync(ct).ConfigureAwait(false);
        return RunServiceResult.Ok(id);
    }

    public async Task<RunServiceResult> ApproveWithoutPaymentAsync(Guid runId, Guid adminId, string reason, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        var tenantId = tenant.RequireTenantId();
        await using var connection = await dataSource.Source.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = await TenantSql.BeginAsync(connection, tenantId, adminId, ct).ConfigureAwait(false);
        if (await RunStore.LoadAsync(connection, transaction, runId, forUpdate: true, ct).ConfigureAwait(false) is not { } run)
        {
            return RunServiceResult.Error(RunCodes.RunNotFound);
        }

        if (run.Kind != RunKind.FullAnalysis || run.Status is not (RunStatus.Queued or RunStatus.Discovering or RunStatus.AwaitingPayment))
        {
            return RunServiceResult.Error(RunCodes.NotAwaitingPayment);
        }

        await using (var audit = new NpgsqlCommand(
            """
            INSERT INTO ops.audit_log (tenant_id, at, actor_user_id, actor_kind, action, entity_type, entity_id, data, created_at)
            VALUES ($1, now(), $2, $3, $4, 'run', $5, $6, now())
            """, connection, transaction)
        {
            Parameters =
            {
                new NpgsqlParameter { Value = tenantId },
                new NpgsqlParameter { Value = adminId },
                new NpgsqlParameter { Value = SnakeCaseEnumConverter<AuditActorKind>.ToText(AuditActorKind.Admin) },
                new NpgsqlParameter { Value = OrderTablePaymentGate.ApprovalAction },
                new NpgsqlParameter { Value = runId.ToString("D") },
                new NpgsqlParameter { Value = new JsonObject { ["reason"] = reason }.ToJsonString(), NpgsqlDbType = NpgsqlDbType.Jsonb },
            },
        })
        {
            await audit.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        if (run.Status == RunStatus.AwaitingPayment && !run.CancelRequested && await paymentGate.IsPaidAsync(connection, transaction, runId, ct).ConfigureAwait(false))
        {
            await RunTransitions.StartCrawlAsync(connection, transaction, queue, run, RunStatus.AwaitingPayment, ct).ConfigureAwait(false);
        }

        await transaction.CommitAsync(ct).ConfigureAwait(false);
        logger.LogWarning("run.approved_without_payment {RunId} {TenantId} {AdminId}", runId, tenantId, adminId);
        return RunServiceResult.Ok(runId);
    }

    public async Task<RunServiceResult> RequestCancelAsync(Guid runId, Guid? userId, CancellationToken ct = default)
    {
        var tenantId = tenant.RequireTenantId();
        await using var connection = await dataSource.Source.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = await TenantSql.BeginAsync(connection, tenantId, userId, ct).ConfigureAwait(false);
        if (await RunStore.LoadAsync(connection, transaction, runId, forUpdate: true, ct).ConfigureAwait(false) is not { } run)
        {
            return RunServiceResult.Error(RunCodes.RunNotFound);
        }

        if (run.IsFinal)
        {
            return RunServiceResult.Error(RunCodes.AlreadyFinished);
        }

        // The flag first, then the waiting jobs: a continuation written at the same time either sees the flag or is canceled here.
        await using (var flag = new NpgsqlCommand("UPDATE checks.runs SET cancel_requested = true, updated_at = now() WHERE id = $1", connection, transaction)
        {
            Parameters = { new NpgsqlParameter { Value = runId } },
        })
        {
            await flag.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        await queue.CancelRunJobsAsync(runId, transaction, ct).ConfigureAwait(false);
        await using var running = new NpgsqlCommand("SELECT count(*) FROM ops.jobs WHERE run_id = $1 AND state = 'running'", connection, transaction)
        {
            Parameters = { new NpgsqlParameter { Value = runId } },
        };
        if ((long)(await running.ExecuteScalarAsync(ct).ConfigureAwait(false))! == 0)
        {
            // Nothing of the run is running, so no handler would see the flag: the run ends now.
            await RunTransitions.CancelAsync(connection, transaction, queue, run, ct).ConfigureAwait(false);
        }

        await transaction.CommitAsync(ct).ConfigureAwait(false);
        return RunServiceResult.Ok(runId);
    }

    public async Task<RunServiceResult> CreateConnectorCheckAsync(Guid shopId, CancellationToken ct = default)
    {
        var tenantId = tenant.RequireTenantId();
        await using var connection = await dataSource.Source.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = await TenantSql.BeginAsync(connection, tenantId, tenant.UserId, ct).ConfigureAwait(false);
        if (await ShopDomainAsync(connection, transaction, shopId, ct).ConfigureAwait(false) is null)
        {
            return RunServiceResult.Error(RunCodes.ShopNotFound);
        }

        var scope = await scopeResolver.ResolveAsync(connection, transaction, shopId, null, ct).ConfigureAwait(false);
        var runId = await InsertRunAsync(connection, transaction, tenantId, shopId, RunKind.ConnectorCheck, 1, null, scope.Jurisdictions, scope.Modules, null, ct, RunTrigger.Webhook).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
        return RunServiceResult.Ok(runId);
    }

    private static async Task<string?> ShopDomainAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid shopId, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("SELECT domain FROM shop.shops WHERE id = $1 AND deleted_at IS NULL", connection, transaction)
        {
            Parameters = { new NpgsqlParameter { Value = shopId } },
        };
        return await command.ExecuteScalarAsync(ct).ConfigureAwait(false) as string;
    }

    private static async Task<Guid> InsertRunAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid tenantId, Guid shopId, RunKind kind, short priority, Guid? requestedBy,
        IReadOnlyList<string> jurisdictions, IReadOnlyList<string> modules, JsonObject? estimate, CancellationToken ct, RunTrigger trigger = RunTrigger.User)
    {
        var runId = Guid.CreateVersion7();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO checks.runs (id, tenant_id, shop_id, kind, trigger, status, priority, requested_by, jurisdictions, modules,
                rule_set_ids, estimate, progress, cancel_requested, created_at, updated_at)
            VALUES ($1, $2, $3, $4, $5, 'queued', $6, $7, $8, $9, '{}', $10, '{}'::jsonb, false, now(), now())
            """, connection, transaction)
        {
            Parameters =
            {
                new NpgsqlParameter { Value = runId },
                new NpgsqlParameter { Value = tenantId },
                new NpgsqlParameter { Value = shopId },
                new NpgsqlParameter { Value = SnakeCaseEnumConverter<RunKind>.ToText(kind) },
                new NpgsqlParameter { Value = SnakeCaseEnumConverter<RunTrigger>.ToText(trigger) },
                new NpgsqlParameter { Value = priority },
                new NpgsqlParameter { Value = (object?)requestedBy ?? DBNull.Value, NpgsqlDbType = NpgsqlDbType.Uuid },
                new NpgsqlParameter { Value = jurisdictions.ToArray() },
                new NpgsqlParameter { Value = modules.ToArray() },
                new NpgsqlParameter { Value = (object?)estimate?.ToJsonString() ?? DBNull.Value, NpgsqlDbType = NpgsqlDbType.Jsonb },
            },
        };
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        return runId;
    }

    private static async Task SetShopStatusAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid shopId, string status, string[] onlyFrom, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("UPDATE shop.shops SET status = $2, updated_at = now() WHERE id = $1 AND status = ANY($3)", connection, transaction)
        {
            Parameters =
            {
                new NpgsqlParameter { Value = shopId },
                new NpgsqlParameter { Value = status },
                new NpgsqlParameter { Value = onlyFrom },
            },
        };
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    /// <summary>The scope basis of the last finished free sample of the e-shop (<c>estimate.basis</c>, with <c>source_run_id</c>).</summary>
    private static async Task<JsonObject?> LastSampleBasisAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid shopId, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT estimate -> 'basis' FROM checks.runs
            WHERE shop_id = $1 AND kind = 'free_sample' AND status IN ('finished', 'partial') AND estimate ? 'basis'
            ORDER BY finished_at DESC NULLS LAST LIMIT 1
            """, connection, transaction)
        {
            Parameters = { new NpgsqlParameter { Value = shopId } },
        };
        return await command.ExecuteScalarAsync(ct).ConfigureAwait(false) is string basis
            ? new JsonObject { ["basis"] = JsonNode.Parse(basis) }
            : null;
    }
}
