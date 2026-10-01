namespace EshopGuard.Data.Entities.Checks;

/// <summary>Values of <c>RunKind</c> (stored as snake_case text with a CHECK constraint).</summary>
public enum RunKind
{
    FreeSample,
    FullAnalysis,
    Monitoring,
    ConnectorCheck,
    Recheck,
    RuleUpdate,
    ProfileRefresh,
}

/// <summary>Values of <c>RunTrigger</c> (stored as snake_case text with a CHECK constraint).</summary>
public enum RunTrigger
{
    User,
    Schedule,
    Webhook,
    System,
}

/// <summary>Values of <c>RunStatus</c> (stored as snake_case text with a CHECK constraint).</summary>
public enum RunStatus
{
    Queued,
    Discovering,
    AwaitingPayment,
    Crawling,
    Profiling,
    Segmenting,
    Evaluating,
    Ruling,
    Rewriting,
    Finished,
    Partial,
    Failed,
    Canceled,
}

/// <summary>Values of <c>Checkability</c> (stored as snake_case text with a CHECK constraint).</summary>
public enum Checkability
{
    Text,
    Assess,
    Verify,
}

/// <summary>Values of <c>FindingBand</c> (stored as snake_case text with a CHECK constraint).</summary>
public enum FindingBand
{
    High,
    Review,
}

/// <summary>Values of <c>FindingScope</c> (stored as snake_case text with a CHECK constraint).</summary>
public enum FindingScope
{
    Segment,
    Page,
    Site,
}

/// <summary>Values of <c>FindingStatus</c> (stored as snake_case text with a CHECK constraint).</summary>
public enum FindingStatus
{
    Open,
    NeedsAnswer,
    Proposed,
    Approved,
    Published,
    Kept,
    KeptWithEvidence,
    Dismissed,
    Resolved,
}

/// <summary>Values of <c>QuestionScope</c> (stored as snake_case text with a CHECK constraint).</summary>
public enum QuestionScope
{
    Finding,
    Site,
}

/// <summary>Values of <c>QuestionStatus</c> (stored as snake_case text with a CHECK constraint).</summary>
public enum QuestionStatus
{
    Open,
    Answered,
}

/// <summary>Values of <c>PageChangeSource</c> (stored as snake_case text with a CHECK constraint).</summary>
public enum PageChangeSource
{
    Webhook,
    Crawl,
    Feed,
    SaveHidden,
}

/// <summary>Values of <c>PageChangeKind</c> (stored as snake_case text with a CHECK constraint).</summary>
public enum PageChangeKind
{
    New,
    TextChanged,
    Removed,
    HiddenSaved,
    FixPublished,
}

/// <summary>Values of <c>PageChangeResult</c> (stored as snake_case text with a CHECK constraint).</summary>
public enum PageChangeResult
{
    NewViolation,
    NewAssess,
    FixConfirmed,
    Ok,
}
