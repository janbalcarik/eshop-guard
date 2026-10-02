using System.Text.Json.Nodes;
using EshopGuard.Application.Audit;
using EshopGuard.Application.Contracts;
using EshopGuard.Application.Identity;
using EshopGuard.Application.Identity.Validators;
using EshopGuard.Application.Localization;
using EshopGuard.Application.Problems;
using EshopGuard.Application.Tenants;
using EshopGuard.Data;
using EshopGuard.Data.Entities.Iam;
using EshopGuard.Data.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EshopGuard.Application.Me;

/// <summary>
/// The signed-in user (<c>/api/me</c>): his memberships in all tenants (policy <c>memberships_select_own</c>), the invitations
/// waiting for his e-mail (<c>invitations_select_own_email</c>), his language (AD 10) and sign-out everywhere. Every read runs
/// in a transaction of this user and no tenant.
/// </summary>
public sealed class MeService(
    EshopGuardDb db, UserManager<User> users, AccountService accounts, LocaleResolver locales, SecurityAuditWriter audit, RequestContext request, TimeProvider time)
{
    private static readonly string[] TenantFilter = [EshopGuardDb.TenantFilter];

    public Task<MeDto> GetAsync(Guid userId, CancellationToken ct) => db.ExecuteInUserTransactionAsync(async () =>
    {
        await db.SwitchTransactionContextAsync(TenantSql.NoTenant, userId, ct).ConfigureAwait(false);
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct).ConfigureAwait(false)
            ?? throw new DomainException(ProblemCodes.AuthUnauthenticated, 401);
        var memberships = await db.Memberships.IgnoreQueryFilters(TenantFilter).AsNoTracking()
            .Where(m => m.UserId == userId)
            .Join(db.Tenants, m => m.TenantId, t => t.Id, (m, t) => new { t.Id, t.Name, t.LegalName, t.MarketCode, t.Status, m.Role, m.CreatedAt })
            .Where(x => x.Status != TenantStatus.Deleted)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(ct).ConfigureAwait(false);
        var now = time.GetUtcNow();
        var memberOf = memberships.Select(m => m.Id).ToHashSet();
        var invitations = await db.Invitations.IgnoreQueryFilters(TenantFilter).AsNoTracking()
            .Where(i => i.Email == user.Email && i.AcceptedAt == null && i.ExpiresAt > now)
            .Join(db.Tenants, i => i.TenantId, t => t.Id, (i, t) => new { i.Id, i.TenantId, TenantName = t.Name, i.Role, i.ExpiresAt })
            .OrderBy(x => x.ExpiresAt)
            .ToListAsync(ct).ConfigureAwait(false);
        var googleLinked = await db.UserLogins.AnyAsync(l => l.UserId == userId && l.Provider == EgUserStore.GoogleProvider, ct).ConfigureAwait(false);
        var locale = await locales.ResolveAsync(user.Locale, request.AcceptLanguage, memberships.FirstOrDefault()?.MarketCode, ct).ConfigureAwait(false);

        return new MeDto(
            user.Id,
            user.Email,
            user.DisplayName,
            user.Locale,
            locale.Locale,
            locale.SourceCode,
            user.PasswordHash is not null,
            googleLinked,
            memberships.Select(m => new MeMembershipDto(m.Id, m.Name, m.LegalName, m.Role.Code(), m.Status.ToString().ToLowerInvariant())).ToList(),
            invitations.Where(i => !memberOf.Contains(i.TenantId))
                .Select(i => new PendingInvitationDto(i.Id, i.TenantName, i.Role.Code(), i.ExpiresAt)).ToList());
    }, ct);

    /// <summary><c>PATCH /api/me</c>: the displayed name (empty = none).</summary>
    public async Task<MeDto> UpdateAsync(Guid userId, string? displayName, CancellationToken ct)
    {
        var validation = new ValidationResult();
        var name = FieldValidators.Name(validation, "displayName", displayName, required: false);
        validation.ThrowIfInvalid();
        var user = await RequireAsync(userId).ConfigureAwait(false);
        if (user.DisplayName != name)
        {
            user.DisplayName = name;
            await accounts.UpdateAsync(user).ConfigureAwait(false);
        }

        return await GetAsync(userId, ct).ConfigureAwait(false);
    }

    /// <summary><c>PUT /api/me/locale</c>: an enabled language, or <c>null</c> back to the automatic one.</summary>
    public async Task SetLocaleAsync(Guid userId, string? locale, CancellationToken ct)
    {
        string? code = null;
        if (locale is not null)
        {
            var validation = new ValidationResult();
            code = FieldValidators.Code(validation, "locale", locale, required: true);
            validation.ThrowIfInvalid();
            if (!await locales.IsEnabledAsync(code!, ct).ConfigureAwait(false))
            {
                throw new DomainException(ProblemCodes.LocaleNotEnabled, 400, new Dictionary<string, object?> { ["locale"] = code });
            }
        }

        var user = await RequireAsync(userId).ConfigureAwait(false);
        if (user.Locale != code)
        {
            user.Locale = code;
            await accounts.UpdateAsync(user).ConfigureAwait(false);
        }
    }

    /// <summary><c>POST /api/auth/logout-everywhere</c>: a new security stamp ends every session within its check interval.</summary>
    public async Task LogoutEverywhereAsync(Guid userId, CancellationToken ct)
    {
        var user = await RequireAsync(userId).ConfigureAwait(false);
        await users.UpdateSecurityStampAsync(user).ConfigureAwait(false);
        await audit.WriteAsync(new AuditEvent(AuditActions.LogoutEverywhere, null, userId, "user", userId.ToString("D"),
            new JsonObject()), ct).ConfigureAwait(false);
    }

    private async Task<User> RequireAsync(Guid userId) =>
        await users.FindByIdAsync(userId.ToString("D")).ConfigureAwait(false) ?? throw new DomainException(ProblemCodes.AuthUnauthenticated, 401);
}
