using System.Text.Json.Nodes;
using EshopGuard.Application.Audit;
using EshopGuard.Application.Problems;
using EshopGuard.Application.Security;
using EshopGuard.Data;
using EshopGuard.Data.Entities.Iam;
using EshopGuard.Data.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EshopGuard.Application.Identity;

/// <summary>What Google said about the person (from the claims of the external sign-in).</summary>
public sealed record ExternalIdentity(string Provider, string ProviderKey, string? Email, bool EmailVerified);

/// <summary>
/// Sign-in through Google (AD 9): (1) the account linked by <c>user_logins</c> (<c>provider = google</c>, the subject of
/// Google); (2) otherwise the account of the e-mail, linked only when Google marks the e-mail verified (audit
/// <c>auth.google_linked</c>); (3) otherwise a new account with its tenant. An unverified e-mail creates and links nothing
/// (<c>google.email_not_verified</c>).
/// </summary>
public sealed class ExternalLoginService(EshopGuardDb db, UserManager<User> users, AccountService accounts, SecurityAuditWriter audit, IpHasher hasher)
{
    public Task<SignedInUser> CompleteAsync(ExternalIdentity identity, string? market, CancellationToken ct) => db.ExecuteInUserTransactionAsync(async () =>
    {
        ArgumentNullException.ThrowIfNull(identity);
        if (string.IsNullOrWhiteSpace(identity.ProviderKey))
        {
            throw new DomainException(ProblemCodes.GoogleFailed, 400);
        }

        var linked = await users.FindByLoginAsync(identity.Provider, identity.ProviderKey).ConfigureAwait(false);
        if (linked is not null)
        {
            await db.SwitchTransactionContextAsync(TenantSql.NoTenant, linked.Id, ct).ConfigureAwait(false);
            return await accounts.SignInByMailboxAsync(linked, AuditActions.Methods.Google, created: false, tenantId: null, ct).ConfigureAwait(false);
        }

        var email = EmailNormalizer.Normalize(identity.Email);
        if (!identity.EmailVerified || email is null)
        {
            throw new DomainException(ProblemCodes.GoogleEmailNotVerified, 400);
        }

        var (user, created, tenantId) = await accounts.FindOrCreateAsync(email, market, AuditActions.Methods.Google, withTenant: true, ct).ConfigureAwait(false);
        db.UserLogins.Add(new UserLogin { UserId = user.Id, Provider = identity.Provider, ProviderKey = identity.ProviderKey });
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        await audit.WriteAsync(new AuditEvent(AuditActions.GoogleLinked, null, user.Id, "user", user.Id.ToString("D"),
            new JsonObject { ["provider"] = identity.Provider, ["emailHash"] = hasher.HashEmail(email), ["newAccount"] = created }), ct).ConfigureAwait(false);
        return await accounts.SignInByMailboxAsync(user, AuditActions.Methods.Google, created, tenantId, ct).ConfigureAwait(false);
    }, ct);

    /// <summary><c>DELETE /api/me/logins/google</c>; <c>404 login.not_linked</c> without a link.</summary>
    public async Task UnlinkAsync(Guid userId, string provider, CancellationToken ct)
    {
        var logins = await db.UserLogins.Where(l => l.UserId == userId && l.Provider == provider).ToListAsync(ct).ConfigureAwait(false);
        if (logins.Count == 0)
        {
            throw new DomainException(ProblemCodes.LoginNotLinked, 404);
        }

        db.UserLogins.RemoveRange(logins);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        await audit.WriteAsync(new AuditEvent(AuditActions.GoogleUnlinked, null, userId, "user", userId.ToString("D"),
            new JsonObject { ["provider"] = provider }), ct).ConfigureAwait(false);
    }
}
