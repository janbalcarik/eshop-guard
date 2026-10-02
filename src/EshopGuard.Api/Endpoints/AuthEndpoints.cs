using EshopGuard.Api.Auth;
using EshopGuard.Api.Contracts;
using EshopGuard.Api.Problems;
using EshopGuard.Api.RateLimiting;
using EshopGuard.Api.Tenancy;
using EshopGuard.Application.Contracts;
using EshopGuard.Application.Identity;
using EshopGuard.Application.Me;
using EshopGuard.Application.Options;
using EshopGuard.Application.Problems;
using EshopGuard.Application.Security;
using EshopGuard.Data.Entities.Iam;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace EshopGuard.Api.Endpoints;

/// <summary><c>/api/auth</c>: CSRF token, sign-in by link, password and Google, sign-out (design of change 9, „Koncové body“).</summary>
public static class AuthEndpoints
{
    private const string ReturnPathItem = "eg.returnPath";
    private const string MarketItem = "eg.market";

    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder api)
    {
        var auth = api.MapGroup("/auth").RequireRateLimiting(RateLimiterSetup.AuthPolicy).WithTags("auth");

        auth.MapGet("/csrf", (HttpContext context, IAntiforgery antiforgery) =>
            TypedResults.Ok(new CsrfTokenDto(antiforgery.GetAndStoreTokens(context).RequestToken!)));

        auth.MapPost("/login-link", async (LoginLinkRequest? body, LoginLinkService links, CancellationToken ct) =>
                TypedResults.Accepted((string?)null, await links.RequestAsync(body!.Email, body.Market, ct)))
            .Validate<LoginLinkRequest>()
            .ProducesProblemCodes(ProblemCodes.RateLimited, ProblemCodes.EmailSendFailed);

        auth.MapPost("/login-link/inspect", async (TokenRequest? body, LoginLinkService links, CancellationToken ct) =>
                TypedResults.Ok(await links.InspectAsync(body!.Token, ct)))
            .Validate<TokenRequest>()
            .ProducesProblemCodes(ProblemCodes.LoginLinkInvalid, ProblemCodes.LoginLinkExpired, ProblemCodes.LoginLinkUsed);

        auth.MapPost("/login-link/consume", async (ConsumeLoginLinkRequest? body, LoginLinkService links, SessionWriter session, CancellationToken ct) =>
                TypedResults.Ok(await session.SignInAsync(await links.ConsumeAsync(body!.Token, body.Market, ct), ct)))
            .Validate<ConsumeLoginLinkRequest>()
            .ProducesProblemCodes(ProblemCodes.LoginLinkInvalid, ProblemCodes.LoginLinkExpired, ProblemCodes.LoginLinkUsed, ProblemCodes.AuthAccountDisabled);

        auth.MapPost("/password/login", async (PasswordLoginRequest? body, PasswordService passwords, SessionWriter session, CancellationToken ct) =>
                TypedResults.Ok(await session.SignInAsync(await passwords.LoginAsync(body!.Email, body.Password, ct), ct)))
            .Validate<PasswordLoginRequest>()
            .ProducesProblemCodes(ProblemCodes.AuthInvalidCredentials, ProblemCodes.RateLimited);

        auth.MapPost("/password/forgot", async (ForgotPasswordRequest? body, PasswordService passwords, CancellationToken ct) =>
            {
                await passwords.ForgotAsync(body!.Email, body.Market, ct);
                return TypedResults.Accepted((string?)null);
            })
            .Validate<ForgotPasswordRequest>()
            .ProducesProblemCodes(ProblemCodes.RateLimited, ProblemCodes.EmailSendFailed);

        auth.MapPost("/password/reset/inspect", async (TokenRequest? body, PasswordService passwords, CancellationToken ct) =>
                TypedResults.Ok(await passwords.InspectResetAsync(body!.Token, ct)))
            .Validate<TokenRequest>()
            .ProducesProblemCodes(ProblemCodes.ResetLinkInvalid, ProblemCodes.ResetLinkExpired, ProblemCodes.ResetLinkUsed);

        auth.MapPost("/password/reset", async (ResetPasswordRequest? body, PasswordService passwords, SessionWriter session, CancellationToken ct) =>
                TypedResults.Ok(await session.SignInAsync(await passwords.ResetAsync(body!.Token, body.NewPassword, ct), ct)))
            .Validate<ResetPasswordRequest>()
            .ProducesProblemCodes(ProblemCodes.ResetLinkInvalid, ProblemCodes.ResetLinkExpired, ProblemCodes.ResetLinkUsed);

        auth.MapGet("/google/start", StartGoogle).ProducesProblemCodes(ProblemCodes.ReturnPathInvalid, ProblemCodes.NotFound);
        auth.MapGet("/google/complete", CompleteGoogleAsync);

        auth.MapPost("/logout", async (SignInManager<User> signIn) =>
        {
            await signIn.SignOutAsync();
            return TypedResults.NoContent();
        });

        auth.MapPost("/logout-everywhere", async (ClaimsPrincipal principal, MeService me, SignInManager<User> signIn, CancellationToken ct) =>
            {
                await me.LogoutEverywhereAsync(principal.RequireUserId(), ct);
                await signIn.SignOutAsync();
                return TypedResults.NoContent();
            })
            .RequireAuthorization()
            .ProducesProblemCodes(ProblemCodes.AuthUnauthenticated);
        return auth;
    }

    /// <summary>Redirects to Google; the return path must be a path of the application (no open redirect).</summary>
    private static IResult StartGoogle(HttpContext context, IConfiguration configuration, string? returnPath, string? market)
    {
        if (!GoogleSetup.IsConfigured(configuration))
        {
            return EgProblem.Result(context, ProblemCodes.NotFound, StatusCodes.Status404NotFound);
        }

        var path = string.IsNullOrEmpty(returnPath) ? "/" : returnPath;
        if (!ReturnPathValidator.IsValid(path))
        {
            return EgProblem.Result(context, ProblemCodes.ReturnPathInvalid, StatusCodes.Status400BadRequest);
        }

        var properties = new AuthenticationProperties { RedirectUri = "/api/auth/google/complete" };
        properties.Items[ReturnPathItem] = path;
        if (market is { Length: > 0 and <= 3 } && market.All(char.IsAsciiLetterLower))
        {
            properties.Items[MarketItem] = market;
        }

        return TypedResults.Challenge(properties, [GoogleSetup.Scheme]);
    }

    /// <summary>
    /// After Google: the external cookie holds what Google said; the account is linked or created, the session cookie set and
    /// the browser goes to the frontend. Any failure goes to the sign-in page with <c>?error=code</c>.
    /// </summary>
    private static async Task<IResult> CompleteGoogleAsync(
        HttpContext context, ExternalLoginService external, SessionWriter session, IOptions<FrontendOptions> frontend, CancellationToken ct)
    {
        var links = frontend.Value;
        var result = await context.AuthenticateAsync(IdentityConstants.ExternalScheme);
        await context.SignOutAsync(IdentityConstants.ExternalScheme);
        if (!result.Succeeded || result.Principal is null)
        {
            return TypedResults.Redirect(links.BaseUrl + links.LoginPath + "?error=" + ProblemCodes.GoogleFailed);
        }

        var principal = result.Principal;
        var identity = new ExternalIdentity(
            EgUserStore.GoogleProvider,
            principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
            principal.FindFirstValue(ClaimTypes.Email),
            string.Equals(principal.FindFirstValue(GoogleSetup.EmailVerifiedClaim), "true", StringComparison.OrdinalIgnoreCase));
        var returnPath = result.Properties?.Items.TryGetValue(ReturnPathItem, out var path) == true && ReturnPathValidator.IsValid(path) ? path! : "/";
        result.Properties!.Items.TryGetValue(MarketItem, out var market);
        try
        {
            await session.SignInAsync(await external.CompleteAsync(identity, market, ct), ct);
        }
        catch (DomainException ex)
        {
            return TypedResults.Redirect(links.BaseUrl + links.LoginPath + "?error=" + Uri.EscapeDataString(ex.Code));
        }

        return TypedResults.Redirect(links.BaseUrl + returnPath);
    }
}

/// <summary>Signs a proven user in with the session cookie and answers with <see cref="SessionDto"/>.</summary>
public sealed class SessionWriter(SignInManager<User> signIn, MeService me, TimeProvider time)
{
    public async Task<SessionDto> SignInAsync(SignedInUser user, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(user);
        await signIn.SignInSessionAsync(user.User, user.Method, time);
        return new SessionDto(await me.GetAsync(user.User.Id, ct), user.IsNewAccount, user.CreatedTenantId);
    }
}
