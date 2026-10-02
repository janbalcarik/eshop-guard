using EshopGuard.Application.Audit;
using EshopGuard.Application.Contracts;
using EshopGuard.Application.Identity.Validators;
using EshopGuard.Application.Localization;
using EshopGuard.Application.Options;
using EshopGuard.Application.Problems;
using EshopGuard.Data;
using EshopGuard.Data.Entities.Iam;
using EshopGuard.Data.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json.Nodes;

namespace EshopGuard.Application.Tenants;

/// <summary>
/// Tenants (customer accounts): the tenant of a new account (AD 11), further tenants of an agency up to
/// <c>Tenants:MaxOwnedPerUser</c>, reading and renaming with optimistic concurrency (<c>xmin</c>). A tenant and its owner
/// membership are written in one transaction: the membership under <c>app.tenant_id</c> of the new tenant (RLS).
/// </summary>
public sealed class TenantService(
    EshopGuardDb db, LocaleResolver locales, SecurityAuditWriter audit, IOptions<TenantsOptions> options, TimeProvider time)
{
    private static readonly string[] MembershipFilter = [EshopGuardDb.TenantFilter];

    /// <summary>
    /// In the open transaction of a new account: when no invitation waits for its e-mail (policy
    /// <c>invitations_select_own_email</c>), a tenant named by the e-mail with the market of the request (or the default one) and
    /// the membership <c>owner</c>; otherwise nothing (the invitation shows in <c>MeDto.pendingInvitations</c>).
    /// </summary>
    public async Task<Guid?> CreateForNewAccountAsync(User user, string? marketCode, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(user);
        await db.SwitchTransactionContextAsync(TenantSql.NoTenant, user.Id, ct).ConfigureAwait(false);
        var now = time.GetUtcNow();
        var invited = await db.Invitations.IgnoreQueryFilters(MembershipFilter)
            .AnyAsync(i => i.Email == user.Email && i.AcceptedAt == null && i.ExpiresAt > now, ct).ConfigureAwait(false);
        if (invited)
        {
            return null;
        }

        var market = await locales.MarketOrDefaultAsync(marketCode, ct).ConfigureAwait(false);
        var tenant = await CreateInTransactionAsync(user.Id, user.Email, market, ct).ConfigureAwait(false);
        return tenant.Id;
    }

    /// <summary><c>POST /api/tenants</c>: another tenant of the signed-in user (user scope), owner membership included.</summary>
    public async Task<TenantDto> CreateAsync(Guid userId, string? name, string? marketCode, CancellationToken ct)
    {
        var validation = new ValidationResult();
        var validName = FieldValidators.Name(validation, "name", name);
        var code = FieldValidators.Code(validation, "market", marketCode, required: true);
        validation.ThrowIfInvalid();
        var market = await locales.FindMarketAsync(code, ct).ConfigureAwait(false);
        if (market is null)
        {
            throw new DomainException(ProblemCodes.MarketUnknown, 400, new Dictionary<string, object?> { ["market"] = code });
        }

        return await db.ExecuteInUserTransactionAsync(async () =>
        {
            // Serializes the creations of one user, so two parallel requests cannot pass the cap together.
            await DbSql.ExecuteAsync(db, "SELECT pg_advisory_xact_lock(hashtextextended(@key, 0))", ct, DbSql.P("key", "tenants:" + userId.ToString("D")))
                .ConfigureAwait(false);
            var owned = await db.Memberships.IgnoreQueryFilters(MembershipFilter)
                .Where(m => m.UserId == userId && m.Role == MembershipRole.Owner)
                .Join(db.Tenants, m => m.TenantId, t => t.Id, (m, t) => t.Id)
                .CountAsync(ct).ConfigureAwait(false);
            if (owned >= options.Value.MaxOwnedPerUser)
            {
                throw new DomainException(ProblemCodes.TenantLimitReached, 409, new Dictionary<string, object?> { ["limit"] = options.Value.MaxOwnedPerUser });
            }

            var tenant = await CreateInTransactionAsync(userId, validName!, market, ct).ConfigureAwait(false);
            return ToDto(tenant, TenantRole.Owner);
        }, ct).ConfigureAwait(false);
    }

    /// <summary><c>GET /api/t/{tenantId}</c> (tenant set by the access filter).</summary>
    public async Task<TenantDto> GetAsync(TenantRole myRole, CancellationToken ct)
    {
        var tenantId = db.TenantContext.RequireTenantId();
        var tenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantId, ct).ConfigureAwait(false)
            ?? throw new DomainException(ProblemCodes.TenantNotFound, 404);
        return ToDto(tenant, myRole);
    }

    /// <summary><c>PATCH /api/t/{tenantId}</c>: a new name, only over the version the client has read (<c>409 concurrency.conflict</c>).</summary>
    public async Task<TenantDto> RenameAsync(Guid userId, TenantRole myRole, string? name, uint? version, CancellationToken ct)
    {
        var validation = new ValidationResult();
        var validName = FieldValidators.Name(validation, "name", name);
        if (version is null)
        {
            validation.Add("version", ProblemCodes.Fields.Required);
        }

        validation.ThrowIfInvalid();
        var tenantId = db.TenantContext.RequireTenantId();
        return await db.ExecuteInTenantTransactionAsync(async () =>
        {
            var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct).ConfigureAwait(false)
                ?? throw new DomainException(ProblemCodes.TenantNotFound, 404);
            db.Entry(tenant).Property(t => t.Version).OriginalValue = version!.Value;
            var previous = tenant.Name;
            tenant.Name = validName!;
            try
            {
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new DomainException(ProblemCodes.ConcurrencyConflict, 409);
            }

            if (previous != tenant.Name)
            {
                await audit.WriteAsync(new AuditEvent(AuditActions.TenantRenamed, tenantId, userId, "tenant", tenantId.ToString("D")), ct).ConfigureAwait(false);
            }

            await db.Entry(tenant).ReloadAsync(ct).ConfigureAwait(false);
            return ToDto(tenant, myRole);
        }, ct).ConfigureAwait(false);
    }

    public static TenantDto ToDto(Tenant tenant, TenantRole myRole)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        return new TenantDto(
            tenant.Id, tenant.Name, tenant.LegalName, tenant.CountryCode, tenant.MarketCode, tenant.Locale, tenant.Currency,
            tenant.Status.ToString().ToLowerInvariant(), myRole.Code(), tenant.Version);
    }

    private async Task<Tenant> CreateInTransactionAsync(Guid userId, string name, MarketInfo market, CancellationToken ct)
    {
        var tenant = new Tenant
        {
            Name = name,
            CountryCode = market.CountryCode,
            Locale = market.DefaultLocale,
            Currency = null,
            MarketCode = market.Code,
            Status = TenantStatus.Active,
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        await db.SwitchTransactionContextAsync(tenant.Id, userId, ct).ConfigureAwait(false);
        var now = time.GetUtcNow();
        await DbSql.ExecuteAsync(db,
            "INSERT INTO iam.memberships (tenant_id, user_id, role, created_at, updated_at) VALUES (@tenant, @user, 'owner', @now, @now)", ct,
            DbSql.P("tenant", tenant.Id), DbSql.P("user", userId), DbSql.P("now", now)).ConfigureAwait(false);
        await audit.WriteAsync(new AuditEvent(AuditActions.TenantCreated, tenant.Id, userId, "tenant", tenant.Id.ToString("D"),
            new JsonObject { ["marketCode"] = market.Code }), ct).ConfigureAwait(false);
        await db.SwitchTransactionContextAsync(TenantSql.NoTenant, userId, ct).ConfigureAwait(false);
        return tenant;
    }
}
