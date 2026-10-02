using System.Security.Claims;
using EshopGuard.Api.Auth;
using EshopGuard.Api.Contracts;
using EshopGuard.Api.Problems;
using EshopGuard.Api.Tenancy;
using EshopGuard.Application.Identity;
using EshopGuard.Application.Me;
using EshopGuard.Application.Problems;
using EshopGuard.Application.Tenants;
using EshopGuard.Data.Entities.Iam;
using Microsoft.AspNetCore.Identity;

namespace EshopGuard.Api.Endpoints;

/// <summary><c>/api/me</c>: the signed-in user, his language, password, Google and invitations waiting for him.</summary>
public static class MeEndpoints
{
    public static RouteGroupBuilder MapMeEndpoints(this RouteGroupBuilder api)
    {
        var me = api.MapGroup("/me").RequireAuthorization().WithTags("me").ProducesProblemCodes(ProblemCodes.AuthUnauthenticated);

        me.MapGet("/", async (ClaimsPrincipal principal, MeService service, CancellationToken ct) =>
            TypedResults.Ok(await service.GetAsync(principal.RequireUserId(), ct)));

        me.MapPatch("/", async (UpdateMeRequest? body, ClaimsPrincipal principal, MeService service, CancellationToken ct) =>
                TypedResults.Ok(await service.UpdateAsync(principal.RequireUserId(), body!.DisplayName, ct)))
            .Validate<UpdateMeRequest>();

        me.MapPut("/locale", async (SetLocaleRequest? body, ClaimsPrincipal principal, MeService service, CancellationToken ct) =>
            {
                await service.SetLocaleAsync(principal.RequireUserId(), body!.Locale, ct);
                return TypedResults.NoContent();
            })
            .Validate<SetLocaleRequest>()
            .ProducesProblemCodes(ProblemCodes.LocaleNotEnabled);

        me.MapPut("/password", async (SetPasswordRequest? body, ClaimsPrincipal principal, PasswordService passwords, SignInManager<User> signIn, CancellationToken ct) =>
            {
                var user = await passwords.SetAsync(principal.RequireUserId(), principal.AuthenticatedAt(), body!.CurrentPassword, body.NewPassword, ct);
                await signIn.RefreshSessionAsync(user, principal);
                return TypedResults.NoContent();
            })
            .Validate<SetPasswordRequest>()
            .ProducesProblemCodes(ProblemCodes.PasswordCurrentInvalid, ProblemCodes.AuthReauthenticationRequired);

        me.MapDelete("/password", async (ClaimsPrincipal principal, PasswordService passwords, SignInManager<User> signIn, CancellationToken ct) =>
            {
                var user = await passwords.RemoveAsync(principal.RequireUserId(), principal.AuthenticatedAt(), ct);
                await signIn.RefreshSessionAsync(user, principal);
                return TypedResults.NoContent();
            })
            .ProducesProblemCodes(ProblemCodes.AuthReauthenticationRequired);

        me.MapDelete("/logins/google", async (ClaimsPrincipal principal, ExternalLoginService external, CancellationToken ct) =>
            {
                await external.UnlinkAsync(principal.RequireUserId(), EgUserStore.GoogleProvider, ct);
                return TypedResults.NoContent();
            })
            .ProducesProblemCodes(ProblemCodes.LoginNotLinked);

        me.MapPost("/invitations/{invitationId:guid}/accept", async (Guid invitationId, ClaimsPrincipal principal, InvitationService invitations, CancellationToken ct) =>
                TypedResults.Ok(await invitations.AcceptOwnAsync(principal.RequireUserId(), invitationId, ct)))
            .ProducesProblemCodes(ProblemCodes.InvitationNotFound, ProblemCodes.InvitationExpired, ProblemCodes.InvitationUsed, ProblemCodes.InvitationEmailMismatch);
        return me;
    }
}
