using EshopGuard.Application.Audit;
using EshopGuard.Application.Email;
using EshopGuard.Application.Email.Smtp;
using EshopGuard.Application.Identity;
using EshopGuard.Application.Localization;
using EshopGuard.Application.Me;
using EshopGuard.Application.Options;
using EshopGuard.Application.RateLimits;
using EshopGuard.Application.Security;
using EshopGuard.Application.Tenants;
using EshopGuard.Data.Entities.Iam;
using EshopGuard.Jobs;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace EshopGuard.Application;

/// <summary>Registration of the services of change 9. Requires <c>AddEshopGuardData</c> (and the job queue for the API).</summary>
public static class ApplicationServiceCollectionExtensions
{
    /// <summary>
    /// The services of the API: identity over <c>iam.users</c> (<see cref="EgUserStore"/>, password hash V3, options from
    /// <c>Auth:*</c>), tokens, limits, e-mails, languages, audit, tenants, members and invitations. Invalid settings stop the
    /// start (<c>config.application_invalid</c>). Returns the builder of Identity (the API adds the sign-in manager).
    /// </summary>
    public static IdentityBuilder AddEshopGuardApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddEshopGuardEmailDelivery();
        services.AddOptions<AuthOptions>().BindConfiguration(AuthOptions.SectionName).ValidateOnStart();
        services.AddOptions<SecurityOptions>().BindConfiguration(SecurityOptions.SectionName).ValidateOnStart();
        services.AddOptions<LegalOptions>().BindConfiguration(LegalOptions.SectionName).ValidateOnStart();
        services.AddOptions<TenantsOptions>().BindConfiguration(TenantsOptions.SectionName);
        services.AddOptions<InvitationsOptions>().BindConfiguration(InvitationsOptions.SectionName);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<AuthOptions>, ApplicationOptionsValidator>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<SecurityOptions>, ApplicationOptionsValidator>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<LegalOptions>, ApplicationOptionsValidator>());

        services.TryAddSingleton<IpHasher>();
        services.TryAddSingleton<IRateLimitBuckets, PgRateLimitBuckets>();
        services.TryAddSingleton<AuthRateLimits>();
        services.TryAddSingleton<DirectEmailSender>();
        services.TryAddScoped<RequestContext>();
        services.TryAddScoped<SecurityAuditWriter>();
        services.TryAddScoped<OneTimeTokenService>();
        services.TryAddScoped<AccountService>();
        services.TryAddScoped<LoginLinkService>();
        services.TryAddScoped<PasswordService>();
        services.TryAddScoped<ExternalLoginService>();
        services.TryAddScoped<MeService>();
        services.TryAddScoped<TenantService>();
        services.TryAddScoped<TenantAccessService>();
        services.TryAddScoped<MembershipService>();
        services.TryAddScoped<InvitationService>();

        services.AddSingleton<IConfigureOptions<IdentityOptions>, IdentityOptionsSetup>();
        var identity = services.AddIdentityCore<User>().AddUserStore<EgUserStore>();
        services.Replace(ServiceDescriptor.Scoped<ILookupNormalizer, EmailLookupNormalizer>());
        services.TryAddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        return identity;
    }

    /// <summary>
    /// Delivery of the e-mails of the outbox (the worker; the API gets it through <see cref="AddEshopGuardApplication"/>):
    /// templates, the SMTP transport, the catalog of languages and the handler of <c>email.send</c>. Validates <c>Email</c>
    /// and <c>Frontend</c> at start.
    /// </summary>
    public static IServiceCollection AddEshopGuardEmailDelivery(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddOptions<EmailOptions>().BindConfiguration(EmailOptions.SectionName).ValidateOnStart();
        services.AddOptions<FrontendOptions>().BindConfiguration(FrontendOptions.SectionName).ValidateOnStart();
        services.AddOptions<LocalizationOptions>().BindConfiguration(LocalizationOptions.SectionName);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<EmailOptions>, ApplicationOptionsValidator>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<FrontendOptions>, ApplicationOptionsValidator>());
        services.AddMemoryCache();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<EmailComposer>();
        services.TryAddSingleton<IEmailTransport, SmtpEmailTransport>();
        services.TryAddSingleton<IRefCatalog, RefCatalog>();
        services.TryAddSingleton<LocaleResolver>();
        services.AddJobHandler<EmailSendHandler>();
        return services;
    }

    /// <summary>Password and lockout of Identity from <c>Auth:*</c> (AD 8): only the length is required, the rest is off.</summary>
    private sealed class IdentityOptionsSetup(IOptions<AuthOptions> auth) : IConfigureOptions<IdentityOptions>
    {
        public void Configure(IdentityOptions options)
        {
            var settings = auth.Value;
            options.Password.RequiredLength = settings.Password.MinLength;
            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequiredUniqueChars = 1;
            options.Lockout.MaxFailedAccessAttempts = settings.Lockout.MaxFailedAttempts;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(settings.Lockout.Minutes);
            options.Lockout.AllowedForNewUsers = true;
            options.User.RequireUniqueEmail = true;
            options.User.AllowedUserNameCharacters = string.Empty;
            options.SignIn.RequireConfirmedEmail = false;
        }
    }
}
