namespace EshopGuard.Data.Entities.Fixes;

/// <summary>Values of <c>FixGroupKind</c> (stored as snake_case text with a CHECK constraint).</summary>
public enum FixGroupKind
{
    RepeatedText,
    Template,
    SiteObligation,
}

/// <summary>Values of <c>FixGroupStatus</c> (stored as snake_case text with a CHECK constraint).</summary>
public enum FixGroupStatus
{
    Draft,
    NeedsValue,
    Approved,
    Published,
    PartiallyPublished,
    Rejected,
}

/// <summary>Values of <c>FixField</c> (stored as snake_case text with a CHECK constraint).</summary>
public enum FixField
{
    Description,
    ShortDescription,
    Name,
    Block,
}

/// <summary>Values of <c>RecheckStatus</c> (stored as snake_case text with a CHECK constraint).</summary>
public enum RecheckStatus
{
    Ok,
    StillFinding,
    Pending,
}

/// <summary>Values of <c>FixProposalStatus</c> (stored as snake_case text with a CHECK constraint).</summary>
public enum FixProposalStatus
{
    Proposed,
    Accepted,
    Rejected,
    Edited,
    Published,
    Conflict,
    Superseded,
}

/// <summary>Values of <c>PublicationStatus</c> (stored as snake_case text with a CHECK constraint).</summary>
public enum PublicationStatus
{
    Queued,
    Published,
    Failed,
    Conflict,
    RolledBack,
}

/// <summary>Values of <c>Decision</c> (stored as snake_case text with a CHECK constraint).</summary>
public enum Decision
{
    Replace,
    Keep,
    KeepWithEvidence,
    Remove,
}

/// <summary>Values of <c>EvidenceSubjectKind</c> (stored as snake_case text with a CHECK constraint).</summary>
public enum EvidenceSubjectKind
{
    Brand,
    Product,
    Group,
}

/// <summary>Values of <c>EvidenceKind</c> (stored as snake_case text with a CHECK constraint).</summary>
public enum EvidenceKind
{
    Certificate,
    License,
    TestReport,
    Statement,
    Answer,
}

/// <summary>Values of <c>EvidenceSource</c> (stored as snake_case text with a CHECK constraint).</summary>
public enum EvidenceSource
{
    Upload,
    Registry,
    Answer,
}

/// <summary>Values of <c>EvidenceStatus</c> (stored as snake_case text with a CHECK constraint).</summary>
public enum EvidenceStatus
{
    Valid,
    Expiring,
    Expired,
    AwaitingAnswer,
    ClaimRemoved,
}
