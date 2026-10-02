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

    public static IReadOnlyList<EmailTemplateKind> All { get; } =
        [LoginLink, PasswordReset, Invitation, InvitationAccepted, SampleFinished, RunFinished, RunPartial, RunFailed];

    public static EmailTemplateKind? Find(string? code) => All.FirstOrDefault(k => k.Code == code);
}
