using Microsoft.Extensions.Options;

namespace EshopGuard.Application.Options;

/// <summary>Settings under <c>Auth</c> (design of change 9, AD 2, AD 7 and AD 8; the numbers are proposals of K rozhodnutí 3).</summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public PasswordSettings Password { get; set; } = new();

    public LockoutSettings Lockout { get; set; } = new();

    /// <summary>Validity of a sign-in link.</summary>
    public int LoginLinkMinutes { get; set; } = 15;

    /// <summary>Pause between two sign-in links (or password resets) of one e-mail address.</summary>
    public int LinkCooldownSeconds { get; set; } = 60;

    /// <summary>Validity of a password reset link.</summary>
    public int ResetLinkMinutes { get; set; } = 60;

    /// <summary>Setting or removing a password without the current one needs a session signed in at most this long ago.</summary>
    public int ReauthenticationMinutes { get; set; } = 15;

    /// <summary>Sliding lifetime of the session cookie.</summary>
    public int SessionDays { get; set; } = 30;

    /// <summary>How often a session checks the security stamp (sign-out everywhere, a new password).</summary>
    public int SecurityStampValidationMinutes { get; set; } = 5;

    public sealed class PasswordSettings
    {
        public int MinLength { get; set; } = 10;
    }

    public sealed class LockoutSettings
    {
        public int MaxFailedAttempts { get; set; } = 5;

        public int Minutes { get; set; } = 15;
    }
}

/// <summary>Settings under <c>Frontend</c>: where the links of e-mails lead (no secret).</summary>
public sealed class FrontendOptions
{
    public const string SectionName = "Frontend";

    /// <summary>Origin of the web application, e.g. <c>https://app.eshopguard.sk</c> (no trailing slash).</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Page 2c; the token follows in the fragment (<c>#t=…</c>).</summary>
    public string LoginLinkPath { get; set; } = "/prihlasenie/odkaz";

    public string ResetPath { get; set; } = "/prihlasenie/nove-heslo";

    public string InvitationPath { get; set; } = "/pozvanka";

    /// <summary>Page 2a; errors of Google come back as <c>?error=code</c>.</summary>
    public string LoginPath { get; set; } = "/prihlasenie";

    /// <summary>A run in the application: <c>{tenantId}</c> and <c>{runId}</c> are replaced.</summary>
    public string RunPath { get; set; } = "/app/{tenantId}/behy/{runId}";

    /// <summary>The members of a tenant: <c>{tenantId}</c> is replaced.</summary>
    public string MembersPath { get; set; } = "/app/{tenantId}/nastavenia/clenovia";

    /// <summary>
    /// Targets of notifications (<c>route.key</c>, change 11): <c>{tenantId}</c> and the parameters of the route
    /// (<c>{shopId}</c>, <c>{evidenceId}</c>, …) are replaced.
    /// </summary>
    public Dictionary<string, string> RoutePaths { get; set; } = new(StringComparer.Ordinal)
    {
        ["runs.item"] = "/app/{tenantId}/behy/{runId}",
        ["shops.overview"] = "/app/{tenantId}/obchody/{shopId}",
        ["fixes.page"] = "/app/{tenantId}/obchody/{shopId}/opravy/{pageId}",
        ["evidence.item"] = "/app/{tenantId}/doklady/{evidenceId}",
        ["protocols.item"] = "/app/{tenantId}/obchody/{shopId}/protokoly/{protocolId}",
        ["publications.item"] = "/app/{tenantId}/obchody/{shopId}/publikacie/{publicationId}",
        ["settings.members"] = "/app/{tenantId}/nastavenia/clenovia",
        ["billing.overview"] = "/app/{tenantId}/predplatne",
    };

    /// <summary>The link of a target of a notification; an unknown key leads to the overview of the tenant.</summary>
    public string Route(Guid tenantId, string? key, IReadOnlyDictionary<string, string?> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var path = key is not null && RoutePaths.TryGetValue(key, out var p) ? p : "/app/{tenantId}";
        path = path.Replace("{tenantId}", tenantId.ToString("D"), StringComparison.Ordinal);
        foreach (var (name, value) in parameters)
        {
            path = path.Replace("{" + name + "}", Uri.EscapeDataString(value ?? ""), StringComparison.Ordinal);
        }

        return BaseUrl + path;
    }
}

/// <summary>Settings under <c>Email</c>; the SMTP password only from user-secrets or the environment.</summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string From { get; set; } = string.Empty;

    public string FromName { get; set; } = "EshopGuard";

    /// <summary>Time limit of one send of an e-mail with a token (in the request); one more attempt follows.</summary>
    public int SendTimeoutSeconds { get; set; } = 10;

    /// <summary>Attempts of an e-mail of the outbox before it stays unsent with its error code.</summary>
    public int MaxAttempts { get; set; } = 8;

    public SmtpSettings Smtp { get; set; } = new();

    public sealed class SmtpSettings
    {
        public string Host { get; set; } = string.Empty;

        public int Port { get; set; } = 587;

        /// <summary><c>StartTls</c> (default), <c>SslOnConnect</c> or <c>None</c> (Mailpit on localhost).</summary>
        public string Security { get; set; } = "StartTls";

        public string? UserName { get; set; }

        public string? Password { get; set; }
    }
}

/// <summary>Settings under <c>Security</c>.</summary>
public sealed class SecurityOptions
{
    public const string SectionName = "Security";

    /// <summary>Key of the HMAC of e-mail addresses and IP addresses (keys of limits, audit); at least 32 bytes in base64.</summary>
    public string IpHashKey { get; set; } = string.Empty;
}

/// <summary>Settings under <c>Tenants</c>.</summary>
public sealed class TenantsOptions
{
    public const string SectionName = "Tenants";

    /// <summary>Tenants one user may own (an agency); proposal of K rozhodnutí 4.</summary>
    public int MaxOwnedPerUser { get; set; } = 20;
}

/// <summary>Settings under <c>Invitations</c>.</summary>
public sealed class InvitationsOptions
{
    public const string SectionName = "Invitations";

    public int ValidDays { get; set; } = 7;
}

/// <summary>Settings under <c>Localization</c>.</summary>
public sealed class LocalizationOptions
{
    public const string SectionName = "Localization";

    /// <summary>Market of a request without a tenant and without a market (the Slovak edition of the web).</summary>
    public string DefaultMarket { get; set; } = "sk";

    /// <summary>Time zone of the times in e-mails.</summary>
    public string TimeZone { get; set; } = "Europe/Bratislava";
}

/// <summary>Settings under <c>Legal</c>: versions of the documents a new account accepts.</summary>
public sealed class LegalOptions
{
    public const string SectionName = "Legal";

    public string TermsVersion { get; set; } = string.Empty;

    public string PrivacyVersion { get; set; } = string.Empty;
}

/// <summary>Refuses the start with a code when a required setting is missing (fail-closed); never prints a value.</summary>
internal sealed class ApplicationOptionsValidator :
    IValidateOptions<AuthOptions>, IValidateOptions<FrontendOptions>, IValidateOptions<SecurityOptions>, IValidateOptions<EmailOptions>, IValidateOptions<LegalOptions>
{
    public const string Code = "config.application_invalid";

    public ValidateOptionsResult Validate(string? name, AuthOptions options) => Collect(
        (options.Password.MinLength < 8, "Auth:Password:MinLength"),
        (options.Lockout.MaxFailedAttempts < 1, "Auth:Lockout:MaxFailedAttempts"),
        (options.Lockout.Minutes < 1, "Auth:Lockout:Minutes"),
        (options.LoginLinkMinutes is < 1 or > 60, "Auth:LoginLinkMinutes"),
        (options.LinkCooldownSeconds < 0, "Auth:LinkCooldownSeconds"),
        (options.ResetLinkMinutes is < 1 or > 24 * 60, "Auth:ResetLinkMinutes"),
        (options.ReauthenticationMinutes < 1, "Auth:ReauthenticationMinutes"),
        (options.SessionDays < 1, "Auth:SessionDays"),
        (options.SecurityStampValidationMinutes < 1, "Auth:SecurityStampValidationMinutes"));

    public ValidateOptionsResult Validate(string? name, FrontendOptions options) => Collect(
        (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http") || options.BaseUrl.EndsWith('/'), "Frontend:BaseUrl"),
        (!options.LoginLinkPath.StartsWith('/'), "Frontend:LoginLinkPath"),
        (!options.ResetPath.StartsWith('/'), "Frontend:ResetPath"),
        (!options.InvitationPath.StartsWith('/'), "Frontend:InvitationPath"),
        (!options.LoginPath.StartsWith('/'), "Frontend:LoginPath"));

    public ValidateOptionsResult Validate(string? name, SecurityOptions options) => Collect(
        (!IsKey(options.IpHashKey), "Security:IpHashKey"));

    public ValidateOptionsResult Validate(string? name, EmailOptions options) => Collect(
        (string.IsNullOrWhiteSpace(options.From) || !options.From.Contains('@', StringComparison.Ordinal), "Email:From"),
        (options.SendTimeoutSeconds is < 1 or > 60, "Email:SendTimeoutSeconds"),
        (options.MaxAttempts < 1, "Email:MaxAttempts"));

    public ValidateOptionsResult Validate(string? name, LegalOptions options) => Collect(
        (string.IsNullOrWhiteSpace(options.TermsVersion), "Legal:TermsVersion"),
        (string.IsNullOrWhiteSpace(options.PrivacyVersion), "Legal:PrivacyVersion"));

    private static bool IsKey(string value)
    {
        Span<byte> bytes = stackalloc byte[256];
        return !string.IsNullOrWhiteSpace(value) && Convert.TryFromBase64String(value, bytes, out var written) && written >= 32;
    }

    private static ValidateOptionsResult Collect(params (bool Invalid, string Key)[] checks)
    {
        var invalid = checks.Where(c => c.Invalid).Select(c => $"{Code}: {c.Key}").ToList();
        return invalid.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(invalid);
    }
}
