using System.Security.Claims;
using EshopGuard.Api.Contracts;
using EshopGuard.Api.Problems;
using EshopGuard.Api.RateLimiting;
using EshopGuard.Api.Tenancy;
using EshopGuard.Application.Contracts;
using EshopGuard.Application.Problems;
using EshopGuard.Application.Tenants;

namespace EshopGuard.Api.Endpoints;

/// <summary><c>/api/invitations</c>: opening and accepting an invitation by its token (before sign-in too).</summary>
public static class InvitationEndpoints
{
    public static RouteGroupBuilder MapInvitationEndpoints(this RouteGroupBuilder api)
    {
        var invitations = api.MapGroup("/invitations").RequireRateLimiting(RateLimiterSetup.AuthPolicy).WithTags("invitations");

        invitations.MapPost("/inspect", async (TokenRequest? body, InvitationService service, CancellationToken ct) =>
                TypedResults.Ok(await service.InspectAsync(body!.Token, ct)))
            .Validate<TokenRequest>()
            .ProducesProblemCodes(ProblemCodes.InvitationInvalid, ProblemCodes.InvitationExpired, ProblemCodes.InvitationUsed);

        invitations.MapPost("/accept", async (AcceptInvitationRequest? body, ClaimsPrincipal principal, InvitationService service, SessionWriter session, CancellationToken ct) =>
            {
                var (user, membership) = await service.AcceptAsync(body!.Token, principal.UserId(), body.Market, ct);
                var signedIn = await session.SignInAsync(user, ct);
                return TypedResults.Ok(new InvitationSessionDto(signedIn.Me, signedIn.IsNewAccount, membership));
            })
            .Validate<AcceptInvitationRequest>()
            .ProducesProblemCodes(ProblemCodes.InvitationInvalid, ProblemCodes.InvitationExpired, ProblemCodes.InvitationUsed,
                ProblemCodes.InvitationEmailMismatch, ProblemCodes.AuthAccountDisabled);
        return invitations;
    }
}
