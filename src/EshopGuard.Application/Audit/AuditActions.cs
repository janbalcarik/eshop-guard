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
    public const string TenantBillingDetailsUpdated = "tenant.billing_details_updated";

    public const string MembershipRoleChanged = "membership.role_changed";
    public const string MembershipRemoved = "membership.removed";
    public const string MembershipLeft = "membership.left";
    public const string OwnershipTransferred = "membership.ownership_transferred";

    public const string InvitationCreated = "invitation.created";
    public const string InvitationResent = "invitation.resent";
    public const string InvitationRevoked = "invitation.revoked";
    public const string InvitationAccepted = "invitation.accepted";

    public const string ShopCreated = "shop.created";
    public const string ShopRenamed = "shop.renamed";
    public const string ShopDeleted = "shop.deleted";
    public const string ShopPlatformSet = "shop.platform_set";
    public const string ShopSourceChanged = "shop.source_changed";
    public const string SampleStarted = "sample.started";
    public const string MarketsConfirmed = "markets.confirmed";
    public const string LanguageConfirmed = "language.confirmed";
    public const string LanguageRejectedOtherDomain = "language.rejected_other_domain";
    public const string LanguageExcluded = "language.excluded";
    public const string LanguageIncluded = "language.included";
    public const string OwnershipVerificationCreated = "ownership.verification_created";
    public const string OwnershipVerified = "ownership.verified";
    public const string SettingsChanged = "settings.changed";

    public const string FindingStatusChanged = "finding.status_changed";
    public const string FindingsExported = "findings.exported";
    public const string ProposalAlternativeSelected = "proposal.alternative_selected";
    public const string ProposalEdited = "proposal.edited";
    public const string ProposalPlaceholdersFilled = "proposal.placeholders_filled";
    public const string ProposalAccepted = "proposal.accepted";
    public const string ProposalRejected = "proposal.rejected";
    public const string ProposalUnaccepted = "proposal.unaccepted";
    public const string QuestionAnswered = "question.answered";
    public const string EvidenceCreated = "evidence.created";
    public const string EvidenceUpdated = "evidence.updated";
    public const string EvidenceDeleted = "evidence.deleted";
    public const string EvidenceLinked = "evidence.linked";
    public const string EvidenceUnlinked = "evidence.unlinked";
    public const string GroupValuesSet = "group.values_set";
    public const string GroupModeSet = "group.mode_set";
    public const string GroupPagesExcluded = "group.pages_excluded";
    public const string GroupApproved = "group.approved";
    public const string GroupPageApproved = "group.page_approved";
    public const string GroupUnapproved = "group.unapproved";
    public const string PublicationRequested = "publication.requested";
    public const string PublicationRollbackRequested = "publication.rollback_requested";
    public const string ProtocolRequested = "protocol.requested";
    public const string NotificationSettingsChanged = "notification.settings_changed";
    public const string RunCancelRequested = "run.cancel_requested";

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
