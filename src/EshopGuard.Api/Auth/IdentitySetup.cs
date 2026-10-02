using System.Security.Claims;
using EshopGuard.Api.Problems;
using EshopGuard.Api.Tenancy;
using EshopGuard.Application;
using EshopGuard.Application.Options;
using EshopGuard.Application.Problems;
using EshopGuard.Data.Entities.Iam;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace EshopGuard.Api.Auth;

/// <summary>
/// Identity, the session cookie and antiforgery (AD 7, AD 8): <see cref="SignInManager{TUser}"/> over the store of
/// <c>EshopGuard.Application</c>; the cookie <c>__Host-eg_session</c> (HttpOnly, Secure, SameSite=Lax, Path=/, 30 days
/// sliding); the security stamp checked every 5 minutes with <c>amr</c> and <c>auth_time</c> kept; 401/403 problems instead
/// of redirects; the keys of Data Protection in <c>DataProtection:KeysPath</c>; antiforgery with the header
/// <c>X-CSRF-TOKEN</c> and the cookie <c>__Host-eg_csrf</c> (SameSite=Strict).
/// </summary>
public static class IdentitySetup
{
    public const string SessionCookie = "__Host-eg_session";
    public const string ExternalCookie = "__Host-eg_external";
    public const string CsrfCookie = "__Host-eg_csrf";
    public const string CsrfHeader = "X-CSRF-TOKEN";

    public static IServiceCollection AddEshopGuardIdentity(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddEshopGuardApplication()
            .AddSignInManager()
            .AddClaimsPrincipalFactory<EgClaimsPrincipalFactory>();
        services.Configure<IdentityOptions>(o => o.ClaimsIdentity.UserIdClaimType = SessionClaims.Subject);

        services.AddAuthentication(o =>
        {
            o.DefaultScheme = IdentityConstants.ApplicationScheme;
            o.DefaultSignInScheme = IdentityConstants.ExternalScheme;
        }).AddIdentityCookies();

        services.AddOptions<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme).Configure<IOptions<AuthOptions>, TimeProvider>((o, auth, time) =>
        {
            o.Cookie.Name = SessionCookie;
            o.Cookie.HttpOnly = true;
            o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            o.Cookie.SameSite = SameSiteMode.Lax;
            o.Cookie.Path = "/";
            o.ExpireTimeSpan = TimeSpan.FromDays(auth.Value.SessionDays);
            o.SlidingExpiration = true;
            o.TimeProvider = time;
            o.Events.OnRedirectToLogin = context => Problem(context.HttpContext, ProblemCodes.AuthUnauthenticated, StatusCodes.Status401Unauthorized);
            o.Events.OnRedirectToAccessDenied = context => Problem(context.HttpContext, ProblemCodes.AuthForbiddenRole, StatusCodes.Status403Forbidden);
        });
        services.AddOptions<CookieAuthenticationOptions>(IdentityConstants.ExternalScheme).Configure<TimeProvider>((o, time) =>
        {
            o.Cookie.Name = ExternalCookie;
            o.Cookie.HttpOnly = true;
            o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            o.Cookie.SameSite = SameSiteMode.Lax;
            o.Cookie.Path = "/";
            o.ExpireTimeSpan = TimeSpan.FromMinutes(10);
            o.TimeProvider = time;
        });
        services.AddOptions<SecurityStampValidatorOptions>().Configure<IOptions<AuthOptions>, TimeProvider>((o, auth, time) =>
        {
            o.ValidationInterval = TimeSpan.FromMinutes(auth.Value.SecurityStampValidationMinutes);
            o.TimeProvider = time;
            o.OnRefreshingPrincipal = context =>
            {
                // The new principal keeps how and when the user signed in (reauthentication, audit).
                var identity = context.NewPrincipal?.Identities.FirstOrDefault();
                foreach (var claim in context.CurrentPrincipal?.Claims.Where(c => c.Type is SessionClaims.Method or SessionClaims.AuthTime) ?? [])
                {
                    identity?.AddClaim(new Claim(claim.Type, claim.Value));
                }

                return Task.CompletedTask;
            };
        });

        var keys = configuration["DataProtection:KeysPath"];
        var protection = services.AddDataProtection().SetApplicationName("EshopGuard");
        if (!string.IsNullOrWhiteSpace(keys))
        {
            protection.PersistKeysToFileSystem(new DirectoryInfo(keys));
        }

        services.AddAntiforgery(o =>
        {
            o.HeaderName = CsrfHeader;
            o.Cookie.Name = CsrfCookie;
            o.Cookie.HttpOnly = true;
            o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            o.Cookie.SameSite = SameSiteMode.Strict;
            o.Cookie.Path = "/";
            o.SuppressXFrameOptionsHeader = false;
        });
        services.AddScoped<CsrfEndpointFilter>();
        services.AddGoogleSignIn(configuration);
        return services;
    }

    /// <summary>Signs the user in with the session cookie: <c>amr</c> = method, <c>auth_time</c> = now.</summary>
    public static async Task SignInSessionAsync(this SignInManager<User> signIn, User user, string method, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(signIn);
        await signIn.SignInWithClaimsAsync(user, isPersistent: true,
        [
            new Claim(SessionClaims.Method, method),
            new Claim(SessionClaims.AuthTime, time.GetUtcNow().ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture)),
        ]).ConfigureAwait(false);
    }

    /// <summary>A new cookie for the same session after the security stamp changed (a new password): same <c>amr</c> and <c>auth_time</c>.</summary>
    public static async Task RefreshSessionAsync(this SignInManager<User> signIn, User user, ClaimsPrincipal current)
    {
        ArgumentNullException.ThrowIfNull(signIn);
        ArgumentNullException.ThrowIfNull(current);
        var claims = current.Claims.Where(c => c.Type is SessionClaims.Method or SessionClaims.AuthTime).Select(c => new Claim(c.Type, c.Value)).ToList();
        await signIn.SignInWithClaimsAsync(user, isPersistent: true, claims).ConfigureAwait(false);
    }

    private static Task Problem(HttpContext context, string code, int status) =>
        EgProblem.WriteAsync(context, EgProblem.Create(context, code, status));
}

/// <summary>The principal of a session: only <c>sub</c> and the security stamp (no e-mail in the cookie).</summary>
public sealed class EgClaimsPrincipalFactory(IOptions<IdentityOptions> options) : IUserClaimsPrincipalFactory<User>
{
    public Task<ClaimsPrincipal> CreateAsync(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        var identity = new ClaimsIdentity(IdentityConstants.ApplicationScheme, SessionClaims.Subject, null);
        identity.AddClaim(new Claim(SessionClaims.Subject, user.Id.ToString("D")));
        identity.AddClaim(new Claim(options.Value.ClaimsIdentity.SecurityStampClaimType, user.SecurityStamp ?? string.Empty));
        return Task.FromResult(new ClaimsPrincipal(identity));
    }
}
