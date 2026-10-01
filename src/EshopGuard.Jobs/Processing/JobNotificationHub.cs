using EshopGuard.Data.Entities.Ops;
using EshopGuard.Jobs.Queue;

namespace EshopGuard.Jobs.Processing;

/// <summary>Wakes the loop of a resource class: a notification of a new job, or a freed slot.</summary>
public sealed class JobNotificationHub
{
    private readonly Dictionary<JobResourceClass, AsyncSignal> _signals = JobNames.ResourceClasses.ToDictionary(c => c, _ => new AsyncSignal());

    /// <summary>Signal of the class.</summary>
    internal AsyncSignal Signal(JobResourceClass resourceClass) => _signals[resourceClass];

    /// <summary>Payload of <c>eshopguard_jobs</c> (the class); unknown payloads are ignored.</summary>
    public void Publish(string? payload)
    {
        if (JobNames.ParseResourceClass(payload) is { } resourceClass)
        {
            _signals[resourceClass].Set();
        }
    }

    /// <summary>Wakes every class (after a reconnect, notifications may have been missed).</summary>
    public void PublishAll()
    {
        foreach (var signal in _signals.Values)
        {
            signal.Set();
        }
    }
}

/// <summary>Auto-reset signal: <see cref="Set"/> before or during a wait releases exactly the next wait.</summary>
internal sealed class AsyncSignal
{
    private TaskCompletionSource _source = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Set() => Volatile.Read(ref _source).TrySetResult();

    /// <summary>Waits for the signal at most <paramref name="timeout"/>; <c>true</c> when it was set.</summary>
    public async Task<bool> WaitAsync(TimeSpan timeout, CancellationToken ct)
    {
        var source = Volatile.Read(ref _source);
        try
        {
            await source.Task.WaitAsync(timeout, ct).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            return false;
        }

        Interlocked.CompareExchange(ref _source, new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously), source);
        return true;
    }
}
