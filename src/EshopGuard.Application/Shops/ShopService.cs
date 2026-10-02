using System.Text.Json;
using System.Text.Json.Nodes;
using EshopGuard.Application.Audit;
using EshopGuard.Application.Contracts;
using EshopGuard.Application.Identity.Validators;
using EshopGuard.Application.Problems;
using EshopGuard.Application.RateLimits;
using EshopGuard.Data;
using EshopGuard.Data.Entities.Billing;
using EshopGuard.Data.Entities.Checks;
using EshopGuard.Data.Entities.Shops;
using EshopGuard.Data.Tenancy;
using EshopGuard.Jobs.Queue;
using EshopGuard.Jobs.Shops;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace EshopGuard.Application.Shops;

/// <summary>
/// E-shops of a tenant (change 10, AD 1, 2, 11): the list, one e-shop, the creation from an address with the recognition of
/// the platform enqueued in the same transaction, the name with optimistic concurrency (<c>xmin</c>) and the soft delete.
/// Every method runs in the tenant set by the access filter; RLS hides other tenants' e-shops (<c>404 shop.not_found</c>).
/// </summary>
public sealed class ShopService(
    EshopGuardDb db,
    ShopReader reader,
    ShopCatalog catalog,
    IJobQueue queue,
    AuthRateLimits limits,
    SecurityAuditWriter audit,
    IOptions<ShopsOptions> options,
    TimeProvider time)
{
    private static readonly RunStatus[] FinalRunStates = [RunStatus.Finished, RunStatus.Partial, RunStatus.Failed, RunStatus.Canceled];
    private static readonly SubscriptionStatus[] LiveSubscriptions = [SubscriptionStatus.Trialing, SubscriptionStatus.Active, SubscriptionStatus.PastDue];

    public async Task<IReadOnlyList<ShopListItemDto>> ListAsync(CancellationToken ct) => await db.ExecuteInTenantTransactionAsync(async () =>
    {
        var shops = await db.Shops.AsNoTracking().OrderBy(s => s.CreatedAt).ThenBy(s => s.Id)
            .Select(s => new
            {
                s.Id, s.Domain, s.Name, s.Platform, s.SourceMode, s.Status, s.LastRunAt,
                Connector = db.Connectors.Where(c => c.ShopId == s.Id).OrderByDescending(c => c.CreatedAt).Select(c => (ConnectorStatus?)c.Status).FirstOrDefault(),
                Languages = db.ShopLanguages.Where(l => l.ShopId == s.Id && l.Status == ShopLanguageStatus.Active).OrderBy(l => l.Language).Select(l => l.Language).ToList(),
            })
            .ToListAsync(ct).ConfigureAwait(false);
        return (IReadOnlyList<ShopListItemDto>)shops.Select(s => new ShopListItemDto(
            s.Id, s.Domain, s.Name, ShopReader.Text(s.Platform), ShopReader.Text(s.SourceMode), s.Connector is { } c ? ShopReader.Text(c) : null,
            ShopReader.Text(s.Status), s.LastRunAt, s.Languages)).ToList();
    }, ct).ConfigureAwait(false);

    public async Task<ShopDto> GetAsync(Guid shopId, CancellationToken ct) => await db.ExecuteInTenantTransactionAsync(async () =>
        await reader.DtoAsync(await reader.ReadAsync(shopId, ct).ConfigureAwait(false), ct).ConfigureAwait(false), ct).ConfigureAwait(false);

    /// <summary>
    /// <c>POST /shops</c>: the checked address (<c>400 shop.url_invalid</c>, <c>shop.url_not_allowed</c>), a duplicate among
    /// the not deleted e-shops of the tenant (<c>409 shop.already_exists</c> with <c>params.shopId</c>), then the e-shop
    /// (<c>draft</c>, platform <c>unknown</c>, <c>web</c>, every available module), its job <c>shop.detect_platform</c> and
    /// the audit in one transaction. Returns the e-shop and the id of the job to wait for.
    /// </summary>
    public async Task<(Guid ShopId, long JobId)> CreateAsync(Guid userId, string? url, CancellationToken ct)
    {
        var address = ShopUrlNormalizer.Normalize(url, options.Value.AllowedDevHosts);
        var tenantId = db.TenantContext.RequireTenantId();
        await limits.TakeAsync(ShopLimits.Create, tenantId.ToString("D"), ct).ConfigureAwait(false);
        var modules = ShopCatalog.Available(await catalog.ModulesAsync(ct).ConfigureAwait(false), null);
        try
        {
            return await db.ExecuteInTenantTransactionAsync(async () =>
            {
                await ThrowIfExistsAsync(address, ct).ConfigureAwait(false);
                var tenant = await db.Tenants.AsNoTracking().FirstAsync(t => t.Id == tenantId, ct).ConfigureAwait(false);
                var shop = new Shop
                {
                    Domain = address.Domain,
                    BaseUrl = address.BaseUrl,
                    BasePath = address.BasePath,
                    HomeCountry = tenant.CountryCode,
                    Platform = ShopPlatform.Unknown,
                    SourceMode = ShopSourceMode.Web,
                    Status = ShopStatus.Draft,
                    Modules = [.. modules],
                };
                db.Shops.Add(shop);
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
                var jobId = await EnqueueDetectionAsync(shop, ct).ConfigureAwait(false);
                await audit.WriteAsync(new AuditEvent(AuditActions.ShopCreated, tenantId, userId, "shop", shop.Id.ToString("D"),
                    new JsonObject { ["domain"] = shop.Domain, ["basePath"] = shop.BasePath }), ct).ConfigureAwait(false);
                return (shop.Id, jobId);
            }, ct).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Two requests for the same address at once: the second sees the first one's e-shop.
            await db.ExecuteInTenantTransactionAsync(() => ThrowIfExistsAsync(address, ct), ct).ConfigureAwait(false);
            throw new DomainException(ProblemCodes.ShopAlreadyExists, 409);
        }
    }

    /// <summary>
    /// Enqueues <c>shop.detect_platform</c> in the open transaction and marks the recognition <c>pending</c> with the id of
    /// the job. The caller holds the e-shop (tracked).
    /// </summary>
    public async Task<long> EnqueueDetectionAsync(Shop shop, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(shop);
        var enqueued = await queue.EnqueueAsync(ShopJobs.DetectPlatform(shop.TenantId, shop.Id), DbSql.Transaction(db), ct).ConfigureAwait(false);
        var previous = ShopDetectionState.Read(shop.Detection);
        var state = new ShopDetectionState
        {
            Status = ShopDetectionState.Pending,
            JobId = enqueued.JobId,
            RequestedAt = time.GetUtcNow(),
            PlatformSource = previous?.PlatformSource ?? ShopDetectionState.SourceDetected,
        };
        shop.Detection = JsonDocument.Parse(state.ToJson());
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return enqueued.JobId;
    }

    /// <summary><c>PATCH /shops/{shopId}</c>: a new name (empty clears it) over the version the client has read.</summary>
    public async Task<ShopDto> RenameAsync(Guid userId, Guid shopId, string? name, uint? version, CancellationToken ct)
    {
        var validation = new ValidationResult();
        var validName = FieldValidators.Name(validation, "name", name, required: false);
        if (version is null)
        {
            validation.Add("version", ProblemCodes.Fields.Required);
        }

        validation.ThrowIfInvalid();
        return await db.ExecuteInTenantTransactionAsync(async () =>
        {
            var shop = await reader.RequireAsync(shopId, ct).ConfigureAwait(false);
            db.Entry(shop).Property(s => s.Version).OriginalValue = version!.Value;
            var previous = shop.Name;
            shop.Name = string.IsNullOrWhiteSpace(validName) ? null : validName;
            await SaveConcurrentAsync(ct).ConfigureAwait(false);
            if (previous != shop.Name)
            {
                await audit.WriteAsync(new AuditEvent(AuditActions.ShopRenamed, shop.TenantId, userId, "shop", shop.Id.ToString("D")), ct).ConfigureAwait(false);
            }

            await db.Entry(shop).ReloadAsync(ct).ConfigureAwait(false);
            return await reader.DtoAsync(shop, ct).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// <c>DELETE /shops/{shopId}</c>: soft delete (<c>deleted_at</c>); refused with a live subscription
    /// (<c>409 shop.subscription_active</c>) or a running run (<c>409 shop.run_in_progress</c>). The same address can be
    /// added again afterwards (the unique index covers only not deleted e-shops).
    /// </summary>
    public async Task DeleteAsync(Guid userId, Guid shopId, CancellationToken ct) => await db.ExecuteInTenantTransactionAsync(async () =>
    {
        var shop = await reader.RequireAsync(shopId, ct, forUpdate: true).ConfigureAwait(false);
        if (await db.Subscriptions.AnyAsync(s => s.ShopId == shopId && LiveSubscriptions.Contains(s.Status), ct).ConfigureAwait(false))
        {
            throw new DomainException(ProblemCodes.ShopSubscriptionActive, 409);
        }

        await ThrowIfRunInProgressAsync(shopId, ct).ConfigureAwait(false);
        shop.DeletedAt = time.GetUtcNow();
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        await audit.WriteAsync(new AuditEvent(AuditActions.ShopDeleted, shop.TenantId, userId, "shop", shop.Id.ToString("D")), ct).ConfigureAwait(false);
    }, ct).ConfigureAwait(false);

    /// <summary><c>409 shop.run_in_progress</c> when a run of the e-shop has not ended.</summary>
    public async Task ThrowIfRunInProgressAsync(Guid shopId, CancellationToken ct)
    {
        if (await db.Runs.AnyAsync(r => r.ShopId == shopId && !FinalRunStates.Contains(r.Status), ct).ConfigureAwait(false))
        {
            throw new DomainException(ProblemCodes.ShopRunInProgress, 409);
        }
    }

    private async Task ThrowIfExistsAsync(ShopAddress address, CancellationToken ct)
    {
        var existing = await db.Shops.AsNoTracking().Where(s => s.Domain == address.Domain && s.BasePath == address.BasePath)
            .Select(s => (Guid?)s.Id).FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (existing is { } id)
        {
            throw new DomainException(ProblemCodes.ShopAlreadyExists, 409, new Dictionary<string, object?> { ["shopId"] = id });
        }
    }

    private async Task SaveConcurrentAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new DomainException(ProblemCodes.ConcurrencyConflict, 409);
        }
    }
}
