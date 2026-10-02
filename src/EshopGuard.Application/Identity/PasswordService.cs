using System.Text.Json.Nodes;
using EshopGuard.Application.Audit;
using EshopGuard.Application.Contracts;
using EshopGuard.Application.Email;
using EshopGuard.Application.Identity.Validators;
using EshopGuard.Application.Localization;
using EshopGuard.Application.Options;
using EshopGuard.Application.Problems;
using EshopGuard.Application.RateLimits;
using EshopGuard.Application.Security;
using EshopGuard.Data;
using EshopGuard.Data.Entities.Iam;
using EshopGuard.Data.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace EshopGuard.Application.Identity;

/// <summary>
/// The optional password (AD 8): sign-in with the same <c>401 auth.invalid_credentials</c> for an unknown account, an account
/// without a password, a wrong password and a lockout; setting, changing and removing it; the reset by a link. Every new
/// password changes the security stamp, so the other sessions end within the stamp's check interval.
/// </summary>
public sealed class PasswordService(
    EshopGuardDb db,
    UserManager<User> users,
    IPasswordHasher<User> passwordHasher,
    OneTimeTokenService tokens,
    AccountService accounts,
    AuthRateLimits limits,
    IpHasher hasher,
    EmailComposer composer,
    DirectEmailSender sender,
    LocaleResolver locales,
    SecurityAuditWriter audit,
    RequestContext request,
    IOptions<AuthOptions> auth,
    IOptions<FrontendOptions> frontend,
    TimeProvider time)
{
    /// <summary>A hash verified when there is no account, so both cases cost the same time.</summary>
    private static readonly Lazy<string> DummyHash = new(() => new PasswordHasher<User>().HashPassword(new User { Email = "x@x.invalid" }, Guid.NewGuid().ToString("N")));

    public async Task<SignedInUser> LoginAsync(string? email, string? password, CancellationToken ct)
    {
        var validation = new ValidationResult();
        var normalized = FieldValidators.Email(validation, "email", email);
        if (string.IsNullOrEmpty(password))
        {
            validation.Add("password", ProblemCodes.Fields.Required);
        }

        validation.ThrowIfInvalid();
        await limits.TakeAsync(AuthRateLimits.PasswordIp, hasher.HashIp(request.Ip), ct).ConfigureAwait(false);
        var emailHash = hasher.HashEmail(normalized!);

        var user = await users.FindByEmailAsync(normalized!).ConfigureAwait(false);
        if (user?.PasswordHash is null)
        {
            passwordHasher.VerifyHashedPassword(new User { Email = normalized! }, DummyHash.Value, password!);
            await FailedAsync(user?.Id, emailHash, "no_password", ct).ConfigureAwait(false);
            throw InvalidCredentials();
        }

        var now = time.GetUtcNow();
        if (user.LockoutEnd is { } end && end > now)
        {
            passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password!);
            await FailedAsync(user.Id, emailHash, "locked_out", ct).ConfigureAwait(false);
            throw InvalidCredentials();
        }

        var verified = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password!);
        if (verified == PasswordVerificationResult.Failed)
        {
            user.AccessFailedCount++;
            var lockedOut = user.AccessFailedCount >= auth.Value.Lockout.MaxFailedAttempts;
            if (lockedOut)
            {
                user.LockoutEnd = now + TimeSpan.FromMinutes(auth.Value.Lockout.Minutes);
                user.AccessFailedCount = 0;
            }

            await accounts.UpdateAsync(user).ConfigureAwait(false);
            await FailedAsync(user.Id, emailHash, "wrong_password", ct).ConfigureAwait(false);
            if (lockedOut)
            {
                await audit.WriteAsync(new AuditEvent(AuditActions.LockedOut, null, user.Id, "user", user.Id.ToString("D"),
                    new JsonObject { ["method"] = AuditActions.Methods.Password, ["minutes"] = auth.Value.Lockout.Minutes }), ct).ConfigureAwait(false);
            }

            throw InvalidCredentials();
        }

        if (verified == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, password!);
        }

        user.AccessFailedCount = 0;
        user.LockoutEnd = null;
        return await db.ExecuteInUserTransactionAsync(
            () => accounts.SignedInAsync(user, AuditActions.Methods.Password, created: false, tenantId: null, ct), ct).ConfigureAwait(false);
    }

    /// <summary>
    /// <c>PUT /api/me/password</c>: a change of an existing password needs the current one; the first password without it
    /// needs a session signed in at most <c>Auth:ReauthenticationMinutes</c> ago.
    /// </summary>
    public async Task<User> SetAsync(Guid userId, DateTimeOffset? authTime, string? currentPassword, string? newPassword, CancellationToken ct)
    {
        var user = await RequireUserAsync(userId).ConfigureAwait(false);
        var validation = new ValidationResult();
        FieldValidators.NewPassword(validation, "newPassword", newPassword, user.Email, auth.Value.Password.MinLength);
        validation.ThrowIfInvalid();
        if (user.PasswordHash is not null)
        {
            if (string.IsNullOrEmpty(currentPassword) || passwordHasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword) == PasswordVerificationResult.Failed)
            {
                throw new DomainException(ProblemCodes.PasswordCurrentInvalid, 400);
            }
        }
        else
        {
            EnsureRecentlyAuthenticated(authTime);
        }

        user.PasswordHash = passwordHasher.HashPassword(user, newPassword!);
        await users.UpdateSecurityStampAsync(user).ConfigureAwait(false);
        await audit.WriteAsync(new AuditEvent(AuditActions.PasswordSet, null, user.Id, "user", user.Id.ToString("D")), ct).ConfigureAwait(false);
        return user;
    }

    /// <summary><c>DELETE /api/me/password</c>: only a recently signed-in session; the account then signs in by link or Google.</summary>
    public async Task<User> RemoveAsync(Guid userId, DateTimeOffset? authTime, CancellationToken ct)
    {
        var user = await RequireUserAsync(userId).ConfigureAwait(false);
        EnsureRecentlyAuthenticated(authTime);
        user.PasswordHash = null;
        await users.UpdateSecurityStampAsync(user).ConfigureAwait(false);
        await audit.WriteAsync(new AuditEvent(AuditActions.PasswordRemoved, null, user.Id, "user", user.Id.ToString("D")), ct).ConfigureAwait(false);
        return user;
    }

    /// <summary><c>POST /api/auth/password/forgot</c>: the same <c>202</c> for any e-mail; a link only to an existing account.</summary>
    public async Task ForgotAsync(string? email, string? market, CancellationToken ct)
    {
        var validation = new ValidationResult();
        var normalized = FieldValidators.Email(validation, "email", email);
        validation.ThrowIfInvalid();
        var options = auth.Value;
        await LoginLinkService.EnsureCooldownAsync(tokens, normalized!, OneTimeTokenPurpose.Reset, options.LinkCooldownSeconds, time, ct).ConfigureAwait(false);
        var emailHash = hasher.HashEmail(normalized!);
        await limits.TakeAsync(AuthRateLimits.ResetIp, hasher.HashIp(request.Ip), ct).ConfigureAwait(false);
        await limits.TakeAsync(AuthRateLimits.ResetEmail, emailHash, ct).ConfigureAwait(false);

        var user = await users.FindByEmailAsync(normalized!).ConfigureAwait(false);
        if (user is not null)
        {
            var (token, row) = await tokens.IssueAsync(normalized!, user.Id, OneTimeTokenPurpose.Reset,
                TimeSpan.FromMinutes(options.ResetLinkMinutes), hasher.HashIpBytes(request.Ip), ct).ConfigureAwait(false);
            var locale = await locales.ResolveAsync(user.Locale, request.AcceptLanguage, market, ct).ConfigureAwait(false);
            var message = composer.Compose(EmailTemplateKind.PasswordReset, locale.Locale, normalized!, new Dictionary<string, object?>
            {
                ["link"] = LoginLinkService.Link(frontend.Value, frontend.Value.ResetPath, token),
                ["minutes"] = options.ResetLinkMinutes,
            });
            if (!await sender.TrySendAsync(message, ct).ConfigureAwait(false))
            {
                await tokens.ExpireAsync(row.Id, ct).ConfigureAwait(false);
                throw DirectEmailSender.Failed();
            }
        }

        await audit.WriteAsync(new AuditEvent(AuditActions.PasswordResetRequested, null, user?.Id, null, null,
            new JsonObject { ["emailHash"] = emailHash }), ct).ConfigureAwait(false);
    }

    public async Task<ResetLinkInfoDto> InspectResetAsync(string? token, CancellationToken ct)
    {
        var (state, row) = await tokens.InspectAsync(token, OneTimeTokenPurpose.Reset, ct).ConfigureAwait(false);
        ThrowIfNotValid(state);
        return new ResetLinkInfoDto(row!.Email, row.ExpiresAt);
    }

    /// <summary>
    /// <c>POST /api/auth/password/reset</c>: uses the link, sets the password, changes the security stamp (other sessions end),
    /// ends a lockout, confirms the e-mail and signs in with <c>amr = reset</c>.
    /// </summary>
    public Task<SignedInUser> ResetAsync(string? token, string? newPassword, CancellationToken ct) => db.ExecuteInUserTransactionAsync(async () =>
    {
        var (state, row) = await tokens.ConsumeAsync(token, OneTimeTokenPurpose.Reset, ct).ConfigureAwait(false);
        ThrowIfNotValid(state);
        var validation = new ValidationResult();
        FieldValidators.NewPassword(validation, "newPassword", newPassword, row!.Email, auth.Value.Password.MinLength);
        validation.ThrowIfInvalid();

        var user = row.UserId is { } id ? await users.FindByIdAsync(id.ToString("D")).ConfigureAwait(false) : null;
        if (user is null)
        {
            throw new DomainException(ProblemCodes.ResetLinkInvalid, 404);
        }

        await db.SwitchTransactionContextAsync(TenantSql.NoTenant, user.Id, ct).ConfigureAwait(false);
        user.PasswordHash = passwordHasher.HashPassword(user, newPassword!);
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        await audit.WriteAsync(new AuditEvent(AuditActions.PasswordReset, null, user.Id, "user", user.Id.ToString("D")), ct).ConfigureAwait(false);
        return await accounts.SignInByMailboxAsync(user, AuditActions.Methods.Reset, created: false, tenantId: null, ct).ConfigureAwait(false);
    }, ct);

    private static DomainException InvalidCredentials() => new(ProblemCodes.AuthInvalidCredentials, 401);

    private async Task<User> RequireUserAsync(Guid userId) =>
        await users.FindByIdAsync(userId.ToString("D")).ConfigureAwait(false) ?? throw new DomainException(ProblemCodes.AuthUnauthenticated, 401);

    private void EnsureRecentlyAuthenticated(DateTimeOffset? authTime)
    {
        if (authTime is not { } at || time.GetUtcNow() - at > TimeSpan.FromMinutes(auth.Value.ReauthenticationMinutes))
        {
            throw new DomainException(ProblemCodes.AuthReauthenticationRequired, 403);
        }
    }

    private Task FailedAsync(Guid? userId, string emailHash, string reason, CancellationToken ct) =>
        audit.WriteAsync(new AuditEvent(AuditActions.LoginFailed, null, userId, userId is null ? null : "user", userId?.ToString("D"),
            new JsonObject { ["method"] = AuditActions.Methods.Password, ["reason"] = reason, ["emailHash"] = emailHash }), ct);

    private static void ThrowIfNotValid(TokenState state)
    {
        switch (state)
        {
            case TokenState.Valid:
                return;
            case TokenState.Used:
                throw new DomainException(ProblemCodes.ResetLinkUsed, 410);
            case TokenState.Expired:
                throw new DomainException(ProblemCodes.ResetLinkExpired, 410);
            default:
                throw new DomainException(ProblemCodes.ResetLinkInvalid, 404);
        }
    }
}
