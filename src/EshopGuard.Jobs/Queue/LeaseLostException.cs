namespace EshopGuard.Jobs.Queue;

/// <summary>
/// The worker no longer owns the job (lease expired and another worker took it, or it was returned). Its transaction was
/// rolled back; nothing of the handler's results was written.
/// </summary>
public sealed class LeaseLostException(long jobId, int attempt)
    : Exception($"{JobErrorCodes.LeaseLost}: job {jobId}, attempt {attempt}")
{
    /// <summary>Code <c>job.lease_lost</c>.</summary>
    public string Code => JobErrorCodes.LeaseLost;

    /// <summary>The job.</summary>
    public long JobId { get; } = jobId;

    /// <summary>Attempt whose lease was lost.</summary>
    public int Attempt { get; } = attempt;
}
