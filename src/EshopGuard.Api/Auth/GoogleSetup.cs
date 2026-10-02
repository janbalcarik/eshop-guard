using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using EshopGuard.Application.Options;
using EshopGuard.Application.Problems;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace EshopGuard.Api.Auth;

/// <summary>
/// Google (AD 9), only when <c>Authentication:Google:ClientId</c> is set (Client ID and Secret from user-secrets or the
/// environment); without it <c>/api/auth/google/start</c> answers <c>404</c> and the rest of the API runs. The callback of
/// the handler is <c>/api/auth/google/signin</c>; <c>email_verified</c> is mapped from both forms of Google's answer.
/// </summary>
public static class GoogleSetup
{
    public const string Scheme = GoogleDefaults.AuthenticationScheme;
    public const string CallbackPath = "/api/auth/google/signin";
    public const string EmailVerifiedClaim = "email_verified";

    public static bool IsConfigured(IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(configuration?["Authentication:Google:ClientId"]);

    public static IServiceCollection AddGoogleSignIn(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (!IsConfigured(configuration))
        {
            return services;
        }

        services.AddAuthentication().AddGoogle(o =>
        {
            o.ClientId = configuration["Authentication:Google:ClientId"]!;
            o.ClientSecret = configuration["Authentication:Google:ClientSecret"] ?? string.Empty;
            o.SignInScheme = IdentityConstants.ExternalScheme;
            o.CallbackPath = CallbackPath;
            o.Scope.Clear();
            o.Scope.Add("openid");
            o.Scope.Add("email");
            o.Scope.Add("profile");
            o.SaveTokens = false;
            o.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "sub");
            o.ClaimActions.MapJsonKey(EmailVerifiedClaim, "email_verified");
            o.ClaimActions.MapJsonKey(EmailVerifiedClaim, "verified_email");
            o.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;

            // A refusal or an error at Google (or a forged callback) goes back to the sign-in page with a code, never a 500.
            o.Events.OnRemoteFailure = context =>
            {
                var frontend = context.HttpContext.RequestServices.GetRequiredService<IOptions<FrontendOptions>>().Value;
                context.Response.Redirect(frontend.BaseUrl + frontend.LoginPath + "?error=" + ProblemCodes.GoogleFailed);
                context.HandleResponse();
                return Task.CompletedTask;
            };
        });
        return services;
    }
}
