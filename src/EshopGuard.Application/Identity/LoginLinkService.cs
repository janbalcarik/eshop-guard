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
using EshopGuard.Data.Tenancy;
using Microsoft.Extensions.Options;

namespace EshopGuard.Application.Identity;

/// <summary>
/// Sign-in by a link in an e-mail (2a → 2b → 2c, design of change 9, „Data Flow“). The request does the same work for an
/// existing and an unknown e-mail and answers the same; the e-mail with the token is sent right away and never stored; opening
/// the link (<see cref="InspectAsync"/>) uses nothing, only <see cref="ConsumeAsync"/> signs in, at most once.
/// </summary>
public sealed class LoginLinkService(
    EshopGuardDb db,
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
    /// <summary>
    /// Order of checks: format of the e-mail → pause of 60 s → bucket of the IP → bucket of the e-mail → expiring the previous
    /// links and a new token → the e-mail → audit. A refused request takes nothing from the later buckets.
    /// </summary>
    public async Task<LoginLinkRequestedDto> RequestAsync(string? email, string? market, CancellationToken ct)
    {
        var validation = new ValidationResult();
        var normalized = FieldValidators.Email(validation, "email", email);
        validation.ThrowIfInvalid();
        var options = auth.Value;
        await EnsureCooldownAsync(tokens, normalized!, OneTimeTokenPurpose.MagicLink, options.LinkCooldownSeconds, time, ct).ConfigureAwait(false);

        var emailHash = hasher.HashEmail(normalized!);
        await limits.TakeAsync(AuthRateLimits.LinkIp, hasher.HashIp(request.Ip), ct).ConfigureAwait(false);
        await limits.TakeAsync(AuthRateLimits.LinkEmail, emailHash, ct).ConfigureAwait(false);

        var user = await accounts.FindIncludingDeletedAsync(normalized!, ct).ConfigureAwait(false);
        var (token, row) = await tokens.IssueAsync(normalized!, user?.Id, OneTimeTokenPurpose.MagicLink,
            TimeSpan.FromMinutes(options.LoginLinkMinutes), hasher.HashIpBytes(request.Ip), ct).ConfigureAwait(false);

        var locale = await locales.ResolveAsync(user?.Locale, request.AcceptLanguage, market, ct).ConfigureAwait(false);
        var message = composer.Compose(EmailTemplateKind.LoginLink, locale.Locale, normalized!, new Dictionary<string, object?>
        {
            ["link"] = Link(frontend.Value, frontend.Value.LoginLinkPath, token),
            ["minutes"] = options.LoginLinkMinutes,
            ["isNewAccount"] = user is null,
        });
        if (!await sender.TrySendAsync(message, ct).ConfigureAwait(false))
        {
            await tokens.ExpireAsync(row.Id, ct).ConfigureAwait(false);
            throw DirectEmailSender.Failed();
        }

        await audit.WriteAsync(new AuditEvent(AuditActions.LoginLinkRequested, null, user?.Id, "user_token", row.Id.ToString("D"),
            new JsonObject { ["emailHash"] = emailHash }), ct).ConfigureAwait(false);
        return new LoginLinkRequestedDto(options.LoginLinkMinutes * 60, options.LinkCooldownSeconds);
    }

    /// <summary>Who the link is for and whether it creates an account; uses nothing (a preview of the mail may open it).</summary>
    public async Task<LoginLinkInfoDto> InspectAsync(string? token, CancellationToken ct)
    {
        var (state, row) = await tokens.InspectAsync(token, OneTimeTokenPurpose.MagicLink, ct).ConfigureAwait(false);
        ThrowIfNotValid(state);
        var user = await accounts.FindIncludingDeletedAsync(row!.Email, ct).ConfigureAwait(false);
        return new LoginLinkInfoDto(row.Email, user is null, row.ExpiresAt);
    }

    /// <summary>
    /// Uses the link and signs in, in one transaction: a new account (with its tenant unless an invitation waits) or the
    /// existing one (lockout ended). Two concurrent uses: the second gets <c>410 login_link.used</c>.
    /// </summary>
    public Task<SignedInUser> ConsumeAsync(string? token, string? market, CancellationToken ct) => db.ExecuteInUserTransactionAsync(async () =>
    {
        var (state, row) = await tokens.ConsumeAsync(token, OneTimeTokenPurpose.MagicLink, ct).ConfigureAwait(false);
        ThrowIfNotValid(state);
        var (user, created, tenantId) = await accounts.FindOrCreateAsync(row!.Email, market, AuditActions.Methods.MagicLink, withTenant: true, ct)
            .ConfigureAwait(false);
        return await accounts.SignInByMailboxAsync(user, AuditActions.Methods.MagicLink, created, tenantId, ct).ConfigureAwait(false);
    }, ct);

    /// <summary>Link of an e-mail: the token is in the fragment, so no server or log on the way sees it.</summary>
    public static string Link(FrontendOptions frontend, string path, string token)
    {
        ArgumentNullException.ThrowIfNull(frontend);
        return frontend.BaseUrl + path + "#t=" + token;
    }

    /// <summary><c>429 rate_limited</c> (<c>email_cooldown</c>) when the last token of the e-mail is younger than the pause.</summary>
    internal static async Task EnsureCooldownAsync(
        OneTimeTokenService tokens, string email, OneTimeTokenPurpose purpose, int cooldownSeconds, TimeProvider time, CancellationToken ct)
    {
        var last = await tokens.LastCreatedAsync(email, purpose, ct).ConfigureAwait(false);
        if (last is { } at && time.GetUtcNow() - at < TimeSpan.FromSeconds(cooldownSeconds))
        {
            throw AuthRateLimits.Limited(AuthRateLimits.CooldownScope, TimeSpan.FromSeconds(cooldownSeconds) - (time.GetUtcNow() - at));
        }
    }

    private static void ThrowIfNotValid(TokenState state)
    {
        switch (state)
        {
            case TokenState.Valid:
                return;
            case TokenState.Used:
                throw new DomainException(ProblemCodes.LoginLinkUsed, 410);
            case TokenState.Expired:
                throw new DomainException(ProblemCodes.LoginLinkExpired, 410);
            default:
                throw new DomainException(ProblemCodes.LoginLinkInvalid, 404);
        }
    }
}
