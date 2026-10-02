using System.Text.Json.Nodes;
using EshopGuard.Application.Audit;
using EshopGuard.Application.Localization;
using EshopGuard.Application.Options;
using EshopGuard.Application.Problems;
using EshopGuard.Application.Security;
using EshopGuard.Application.Tenants;
using EshopGuard.Data;
using EshopGuard.Data.Entities.Iam;
using EshopGuard.Data.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EshopGuard.Application.Identity;

/// <summary>
/// Accounts behind every way of signing in (link, Google, invitation, reset): finding the account of an e-mail (a deleted one
/// is <c>403 auth.account_disabled</c>), creating it with a confirmed e-mail, the language of the request and the accepted
/// versions of the terms, and what a proven mailbox does to an existing account (confirmed e-mail, lockout ended, last sign-in).
/// Works in the caller's open transaction (user scope).
/// </summary>
public sealed class AccountService(
    EshopGuardDb db,
    UserManager<User> users,
    TenantService tenants,
    LocaleResolver locales,
    SecurityAuditWriter audit,
    RequestContext request,
    IpHasher hasher,
    IOptions<LegalOptions> legal,
    TimeProvider time)
{
    private static readonly string[] SoftDelete = [EshopGuardDb.SoftDeleteFilter];

    /// <summary>The account of a normalized e-mail, a live one first, a deleted one otherwise.</summary>
    public Task<User?> FindIncludingDeletedAsync(string email, CancellationToken ct) =>
        db.Users.IgnoreQueryFilters(SoftDelete)
            .Where(u => u.Email.ToLower() == email)
            .OrderBy(u => u.DeletedAt == null ? 0 : 1).ThenByDescending(u => u.CreatedAt)
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// The account of the e-mail, or a new one (<c>email_confirmed</c>, language by AD 10, audit <c>user.created</c> and
    /// <c>user.terms_accepted</c>) with its own tenant when <paramref name="withTenant"/> (AD 11). The transaction then runs as
    /// this user (<c>app.user_id</c>).
    /// </summary>
    public async Task<(User User, bool Created, Guid? TenantId)> FindOrCreateAsync(
        string email, string? marketCode, string method, bool withTenant, CancellationToken ct)
    {
        var user = await FindIncludingDeletedAsync(email, ct).ConfigureAwait(false);
        if (user is not null)
        {
            EnsureActive(user);
            await db.SwitchTransactionContextAsync(TenantSql.NoTenant, user.Id, ct).ConfigureAwait(false);
            return (user, false, null);
        }

        var locale = await locales.ResolveAsync(null, request.AcceptLanguage, marketCode, ct).ConfigureAwait(false);
        user = new User { Email = email, EmailConfirmed = true, Locale = locale.Locale };
        var result = await users.CreateAsync(user).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException("user.create_failed: " + string.Join(",", result.Errors.Select(e => e.Code)));
        }

        await db.SwitchTransactionContextAsync(TenantSql.NoTenant, user.Id, ct).ConfigureAwait(false);
        var emailHash = hasher.HashEmail(email);
        await audit.WriteAsync(new AuditEvent(AuditActions.UserCreated, null, user.Id, "user", user.Id.ToString("D"),
            new JsonObject { ["method"] = method, ["emailHash"] = emailHash, ["locale"] = user.Locale }), ct).ConfigureAwait(false);
        await audit.WriteAsync(new AuditEvent(AuditActions.TermsAccepted, null, user.Id, "user", user.Id.ToString("D"),
            new JsonObject { ["termsVersion"] = legal.Value.TermsVersion, ["privacyVersion"] = legal.Value.PrivacyVersion }), ct).ConfigureAwait(false);
        var tenantId = withTenant ? await tenants.CreateForNewAccountAsync(user, marketCode, ct).ConfigureAwait(false) : null;
        return (user, true, tenantId);
    }

    /// <summary>
    /// A sign-in that proved the mailbox (link, Google, invitation, reset): the e-mail is confirmed, a lockout of the password
    /// ends (AD 8), <c>last_login_at</c> is now; audit <c>auth.login_succeeded</c> with the method.
    /// </summary>
    public async Task<SignedInUser> SignInByMailboxAsync(User user, string method, bool created, Guid? tenantId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(user);
        user.EmailConfirmed = true;
        user.AccessFailedCount = 0;
        user.LockoutEnd = null;
        return await SignedInAsync(user, method, created, tenantId, ct).ConfigureAwait(false);
    }

    /// <summary>Records a successful sign-in: <c>last_login_at</c> and the audit.</summary>
    public async Task<SignedInUser> SignedInAsync(User user, string method, bool created, Guid? tenantId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(user);
        user.LastLoginAt = time.GetUtcNow();
        await UpdateAsync(user).ConfigureAwait(false);
        await audit.WriteAsync(new AuditEvent(AuditActions.LoginSucceeded, null, user.Id, "user", user.Id.ToString("D"),
            new JsonObject { ["method"] = method }), ct).ConfigureAwait(false);
        return new SignedInUser(user, method, created, tenantId);
    }

    /// <summary>Saves the user through Identity (concurrency stamp); a failure is an error of the server.</summary>
    public async Task UpdateAsync(User user)
    {
        var result = await users.UpdateAsync(user).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.ConcurrencyFailure))
                ? new DomainException(ProblemCodes.ConcurrencyConflict, 409)
                : new InvalidOperationException("user.update_failed: " + string.Join(",", result.Errors.Select(e => e.Code)));
        }
    }

    /// <summary>A deleted account cannot sign in by any method.</summary>
    public static void EnsureActive(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (user.DeletedAt is not null)
        {
            throw new DomainException(ProblemCodes.AuthAccountDisabled, 403);
        }
    }
}
