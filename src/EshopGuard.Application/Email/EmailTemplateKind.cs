namespace EshopGuard.Application.Email;

/// <summary>
/// A kind of e-mail: its template <c>Email/Templates/{locale}/{Code}.{subject.txt,html,txt}</c>, its parameters and whether it
/// carries a one-time token (AD 3: such e-mails go out directly from the request and are never composed from the outbox).
/// Boolean parameters open sections <c>{{#name}}…{{/name}}</c> and <c>{{^name}}…{{/name}}</c>.
/// </summary>
public sealed record EmailTemplateKind(string Code, bool ContainsToken, IReadOnlyList<string> Parameters, IReadOnlyList<string> Flags)
{
    public static readonly EmailTemplateKind LoginLink = new("login_link", true, ["email", "link", "minutes"], ["isNewAccount"]);
    public static readonly EmailTemplateKind PasswordReset = new("password_reset", true, ["email", "link", "minutes"], []);
    public static readonly EmailTemplateKind Invitation = new("invitation", true, ["email", "link", "days", "tenantName", "inviterName", "roleName"], []);
    public static readonly EmailTemplateKind InvitationAccepted = new("invitation_accepted", false, ["email", "link", "memberEmail", "tenantName", "roleName"], []);
    public static readonly EmailTemplateKind SampleFinished = new("sample_finished", false, ["email", "link", "shopName", "pages", "findings"], []);
    public static readonly EmailTemplateKind RunFinished = new("run_finished", false, ["email", "link", "shopName", "pages", "findings"], []);
    public static readonly EmailTemplateKind RunPartial = new("run_partial", false, ["email", "link", "shopName", "pages", "findings", "unchecked"], []);
    public static readonly EmailTemplateKind RunFailed = new("run_failed", false, ["email", "link", "shopName"], []);

    // Notifications of change 11 (AD 12): the values come from the parameters of the notification and its target.
    public static readonly EmailTemplateKind NewViolation = new("new_violation", false, ["email", "link", "shopName", "findings"], []);
    public static readonly EmailTemplateKind EvidenceExpiring = new("evidence_expiring", false, ["email", "link", "days"], []);
    public static readonly EmailTemplateKind EvidenceExpired = new("evidence_expired", false, ["email", "link"], []);
    public static readonly EmailTemplateKind ProtocolReady = new("protocol_ready", false, ["email", "link", "shopName", "number"], []);
    public static readonly EmailTemplateKind ProtocolFailed = new("protocol_failed", false, ["email", "link", "shopName"], []);
    public static readonly EmailTemplateKind PublicationFailed = new("publication_failed", false, ["email", "link", "shopName"], []);
    public static readonly EmailTemplateKind PublicationConflict = new("publication_conflict", false, ["email", "link", "shopName"], []);

    // Billing (change 12): amounts and dates come as codes and are formatted by the language of the recipient.
    public static readonly EmailTemplateKind TrialEnding = new("trial_ending", false, ["email", "link", "shopName", "amount", "date"], []);
    public static readonly EmailTemplateKind PaymentFailed = new("payment_failed", false, ["email", "link", "shopName", "amount"], []);
    public static readonly EmailTemplateKind SubscriptionEnded = new("subscription_ended", false, ["email", "link", "shopName", "date"], ["paymentFailed"]);
    /// <summary><c>lockedUntil</c>: the end of the locked price of a founder (<c>founder</c>), otherwise the date of the change.</summary>
    public static readonly EmailTemplateKind PriceChange = new("price_change", false, ["email", "link", "shopName", "oldAmount", "newAmount", "date", "lockedUntil"], ["decrease", "founder"]);
    public static readonly EmailTemplateKind TierChange = new("tier_change", false, ["email", "link", "shopName", "oldAmount", "newAmount", "date", "products"], ["decrease"]);
    public static readonly EmailTemplateKind PriceChangeCanceled = new("price_change_canceled", false, ["email", "link", "shopName", "date"], []);

    /// <summary>The kinds whose values come from a notification (<c>NotificationDispatcher</c>) rather than from a run.</summary>
    public static IReadOnlyList<EmailTemplateKind> Notifications { get; } =
        [NewViolation, EvidenceExpiring, EvidenceExpired, ProtocolReady, ProtocolFailed, PublicationFailed, PublicationConflict,
            TrialEnding, PaymentFailed, SubscriptionEnded, PriceChange, TierChange, PriceChangeCanceled];

    public static IReadOnlyList<EmailTemplateKind> All { get; } =
        [LoginLink, PasswordReset, Invitation, InvitationAccepted, SampleFinished, RunFinished, RunPartial, RunFailed,
            NewViolation, EvidenceExpiring, EvidenceExpired, ProtocolReady, ProtocolFailed, PublicationFailed, PublicationConflict,
            TrialEnding, PaymentFailed, SubscriptionEnded, PriceChange, TierChange, PriceChangeCanceled];

    public static EmailTemplateKind? Find(string? code) => All.FirstOrDefault(k => k.Code == code);
}
