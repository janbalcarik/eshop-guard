namespace EshopGuard.Jobs.Runs;

/// <summary>A temporary failure of a step after the clients' own retries: the queue repeats the batch later.</summary>
public sealed class TransientStepException(string code, TimeSpan? retryAfter = null, Exception? inner = null)
    : Exception($"{code}: temporary failure of a step", inner)
{
    public string Code { get; } = code;

    public TimeSpan? RetryAfter { get; } = retryAfter;
}
