namespace EshopGuard.Data.Entities.Ops;

/// <summary>Values of <c>JobResourceClass</c> (stored as snake_case text with a CHECK constraint).</summary>
public enum JobResourceClass
{
    Fetch,
    Cpu,
    Jev,
    Llm,
    Io,
    System,
}

/// <summary>Values of <c>JobState</c> (stored as snake_case text with a CHECK constraint).</summary>
public enum JobState
{
    Queued,
    Running,
    Succeeded,
    Failed,
    Canceled,
}

/// <summary>Values of <c>ScheduleKind</c> (stored as snake_case text with a CHECK constraint).</summary>
public enum ScheduleKind
{
    Nightly,
    WeeklyWeb,
    Reconcile,
}

/// <summary>Values of <c>OutboxKind</c> (stored as snake_case text with a CHECK constraint).</summary>
public enum OutboxKind
{
    Email,
    Invoice,
    Einvoice,
}

/// <summary>Values of <c>AuditActorKind</c> (stored as snake_case text with a CHECK constraint).</summary>
public enum AuditActorKind
{
    User,
    System,
    Admin,
}
