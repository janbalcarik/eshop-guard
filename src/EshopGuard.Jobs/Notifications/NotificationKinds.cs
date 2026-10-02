namespace EshopGuard.Jobs.Notifications;

/// <summary>Which e-mail goes with a kind of notification (change 11, AD 12).</summary>
public enum NotificationEmail
{
    /// <summary>Only in the application.</summary>
    None,

    /// <summary>By the setting <c>email_new_violation</c>.</summary>
    NewViolation,

    /// <summary>By the setting <c>email_run_finished</c>.</summary>
    RunFinished,

    /// <summary>By the setting <c>email_weekly_summary</c>.</summary>
    WeeklySummary,

    /// <summary>Always to the roles editor and above (evidence, protocols, publications).</summary>
    EditorsAlways,
}

/// <summary>A kind of notification: its code (also the template of its e-mail) and its e-mail.</summary>
public sealed record NotificationKind(string Code, NotificationEmail Email);

/// <summary>The kinds of notifications of changes 8, 11, 15 and 16 (AD 12).</summary>
public static class NotificationKinds
{
    public static readonly NotificationKind NewViolation = new("new_violation", NotificationEmail.NewViolation);
    public static readonly NotificationKind RunFinished = new("run_finished", NotificationEmail.RunFinished);
    public static readonly NotificationKind RunPartial = new("run_partial", NotificationEmail.RunFinished);
    public static readonly NotificationKind RunFailed = new("run_failed", NotificationEmail.RunFinished);
    public static readonly NotificationKind SampleFinished = new("sample_finished", NotificationEmail.RunFinished);
    public static readonly NotificationKind WeeklySummary = new("weekly_summary", NotificationEmail.WeeklySummary);
    public static readonly NotificationKind EvidenceExpiring = new("evidence_expiring", NotificationEmail.EditorsAlways);
    public static readonly NotificationKind EvidenceExpired = new("evidence_expired", NotificationEmail.EditorsAlways);
    public static readonly NotificationKind ProtocolReady = new("protocol_ready", NotificationEmail.EditorsAlways);
    public static readonly NotificationKind ProtocolFailed = new("protocol_failed", NotificationEmail.EditorsAlways);
    public static readonly NotificationKind PublicationFailed = new("publication_failed", NotificationEmail.EditorsAlways);
    public static readonly NotificationKind PublicationConflict = new("publication_conflict", NotificationEmail.EditorsAlways);
    public static readonly NotificationKind MarketSuggested = new("market_suggested", NotificationEmail.None);
    public static readonly NotificationKind MarketNowSupported = new("market_now_supported", NotificationEmail.None);
    public static readonly NotificationKind InvitationAccepted = new("invitation_accepted", NotificationEmail.None);

    public static IReadOnlyList<NotificationKind> All { get; } =
    [
        NewViolation, RunFinished, RunPartial, RunFailed, SampleFinished, WeeklySummary, EvidenceExpiring, EvidenceExpired, ProtocolReady, ProtocolFailed,
        PublicationFailed, PublicationConflict, MarketSuggested, MarketNowSupported, InvitationAccepted,
    ];

    public static NotificationKind? Find(string? code) => All.FirstOrDefault(k => k.Code == code);
}

/// <summary>Language-neutral targets of a notification (<c>route.key</c>); the frontend builds the link.</summary>
public static class NotificationRoutes
{
    public const string Run = "runs.item";
    public const string FixesPage = "fixes.page";
    public const string Evidence = "evidence.item";
    public const string Protocol = "protocols.item";
    public const string Publication = "publications.item";
    public const string Overview = "shops.overview";
    public const string Members = "settings.members";
}

/// <summary>Settings <c>Notifications</c>: the e-mails of a member without a row of settings (K rozhodnutí 8, proposal).</summary>
public sealed class NotificationsOptions
{
    public const string SectionName = "Notifications";

    public NotificationDefaults Defaults { get; set; } = new();

    public sealed class NotificationDefaults
    {
        public bool EmailNewViolation { get; set; } = true;

        public bool EmailWeeklySummary { get; set; } = true;

        public bool EmailRunFinished { get; set; } = true;
    }
}
