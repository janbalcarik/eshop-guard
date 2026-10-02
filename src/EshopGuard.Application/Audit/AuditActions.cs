namespace EshopGuard.Application.Audit;

/// <summary>Actions of the security audit (AD 13); events without a tenant are only <c>auth.*</c> and <c>user.*</c>.</summary>
public static class AuditActions
{
    public const string LoginLinkRequested = "auth.login_link_requested";
    public const string LoginSucceeded = "auth.login_succeeded";
    public const string LoginFailed = "auth.login_failed";
    public const string LockedOut = "auth.locked_out";
    public const string PasswordSet = "auth.password_set";
    public const string PasswordRemoved = "auth.password_removed";
    public const string PasswordResetRequested = "auth.password_reset_requested";
    public const string PasswordReset = "auth.password_reset";
    public const string GoogleLinked = "auth.google_linked";
    public const string GoogleUnlinked = "auth.google_unlinked";
    public const string LogoutEverywhere = "auth.logout_everywhere";

    public const string UserCreated = "user.created";
    public const string TermsAccepted = "user.terms_accepted";
    public const string UserUpdated = "user.updated";

    public const string TenantCreated = "tenant.created";
    public const string TenantRenamed = "tenant.renamed";

    public const string MembershipRoleChanged = "membership.role_changed";
    public const string MembershipRemoved = "membership.removed";
    public const string MembershipLeft = "membership.left";
    public const string OwnershipTransferred = "membership.ownership_transferred";

    public const string InvitationCreated = "invitation.created";
    public const string InvitationResent = "invitation.resent";
    public const string InvitationRevoked = "invitation.revoked";
    public const string InvitationAccepted = "invitation.accepted";

    /// <summary>Methods of a sign-in (<c>data.method</c> and the claim <c>amr</c> of the session).</summary>
    public static class Methods
    {
        public const string MagicLink = "magic_link";
        public const string Password = "password";
        public const string Google = "google";
        public const string Invitation = "invitation";
        public const string Reset = "reset";
    }
}
