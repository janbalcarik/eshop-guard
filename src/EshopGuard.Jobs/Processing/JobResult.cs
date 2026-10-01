namespace EshopGuard.Jobs.Processing;

/// <summary>Outcome of a handler, mapped by the worker to the queue.</summary>
public abstract record JobResult
{
    private JobResult()
    {
    }

    /// <summary>Done; the worker completes the job unless the handler already called <c>CompleteAsync</c>.</summary>
    public sealed record Succeeded : JobResult;

    /// <summary>Temporary error (429, 5xx, timeout): retry with backoff, or after <paramref name="After"/>.</summary>
    public sealed record Retry(string Code, TimeSpan? After = null) : JobResult;

    /// <summary>Permanent error: the job fails without further attempts.</summary>
    public sealed record Fail(string Code) : JobResult;

    /// <summary>Back to the queue after <paramref name="Delay"/> without counting the attempt (e.g. the domain is busy).</summary>
    public sealed record Defer(TimeSpan Delay, string Code) : JobResult;

    /// <summary>The external service refuses all work (credit exhausted, key rejected): pause the whole class, return the job.</summary>
    public sealed record PauseClass(string Code) : JobResult;

    /// <summary>The run was canceled; the job ends <c>canceled</c>.</summary>
    public sealed record Canceled : JobResult;

    /// <summary>Shared <see cref="Succeeded"/> instance.</summary>
    public static JobResult Done { get; } = new Succeeded();
}
