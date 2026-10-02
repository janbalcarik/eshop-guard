namespace EshopGuard.Jobs.Runs;

/// <summary>Kinds of the jobs of an analysis run (<c>ops.jobs.kind</c>), one per step (design of change 8, "Plán kroků").</summary>
public static class RunJobKinds
{
    public const string Discover = "run.discover";
    public const string Markets = "run.markets";
    public const string Fetch = "run.fetch";
    public const string Profile = "run.profile";
    public const string Segment = "run.segment";
    public const string Sieve = "run.sieve";
    public const string PlanEvaluate = "run.plan_evaluate";
    public const string Evaluate = "run.evaluate";
    public const string Rules = "run.rules";
    public const string Rewrite = "run.rewrite";
    public const string Finalize = "run.finalize";
}

/// <summary>Codes of runs: why a run failed, events of its progress, why something was not checked. Codes only, never texts.</summary>
public static class RunCodes
{
    // Errors of IRunService.
    public const string SampleAlreadyUsed = "sample.already_used_for_domain";
    public const string RunAlreadyActive = "run.already_active";
    public const string OrderNotPaid = "order_not_paid";
    public const string OrderRunMismatch = "order_run_mismatch";
    public const string NotAwaitingPayment = "run.not_awaiting_payment";
    public const string AlreadyFinished = "run.already_finished";
    public const string ShopNotFound = "shop.not_found";
    public const string RunNotFound = "run.not_found";
    public const string OwnershipNotVerified = "shop.ownership_not_verified";

    // A run that cannot check anything ends failed with one of these.
    public const string RulesInvalid = "rules_invalid";
    public const string RobotsDisallowAll = "robots_disallow_all";
    public const string TargetNotAllowed = "target_not_allowed";
    public const string SiteUnreachable = "site_unreachable";
    public const string NoHtmlPages = "no_html_pages";
    public const string SampleBudgetExceeded = "sample_budget_exceeded";
    public const string InternalError = "internal_error";

    // Events (checks.run_events.code).
    public const string EventStatus = "run.status";
    public const string EventProgress = "run.progress";
    public const string EventThrottled = "crawl.throttled";
    public const string EventWaitingDomain = "crawl.waiting_domain";
    public const string EventSiteUnreachable = "site.unreachable";
    public const string EventPausedInternal = "run.paused_internal";
    public const string EventPartial = "run.partial";
    public const string EventFinished = "run.finished";
    public const string EventFailed = "run.failed";
    public const string EventCanceled = "run.canceled";
    public const string EventVersionNotChecked = "version.not_checked";

    // Why an example fix of the free sample is missing.
    public const string NoRewritableFinding = "no_rewritable_finding";
    public const string RewriteFailed = "rewrite_failed";
    public const string StillFinding = "still_finding";

    // Versions the worker of change 8 does not crawl yet (their addresses are the same as of another checked version).
    public const string VersionSharedUrls = "version_shared_urls_unsupported";
}
