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

    public const string ShopNotFound = "shop.not_found";
    public const string ShopUrlInvalid = "shop.url_invalid";
    public const string ShopUrlNotAllowed = "shop.url_not_allowed";
    public const string ShopAlreadyExists = "shop.already_exists";
    public const string ShopSubscriptionActive = "shop.subscription_active";
    public const string ShopRunInProgress = "shop.run_in_progress";
    public const string ShopConnectorNotConnected = "shop.connector_not_connected";
    public const string ShopOwnershipNotVerified = "shop.ownership_not_verified";
    public const string ShopStatusNotOrderable = "shop.status_not_orderable";
    public const string DetectionInProgress = "detection.in_progress";
    public const string PlatformUnknown = "platform.unknown";
    public const string FeedUrlInvalid = "feed.url_invalid";
    public const string FeedFormatUnknown = "feed.format_unknown";
    public const string SourceModeUnknown = "source.mode_unknown";

    public const string SampleAlreadyUsedForDomain = "sample.already_used_for_domain";
    public const string SampleNotAllowedInStatus = "sample.not_allowed_in_status";
    public const string SampleNotStarted = "sample.not_started";
    public const string SampleNotFinished = "sample.not_finished";

    public const string MarketsNoneSelected = "markets.none_selected";
    public const string MarketsUnsupported = "markets.unsupported";
    public const string MarketsUnknown = "markets.unknown";
    public const string MarketsLockedDuringRun = "markets.locked_during_run";
    public const string MarketsNotConfirmed = "markets.not_confirmed";

    public const string LanguageNotFound = "language.not_found";
    public const string LanguageNotAwaitingConfirmation = "language.not_awaiting_confirmation";
    public const string LanguageLastCheckedVersion = "language.last_checked_version";
    public const string LanguageAwaitingConfirmation = "language.awaiting_confirmation";
    public const string LanguagesConfirmationPending = "languages.confirmation_pending";

    public const string ScopeBasisMissing = "scope.basis_missing";
    public const string ScopeNoCheckableVersion = "scope.no_checkable_version";
    public const string ScopeProductCountUnknown = "scope.product_count_unknown";
    public const string QuoteSampleNotFinished = "quote.sample_not_finished";
    public const string QuoteBasisMissing = "quote.basis_missing";
    public const string BillingUnavailable = "billing.unavailable";

    public const string OwnershipMethodUnknown = "ownership.method_unknown";
    public const string OwnershipAlreadyVerified = "ownership.already_verified";
    public const string OwnershipVerificationNotFound = "ownership.verification_not_found";

    public const string SettingsNoModule = "settings.no_module";
    public const string SettingsModuleUnavailable = "settings.module_unavailable";
    public const string SettingsHiddenCheckRequiresConnector = "settings.hidden_check_requires_connector";
    public const string SettingsHiddenCheckUnsupportedPlatform = "settings.hidden_check_unsupported_platform";

    public const string ShopSampleOnly = "shop.sample_only";
    public const string FindingNotFound = "finding.not_found";
    public const string FindingTransitionNotAllowed = "finding.transition_not_allowed";
    public const string PageNotFound = "page.not_found";
    public const string PageNoAcceptedChanges = "page.no_accepted_changes";
    public const string SearchQueryTooShort = "search.query_too_short";
    public const string CatalogLocaleIncomplete = "catalog.locale_incomplete";

    public const string ProposalNotFound = "proposal.not_found";
    public const string ProposalAlternativeUnknown = "proposal.alternative_unknown";
    public const string ProposalTextEmpty = "proposal.text_empty";
    public const string ProposalTextUnchanged = "proposal.text_unchanged";
    public const string ProposalTextTooLong = "proposal.text_too_long";
    public const string ProposalLocked = "proposal.locked";
    public const string ProposalPlaceholderUnknown = "proposal.placeholder_unknown";
    public const string ProposalPlaceholderEmpty = "proposal.placeholder_empty";
    public const string ProposalPlaceholderMissing = "proposal.placeholder_missing";
    public const string ProposalRecheckPending = "proposal.recheck_pending";
    public const string ProposalRecheckFailed = "proposal.recheck_failed";
    public const string ProposalAlreadyPublished = "proposal.already_published";

    public const string QuestionNotFound = "question.not_found";
    public const string QuestionAnswerLocked = "question.answer_locked";
    public const string BudgetDailyLimitReached = "budget.daily_limit_reached";

    public const string GroupNotFound = "group.not_found";
    public const string GroupPlaceholderUnknown = "group.placeholder_unknown";
    public const string GroupLocked = "group.locked";
    public const string GroupCustomTextRequired = "group.custom_text_required";
    public const string GroupPageNotInGroup = "group.page_not_in_group";
    public const string GroupNoPagesLeft = "group.no_pages_left";
    public const string GroupValueMissing = "group.value_missing";
    public const string GroupRecheckPending = "group.recheck_pending";
    public const string GroupRecheckFailed = "group.recheck_failed";
    public const string GroupPageNeedsIndividualFix = "group.page_needs_individual_fix";
    public const string GroupAlreadyPublished = "group.already_published";

    public const string PublicationNotAvailable = "publication.not_available";
    public const string PublicationConnectorUnavailable = "publication.connector_unavailable";
    public const string PublicationNothingToPublish = "publication.nothing_to_publish";
    public const string PublicationNotFound = "publication.not_found";
    public const string PublicationNotRollbackable = "publication.not_rollbackable";

    public const string EvidenceNotFound = "evidence.not_found";
    public const string EvidenceFileTypeNotAllowed = "evidence.file_type_not_allowed";
    public const string EvidenceFileTooLarge = "evidence.file_too_large";
    public const string EvidenceClaimRequired = "evidence.claim_required";
    public const string EvidenceValidUntilBeforeFrom = "evidence.valid_until_before_from";
    public const string EvidenceNoFile = "evidence.no_file";

    public const string ProtocolNotFound = "protocol.not_found";
    public const string ProtocolPeriodInvalid = "protocol.period_invalid";
    public const string ProtocolNoCompletedRun = "protocol.no_completed_run";
    public const string ProtocolNotReady = "protocol.not_ready";
    public const string ProtocolFailed = "protocol.failed";

    public const string NotificationNotFound = "notification.not_found";
    public const string RunNotFound = "run.not_found";
    public const string RunNotCancelable = "run.not_cancelable";
    public const string SseTooManyConnections = "sse.too_many_connections";

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
        public const string UrlInvalid = "url.invalid";
        public const string ValueUnknown = "value.unknown";
        public const string ValueNotAllowed = "value.not_allowed";
    }
}
