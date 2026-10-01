using EshopGuard.Data.Entities.Ops;
using Microsoft.Extensions.Options;

namespace EshopGuard.Jobs.Processing;

/// <summary>Settings under <c>Worker</c>.</summary>
public sealed class WorkerOptions
{
    /// <summary>Configuration section.</summary>
    public const string SectionName = "Worker";

    /// <summary>Identity of the worker in logs, leases and <c>ops.workers</c>; default <c>{MachineName}:{ProcessId}</c>.</summary>
    public string? Id { get; set; }

    /// <summary>
    /// How long a shutdown may take (the host's shutdown timeout; Compose <c>stop_grace_period</c> must be longer). Running
    /// jobs get it minus a margin of at most 5 s, then they go back to the queue. Must be below <see cref="LeaseSeconds"/>.
    /// </summary>
    public int ShutdownSeconds { get; set; } = 90;

    /// <summary>
    /// Slots per resource class: how many jobs of the class this worker runs at once. 0 = the worker does not take the class.
    /// <c>cpu</c> missing = <see cref="Environment.ProcessorCount"/>.
    /// </summary>
    public ResourceClassValues Slots { get; set; } = new() { Fetch = 100, Cpu = Environment.ProcessorCount, Jev = 8, Llm = 4, Io = 4, System = 2 };

    /// <summary>Soft cap of running jobs of one tenant per class across all workers; 0 = no cap.</summary>
    public ResourceClassValues TenantCaps { get; set; } = new() { Fetch = 20, Cpu = 4, Jev = 4, Llm = 2, Io = 4, System = 0 };

    /// <summary>Lease of a claimed job; a worker that stops sending heartbeats loses the job after it.</summary>
    public double LeaseSeconds { get; set; } = 120;

    /// <summary>Heartbeat of a running job; must be below a third of <see cref="LeaseSeconds"/>.</summary>
    public double HeartbeatSeconds { get; set; } = 30;

    /// <summary>Polling interval right after jobs were taken.</summary>
    public int MinPollMilliseconds { get; set; } = 500;

    /// <summary>Longest polling interval with an empty queue (notifications wake the worker earlier).</summary>
    public double MaxPollSeconds { get; set; } = 5;

    /// <summary>Heartbeat of the worker's row in <c>ops.workers</c>.</summary>
    public double RegistryHeartbeatSeconds { get; set; } = 30;

    /// <summary>The configured id, or <c>{MachineName}:{ProcessId}</c>.</summary>
    public string EffectiveId => string.IsNullOrWhiteSpace(Id) ? $"{Environment.MachineName}:{Environment.ProcessId}" : Id;

    /// <summary><see cref="LeaseSeconds"/> as a time span.</summary>
    public TimeSpan Lease => TimeSpan.FromSeconds(LeaseSeconds);

    /// <summary>Time running jobs get to finish at shutdown: <see cref="ShutdownSeconds"/> minus min(5 s, a fifth of it).</summary>
    public TimeSpan DrainTimeout => TimeSpan.FromSeconds(ShutdownSeconds - Math.Min(5, ShutdownSeconds / 5.0));
}

/// <summary>One integer per resource class (slots, tenant caps).</summary>
public sealed class ResourceClassValues
{
    public int Fetch { get; set; }

    public int Cpu { get; set; }

    public int Jev { get; set; }

    public int Llm { get; set; }

    public int Io { get; set; }

    public int System { get; set; }

    /// <summary>Value of the class.</summary>
    public int this[JobResourceClass resourceClass] => resourceClass switch
    {
        JobResourceClass.Fetch => Fetch,
        JobResourceClass.Cpu => Cpu,
        JobResourceClass.Jev => Jev,
        JobResourceClass.Llm => Llm,
        JobResourceClass.Io => Io,
        JobResourceClass.System => System,
        _ => throw new ArgumentOutOfRangeException(nameof(resourceClass)),
    };
}

/// <summary>Validation of <see cref="WorkerOptions"/> at startup (<c>config.worker_invalid</c> with the key, never a value).</summary>
internal sealed class WorkerOptionsValidator : IValidateOptions<WorkerOptions>
{
    public ValidateOptionsResult Validate(string? name, WorkerOptions options)
    {
        var failures = Check(options).Select(f => $"{JobErrorCodes.WorkerConfigInvalid}: {f}").ToList();
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    internal static IEnumerable<string> Check(WorkerOptions o)
    {
        if (o.LeaseSeconds <= 0)
        {
            yield return "Worker:LeaseSeconds (must be positive)";
        }

        if (o.HeartbeatSeconds <= 0 || o.HeartbeatSeconds >= o.LeaseSeconds / 3)
        {
            yield return "Worker:HeartbeatSeconds (must be positive and below a third of Worker:LeaseSeconds)";
        }

        if (o.ShutdownSeconds <= 0 || o.ShutdownSeconds >= o.LeaseSeconds)
        {
            yield return "Worker:ShutdownSeconds (must be positive and below Worker:LeaseSeconds)";
        }

        if (o.MinPollMilliseconds <= 0 || o.MaxPollSeconds * 1000 < o.MinPollMilliseconds)
        {
            yield return "Worker:MinPollMilliseconds, Worker:MaxPollSeconds (positive, max at least min)";
        }

        if (o.RegistryHeartbeatSeconds <= 0)
        {
            yield return "Worker:RegistryHeartbeatSeconds (must be positive)";
        }

        foreach (var resourceClass in Queue.JobNames.ResourceClasses)
        {
            if (o.Slots[resourceClass] < 0)
            {
                yield return $"Worker:Slots:{resourceClass} (must not be negative)";
            }

            if (o.TenantCaps[resourceClass] < 0)
            {
                yield return $"Worker:TenantCaps:{resourceClass} (must not be negative)";
            }
        }
    }
}
