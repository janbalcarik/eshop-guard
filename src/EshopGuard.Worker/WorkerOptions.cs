namespace EshopGuard.Worker;

/// <summary>Settings under <c>Worker</c>.</summary>
public sealed class WorkerOptions
{
    /// <summary>Configuration section.</summary>
    public const string SectionName = "Worker";

    /// <summary>Identity of the worker in logs and leases; default <c>{MachineName}:{ProcessId}</c>.</summary>
    public string? Id { get; set; }

    /// <summary>How long a shutdown may take before the host gives up (Compose stop_grace_period must be longer).</summary>
    public int ShutdownSeconds { get; set; } = 90;

    /// <summary>The configured id, or <c>{MachineName}:{ProcessId}</c>.</summary>
    public string EffectiveId => string.IsNullOrWhiteSpace(Id) ? $"{Environment.MachineName}:{Environment.ProcessId}" : Id;
}
