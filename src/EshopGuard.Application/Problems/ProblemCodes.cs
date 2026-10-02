namespace EshopGuard.Application.Problems;

/// <summary>
/// Codes of the errors of the API (change 9, AD 12). A response carries the code and its parameters, never a sentence; the
/// frontend composes the text from <c>messages/{locale}.json</c>.
/// </summary>
public static class ProblemCodes
{
    public const string ValidationFailed = "validation.failed";
    public const string RateLimited = "rate_limited";
    public const string InternalError = "internal_error";
    public const string NotFound = "not_found";
    public const string CsrfInvalid = "csrf.invalid";
    public const string ConcurrencyConflict = "concurrency.conflict";
    public const string RequestInvalid = "request.invalid";
    public const string HttpsRequired = "request.https_required";

    public const string EmailSendFailed = "email.send_failed";
    public const string LoginLinkInvalid = "login_link.invalid";
    public const string LoginLinkExpired = "login_link.expired";
    public const string LoginLinkUsed = "login_link.used";
    public const string ResetLinkInvalid = "reset_link.invalid";
    public const string ResetLinkExpired = "reset_link.expired";
    public const string ResetLinkUsed = "reset_link.used";

    public const string AuthUnauthenticated = "auth.unauthenticated";
    public const string AuthAccountDisabled = "auth.account_disabled";
    public const string AuthInvalidCredentials = "auth.invalid_credentials";
    public const string AuthReauthenticationRequired = "auth.reauthentication_required";
    public const string AuthForbiddenRole = "auth.forbidden_role";

    public const string PasswordCurrentInvalid = "password.current_invalid";
    public const string ReturnPathInvalid = "return_path.invalid";
    public const string GoogleEmailNotVerified = "google.email_not_verified";
    public const string GoogleFailed = "google.failed";
    public const string LoginNotLinked = "login.not_linked";
    public const string LocaleNotEnabled = "locale.not_enabled";
    public const string MarketUnknown = "market.unknown";

    public const string TenantNotFound = "tenant.not_found";
    public const string TenantSuspended = "tenant.suspended";
    public const string TenantLimitReached = "tenant.limit_reached";
    public const string MembershipNotFound = "membership.not_found";
    public const string MembershipLastOwner = "membership.last_owner";
    public const string MembershipRoleNotAllowed = "membership.role_not_allowed";

    public const string InvitationNotFound = "invitation.not_found";
    public const string InvitationInvalid = "invitation.invalid";
    public const string InvitationExpired = "invitation.expired";
    public const string InvitationUsed = "invitation.used";
    public const string InvitationEmailMismatch = "invitation.email_mismatch";
    public const string InvitationAlreadyMember = "invitation.already_member";

    /// <summary>Codes of single fields inside <see cref="ValidationFailed"/>.</summary>
    public static class Fields
    {
        public const string Required = "value.required";
        public const string EmailInvalidFormat = "email.invalid_format";
        public const string PasswordTooShort = "password.too_short";
        public const string PasswordTooLong = "password.too_long";
        public const string PasswordSameAsEmail = "password.same_as_email";
        public const string NameTooLong = "name.too_long";
        public const string RoleInvalid = "role.invalid";
        public const string CodeInvalid = "code.invalid";
    }
}
