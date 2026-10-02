using System.Security.Claims;
using EshopGuard.Api.Contracts;
using EshopGuard.Api.Problems;
using EshopGuard.Api.Tenancy;
using EshopGuard.Application.Problems;
using EshopGuard.Application.Tenants;

namespace EshopGuard.Api.Endpoints;

/// <summary><c>/api/tenants</c> and <c>/api/t/{tenantId}</c>: tenants, members and invitations by the matrix of roles.</summary>
public static class TenantEndpoints
{
    public static RouteGroupBuilder MapTenantsEndpoints(this RouteGroupBuilder api)
    {
        var tenants = api.MapGroup("/tenants").RequireAuthorization().WithTags("tenants").ProducesProblemCodes(ProblemCodes.AuthUnauthenticated);
        tenants.MapPost("/", async (CreateTenantRequest? body, ClaimsPrincipal principal, TenantService service, CancellationToken ct) =>
            {
                var tenant = await service.CreateAsync(principal.RequireUserId(), body!.Name, body.Market, ct);
                return TypedResults.Created($"/api/t/{tenant.Id:D}", tenant);
            })
            .Validate<CreateTenantRequest>()
            .ProducesProblemCodes(ProblemCodes.MarketUnknown, ProblemCodes.TenantLimitReached);
        return tenants;
    }

    /// <summary>The group of one tenant: the access filter runs first, every endpoint declares its lowest role.</summary>
    public static RouteGroupBuilder MapTenantGroup(this RouteGroupBuilder api)
    {
        var tenant = api.MapGroup("/t/{tenantId:guid}")
            .RequireAuthorization()
            .AddEndpointFilter<TenantAccessFilter>()
            .WithTags("tenant")
            .ProducesProblemCodes(ProblemCodes.AuthUnauthenticated, ProblemCodes.TenantNotFound, ProblemCodes.TenantSuspended);

        // Reading the account works in a suspended tenant too (K rozhodnutí 10; change 12 adds the payments).
        tenant.MapGet("/", async (HttpContext context, TenantService service, CancellationToken ct) =>
                TypedResults.Ok(await service.GetAsync(context.CallerRole(), ct)))
            .RequireTenantRole(TenantRole.Viewer)
            .AllowSuspendedTenant();

        tenant.MapPatch("/", async (RenameTenantRequest? body, HttpContext context, TenantService service, CancellationToken ct) =>
                TypedResults.Ok(await service.RenameAsync(context.User.RequireUserId(), context.CallerRole(), body!.Name, body.Version, ct)))
            .RequireTenantRole(TenantRole.Admin)
            .Validate<RenameTenantRequest>()
            .ProducesProblemCodes(ProblemCodes.ConcurrencyConflict);

        tenant.MapGet("/members", async (MembershipService service, CancellationToken ct) => TypedResults.Ok(await service.ListAsync(ct)))
            .RequireTenantRole(TenantRole.Admin);

        tenant.MapPatch("/members/{userId:guid}", async (Guid userId, ChangeRoleRequest? body, HttpContext context, MembershipService service, CancellationToken ct) =>
                TypedResults.Ok(await service.ChangeRoleAsync(context.User.RequireUserId(), context.CallerRole(), userId, body!.Role, ct)))
            .RequireTenantRole(TenantRole.Admin)
            .Validate<ChangeRoleRequest>()
            .ProducesProblemCodes(ProblemCodes.MembershipNotFound, ProblemCodes.MembershipLastOwner, ProblemCodes.MembershipRoleNotAllowed);

        // Anyone may leave; removing somebody else follows the matrix in the service.
        tenant.MapDelete("/members/{userId:guid}", async (Guid userId, HttpContext context, MembershipService service, CancellationToken ct) =>
            {
                await service.RemoveAsync(context.User.RequireUserId(), context.CallerRole(), userId, ct);
                return TypedResults.NoContent();
            })
            .RequireTenantRole(TenantRole.Viewer)
            .ProducesProblemCodes(ProblemCodes.MembershipNotFound, ProblemCodes.MembershipLastOwner, ProblemCodes.MembershipRoleNotAllowed);

        tenant.MapPost("/ownership-transfer", async (TransferOwnershipRequest? body, HttpContext context, MembershipService service, CancellationToken ct) =>
            {
                await service.TransferOwnershipAsync(context.User.RequireUserId(), context.CallerRole(), body!.UserId, ct);
                return TypedResults.NoContent();
            })
            .RequireTenantRole(TenantRole.Owner)
            .Validate<TransferOwnershipRequest>()
            .ProducesProblemCodes(ProblemCodes.MembershipNotFound);

        tenant.MapGet("/invitations", async (InvitationService service, CancellationToken ct) => TypedResults.Ok(await service.ListAsync(ct)))
            .RequireTenantRole(TenantRole.Admin);

        tenant.MapPost("/invitations", async (Guid tenantId, CreateInvitationRequest? body, HttpContext context, InvitationService service, CancellationToken ct) =>
            {
                var invitation = await service.CreateAsync(context.User.RequireUserId(), context.CallerRole(), body!.Email, body.Role, body.Locale, ct);
                return TypedResults.Created($"/api/t/{tenantId:D}/invitations/{invitation.Id:D}", invitation);
            })
            .RequireTenantRole(TenantRole.Admin)
            .Validate<CreateInvitationRequest>()
            .ProducesProblemCodes(ProblemCodes.InvitationAlreadyMember, ProblemCodes.MembershipRoleNotAllowed, ProblemCodes.RateLimited,
                ProblemCodes.EmailSendFailed, ProblemCodes.LocaleNotEnabled);

        tenant.MapPost("/invitations/{invitationId:guid}/resend", async (Guid invitationId, HttpContext context, InvitationService service, CancellationToken ct) =>
            {
                await service.ResendAsync(context.User.RequireUserId(), invitationId, ct);
                return TypedResults.NoContent();
            })
            .RequireTenantRole(TenantRole.Admin)
            .ProducesProblemCodes(ProblemCodes.InvitationNotFound, ProblemCodes.InvitationUsed, ProblemCodes.RateLimited, ProblemCodes.EmailSendFailed);

        tenant.MapDelete("/invitations/{invitationId:guid}", async (Guid invitationId, HttpContext context, InvitationService service, CancellationToken ct) =>
            {
                await service.RevokeAsync(context.User.RequireUserId(), invitationId, ct);
                return TypedResults.NoContent();
            })
            .RequireTenantRole(TenantRole.Admin)
            .ProducesProblemCodes(ProblemCodes.InvitationNotFound, ProblemCodes.InvitationUsed);
        return tenant;
    }
}
