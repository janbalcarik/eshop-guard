namespace EshopGuard.Jobs.Queue;

/// <summary>
/// Result of a heartbeat. <paramref name="LeaseHeld"/> = false: another worker owns the job now (the handler is canceled).
/// <paramref name="CancelRequested"/>: the job's run was canceled (<c>checks.runs.cancel_requested</c>).
/// </summary>
public sealed record HeartbeatResult(bool LeaseHeld, bool CancelRequested);
