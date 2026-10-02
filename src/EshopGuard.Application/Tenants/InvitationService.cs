using System.Text.Json.Nodes;
using EshopGuard.Application.Audit;
using EshopGuard.Application.Contracts;
using EshopGuard.Application.Email;
using EshopGuard.Application.Identity;
using EshopGuard.Application.Identity.Validators;
using EshopGuard.Application.Localization;
using EshopGuard.Application.Options;
using EshopGuard.Application.Problems;
using EshopGuard.Application.RateLimits;
using EshopGuard.Application.Security;
using EshopGuard.Data;
using EshopGuard.Data.Entities.Iam;
using EshopGuard.Data.Tenancy;
using EshopGuard.Jobs.Outbox;
using EshopGuard.Jobs.Queue;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EshopGuard.Application.Tenants;

/// <summary>
/// Invitations into a tenant (design of change 9, „Pozvánka“): an admin invites an editor or a viewer, only an owner an admin;
/// the token goes only in the e-mail (in the language of the recipient), the database keeps its hash. Opening and accepting
/// by the token run before a tenant is known: the transaction carries the hash of the token (<c>app.invitation_hash</c>,
/// policy <c>invitations_select_by_token</c>), accepting then switches to the invitation's tenant for the membership.
/// </summary>
public sealed class InvitationService(
    EshopGuardDb db,
    AccountService accounts,
    AuthRateLimits limits,
    IpHasher hasher,
    EmailComposer composer,
    DirectEmailSender sender,
    LocaleResolver locales,
    SecurityAuditWriter audit,
    IJobQueue queue,
    IOptions<InvitationsOptions> options,
    IOptions<FrontendOptions> frontend,
    TimeProvider time)
{
    public Task<IReadOnlyList<InvitationDto>> ListAsync(CancellationToken ct) => db.ExecuteInTenantTransactionAsync(async () =>
    {
        var now = time.GetUtcNow();
        var rows = await db.Invitations.AsNoTracking().OrderByDescending(i => i.CreatedAt).ToListAsync(ct).ConfigureAwait(false);
        return (IReadOnlyList<InvitationDto>)rows.Select(i => ToDto(i, now)).ToList();
    }, ct);

    /// <summary>A new invitation; a pending one of the same e-mail stops being valid.</summary>
    public async Task<InvitationDto> CreateAsync(Guid callerId, TenantRole callerRole, string? email, string? role, string? locale, CancellationToken ct)
    {
        var validation = new ValidationResult();
        var normalized = FieldValidators.Email(validation, "email", email);
        var membershipRole = FieldValidators.Role(validation, "role", role);
        var code = FieldValidators.Code(validation, "locale", locale, required: false);
        validation.ThrowIfInvalid();
        if (code is not null && !await locales.IsEnabledAsync(code, ct).ConfigureAwait(false))
        {
            throw new DomainException(ProblemCodes.LocaleNotEnabled, 400, new Dictionary<string, object?> { ["locale"] = code });
        }

        var wanted = TenantRoles.FromMembership(membershipRole!.Value);
        if (!MembershipRules.CanInvite(callerRole, wanted))
        {
            throw new DomainException(ProblemCodes.MembershipRoleNotAllowed, 403, new Dictionary<string, object?> { ["role"] = wanted.Code() });
        }

        var tenantId = db.TenantContext.RequireTenantId();
        var (invitation, token) = await db.ExecuteInTenantTransactionAsync(async () =>
        {
            var member = await db.Memberships.Join(db.Users, m => m.UserId, u => u.Id, (m, u) => u.Email)
                .AnyAsync(e => e == normalized, ct).ConfigureAwait(false);
            if (member)
            {
                throw new DomainException(ProblemCodes.InvitationAlreadyMember, 409);
            }

            await limits.TakeAsync(AuthRateLimits.InviteTenant, tenantId.ToString("N"), ct).ConfigureAwait(false);
            var now = time.GetUtcNow();
            await DbSql.ExecuteAsync(db,
                "UPDATE iam.invitations SET expires_at = @now, updated_at = @now WHERE email = @email AND accepted_at IS NULL AND expires_at > @now", ct,
                DbSql.P("now", now), DbSql.P("email", normalized)).ConfigureAwait(false);
            var (secret, hash) = OneTimeTokens.Create();
            var row = new Invitation
            {
                TenantId = tenantId,
                Email = normalized!,
                Role = membershipRole.Value,
                TokenHash = hash,
                ExpiresAt = now + TimeSpan.FromDays(options.Value.ValidDays),
                InvitedBy = callerId,
            };
            db.Invitations.Add(row);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            await audit.WriteAsync(new AuditEvent(AuditActions.InvitationCreated, tenantId, callerId, "invitation", row.Id.ToString("D"),
                new JsonObject { ["role"] = wanted.Code(), ["emailHash"] = hasher.HashEmail(normalized!) }), ct).ConfigureAwait(false);
            return (row, secret);
        }, ct).ConfigureAwait(false);

        await SendAsync(invitation, token, code, callerId, ct).ConfigureAwait(false);
        return ToDto(invitation, time.GetUtcNow());
    }

    /// <summary>A new token for a pending invitation (the old one stops working) and the e-mail again.</summary>
    public async Task ResendAsync(Guid callerId, Guid invitationId, CancellationToken ct)
    {
        var tenantId = db.TenantContext.RequireTenantId();
        var (invitation, token) = await db.ExecuteInTenantTransactionAsync(async () =>
        {
            var row = await db.Invitations.FirstOrDefaultAsync(i => i.Id == invitationId, ct).ConfigureAwait(false)
                ?? throw new DomainException(ProblemCodes.InvitationNotFound, 404);
            if (row.AcceptedAt is not null)
            {
                throw new DomainException(ProblemCodes.InvitationUsed, 410);
            }

            await limits.TakeAsync(AuthRateLimits.InviteTenant, tenantId.ToString("N"), ct).ConfigureAwait(false);
            var (secret, hash) = OneTimeTokens.Create();
            row.TokenHash = hash;
            row.ExpiresAt = time.GetUtcNow() + TimeSpan.FromDays(options.Value.ValidDays);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            await audit.WriteAsync(new AuditEvent(AuditActions.InvitationResent, tenantId, callerId, "invitation", row.Id.ToString("D")), ct).ConfigureAwait(false);
            return (row, secret);
        }, ct).ConfigureAwait(false);

        await SendAsync(invitation, token, null, callerId, ct).ConfigureAwait(false);
    }

    /// <summary>Revokes a pending invitation: it expires now (<c>410 invitation.expired</c> for its token).</summary>
    public async Task RevokeAsync(Guid callerId, Guid invitationId, CancellationToken ct)
    {
        var tenantId = db.TenantContext.RequireTenantId();
        await db.ExecuteInTenantTransactionAsync(async () =>
        {
            var row = await db.Invitations.FirstOrDefaultAsync(i => i.Id == invitationId, ct).ConfigureAwait(false)
                ?? throw new DomainException(ProblemCodes.InvitationNotFound, 404);
            if (row.AcceptedAt is not null)
            {
                throw new DomainException(ProblemCodes.InvitationUsed, 410);
            }

            var now = time.GetUtcNow();
            if (row.ExpiresAt > now)
            {
                row.ExpiresAt = now;
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
            }

            await audit.WriteAsync(new AuditEvent(AuditActions.InvitationRevoked, tenantId, callerId, "invitation", row.Id.ToString("D")), ct).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);
    }

    /// <summary>What the invitation of a token offers (page of the invitation); uses nothing.</summary>
    public Task<InvitationInfoDto> InspectAsync(string? token, CancellationToken ct) => db.ExecuteInUserTransactionAsync(async () =>
    {
        var row = await FindByTokenAsync(token, ct).ConfigureAwait(false);
        ThrowIfNotOpen(row);
        var tenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == row!.TenantId, ct).ConfigureAwait(false)
            ?? throw new DomainException(ProblemCodes.InvitationInvalid, 404);
        var inviter = await db.Users.AsNoTracking().Where(u => u.Id == row!.InvitedBy).Select(u => u.DisplayName).FirstOrDefaultAsync(ct).ConfigureAwait(false);
        var exists = await accounts.FindIncludingDeletedAsync(row!.Email, ct).ConfigureAwait(false) is not null;
        return new InvitationInfoDto(tenant.Name, inviter, row.Role.Code(), row.Email, row.ExpiresAt, exists);
    }, ct);

    /// <summary>
    /// Accepts the invitation of a token: the signed-in user must have its e-mail (<c>409 invitation.email_mismatch</c>);
    /// without a session the account of the e-mail signs in, or is created (no tenant of its own).
    /// </summary>
    public Task<(SignedInUser User, MembershipDto Membership)> AcceptAsync(string? token, Guid? currentUserId, string? market, CancellationToken ct) =>
        db.ExecuteInUserTransactionAsync(async () =>
        {
            var row = await FindByTokenAsync(token, ct).ConfigureAwait(false);
            ThrowIfNotOpen(row);
            return await AcceptCoreAsync(row!, currentUserId, market, ct).ConfigureAwait(false);
        }, ct);

    /// <summary>Accepts a pending invitation of the signed-in user's e-mail from <c>MeDto.pendingInvitations</c>.</summary>
    public Task<MembershipDto> AcceptOwnAsync(Guid userId, Guid invitationId, CancellationToken ct) => db.ExecuteInUserTransactionAsync(async () =>
    {
        await db.SwitchTransactionContextAsync(TenantSql.NoTenant, userId, ct).ConfigureAwait(false);
        var row = await db.Invitations.IgnoreQueryFilters([EshopGuardDb.TenantFilter]).AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == invitationId, ct).ConfigureAwait(false);
        if (row is null)
        {
            throw new DomainException(ProblemCodes.InvitationNotFound, 404);
        }

        ThrowIfNotOpen(row);
        return (await AcceptCoreAsync(row, userId, null, ct).ConfigureAwait(false)).Membership;
    }, ct);

    private async Task<(SignedInUser User, MembershipDto Membership)> AcceptCoreAsync(Invitation invitation, Guid? currentUserId, string? market, CancellationToken ct)
    {
        var tenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == invitation.TenantId, ct).ConfigureAwait(false)
            ?? throw new DomainException(ProblemCodes.InvitationInvalid, 404);
        User user;
        var created = false;
        if (currentUserId is { } currentId)
        {
            user = await db.Users.FirstOrDefaultAsync(u => u.Id == currentId, ct).ConfigureAwait(false)
                ?? throw new DomainException(ProblemCodes.AuthUnauthenticated, 401);
            if (!string.Equals(user.Email, invitation.Email, StringComparison.Ordinal))
            {
                throw new DomainException(ProblemCodes.InvitationEmailMismatch, 409);
            }

            await db.SwitchTransactionContextAsync(TenantSql.NoTenant, user.Id, ct).ConfigureAwait(false);
        }
        else
        {
            (user, created, _) = await accounts.FindOrCreateAsync(invitation.Email, market ?? tenant.MarketCode, AuditActions.Methods.Invitation,
                withTenant: false, ct).ConfigureAwait(false);
        }

        // The tenant of the invitation for the membership, the outbox and the audit (RLS checks every row against it).
        await db.SwitchTransactionContextAsync(invitation.TenantId, user.Id, ct).ConfigureAwait(false);
        var now = time.GetUtcNow();
        var accepted = await DbSql.ExecuteAsync(db,
            "UPDATE iam.invitations SET accepted_at = @now, updated_at = @now WHERE id = @id AND accepted_at IS NULL AND expires_at > @now", ct,
            DbSql.P("now", now), DbSql.P("id", invitation.Id)).ConfigureAwait(false);
        if (accepted == 0)
        {
            throw new DomainException(ProblemCodes.InvitationUsed, 410);
        }

        // An existing member keeps his role.
        await DbSql.ExecuteAsync(db,
            """
            INSERT INTO iam.memberships (tenant_id, user_id, role, invited_by, created_at, updated_at)
            VALUES (@tenant, @user, @role, @inviter, @now, @now)
            ON CONFLICT (tenant_id, user_id) DO NOTHING
            """, ct,
            DbSql.P("tenant", invitation.TenantId), DbSql.P("user", user.Id), DbSql.P("role", invitation.Role.Code()),
            DbSql.P("inviter", invitation.InvitedBy), DbSql.P("now", now)).ConfigureAwait(false);
        var role = await DbSql.ScalarAsync<string>(db, "SELECT role FROM iam.memberships WHERE tenant_id = @tenant AND user_id = @user", ct,
            DbSql.P("tenant", invitation.TenantId), DbSql.P("user", user.Id)).ConfigureAwait(false);

        await OutboxEmails.AddAsync(DbSql.Transaction(db), queue, invitation.TenantId, new OutboxEmail(
            EmailTemplates.InvitationAccepted, invitation.InvitedBy, null, null, new JsonObject
            {
                ["invitation_id"] = invitation.Id.ToString("D"),
                ["member_user_id"] = user.Id.ToString("D"),
            }), ct).ConfigureAwait(false);
        await audit.WriteAsync(new AuditEvent(AuditActions.InvitationAccepted, invitation.TenantId, user.Id, "invitation", invitation.Id.ToString("D"),
            new JsonObject { ["role"] = role, ["newAccount"] = created }), ct).ConfigureAwait(false);
        await db.SwitchTransactionContextAsync(TenantSql.NoTenant, user.Id, ct).ConfigureAwait(false);

        var signedIn = await accounts.SignInByMailboxAsync(user, AuditActions.Methods.Invitation, created, null, ct).ConfigureAwait(false);
        return (signedIn, new MembershipDto(invitation.TenantId, tenant.Name, role!));
    }

    /// <summary>The invitation of a token, visible only through its hash in the transaction (no tenant needed).</summary>
    private async Task<Invitation?> FindByTokenAsync(string? token, CancellationToken ct)
    {
        var hash = OneTimeTokens.TryHash(token);
        if (hash is null)
        {
            return null;
        }

        await DbSql.ExecuteAsync(db, "SELECT set_config('app.invitation_hash', @hash, true)", ct, DbSql.P("hash", Convert.ToHexStringLower(hash)))
            .ConfigureAwait(false);
        return await db.Invitations.IgnoreQueryFilters([EshopGuardDb.TenantFilter]).AsNoTracking()
            .FirstOrDefaultAsync(i => i.TokenHash == hash, ct).ConfigureAwait(false);
    }

    private void ThrowIfNotOpen(Invitation? row)
    {
        if (row is null)
        {
            throw new DomainException(ProblemCodes.InvitationInvalid, 404);
        }

        if (row.AcceptedAt is not null)
        {
            throw new DomainException(ProblemCodes.InvitationUsed, 410);
        }

        if (row.ExpiresAt <= time.GetUtcNow())
        {
            throw new DomainException(ProblemCodes.InvitationExpired, 410);
        }
    }

    /// <summary>
    /// The e-mail of an invitation in the language of the recipient (AD 10): his account's language, otherwise the language of
    /// the request of the invitation, otherwise the tenant's. A failed send expires the invitation (<c>503 email.send_failed</c>).
    /// </summary>
    private async Task SendAsync(Invitation invitation, string token, string? requestedLocale, Guid callerId, CancellationToken ct)
    {
        var (tenantName, tenantLocale, inviterName, inviterEmail, recipientLocale) = await db.ExecuteInTenantTransactionAsync(async () =>
        {
            var tenant = await db.Tenants.AsNoTracking().FirstAsync(t => t.Id == invitation.TenantId, ct).ConfigureAwait(false);
            var inviter = await db.Users.AsNoTracking().FirstAsync(u => u.Id == callerId, ct).ConfigureAwait(false);
            var recipient = await accounts.FindIncludingDeletedAsync(invitation.Email, ct).ConfigureAwait(false);
            return (tenant.Name, tenant.Locale, inviter.DisplayName, inviter.Email, recipient?.Locale);
        }, ct).ConfigureAwait(false);

        var locale = recipientLocale is not null && await locales.IsEnabledAsync(recipientLocale, ct).ConfigureAwait(false)
            ? recipientLocale
            : requestedLocale ?? tenantLocale;
        var message = composer.Compose(EmailTemplateKind.Invitation, locale, invitation.Email, new Dictionary<string, object?>
        {
            ["link"] = Identity.LoginLinkService.Link(frontend.Value, frontend.Value.InvitationPath, token),
            ["days"] = options.Value.ValidDays,
            ["tenantName"] = tenantName,
            ["inviterName"] = string.IsNullOrWhiteSpace(inviterName) ? inviterEmail : inviterName,
            ["roleName"] = composer.Label(locale, "role." + invitation.Role.Code()),
        });
        if (!await sender.TrySendAsync(message, ct).ConfigureAwait(false))
        {
            await db.ExecuteInTenantTransactionAsync(() => DbSql.ExecuteAsync(db,
                "UPDATE iam.invitations SET expires_at = least(expires_at, @now), updated_at = @now WHERE id = @id", ct,
                DbSql.P("now", time.GetUtcNow()), DbSql.P("id", invitation.Id)), ct).ConfigureAwait(false);
            throw DirectEmailSender.Failed();
        }
    }

    private static InvitationDto ToDto(Invitation invitation, DateTimeOffset now) => new(
        invitation.Id, invitation.Email, invitation.Role.Code(), invitation.InvitedBy, invitation.ExpiresAt, invitation.AcceptedAt,
        invitation.AcceptedAt is not null ? "accepted" : invitation.ExpiresAt <= now ? "expired" : "pending");
}
