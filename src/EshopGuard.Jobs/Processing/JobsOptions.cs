using Microsoft.Extensions.Options;

namespace EshopGuard.Jobs.Processing;

/// <summary>Settings under <c>Jobs</c> (shared by the API and the worker).</summary>
public sealed class JobsOptions
{
    /// <summary>Configuration section.</summary>
    public const string SectionName = "Jobs";

    /// <summary>Attempts of a job that does not set its own.</summary>
    public int DefaultMaxAttempts { get; set; } = 5;

    /// <summary>Backoff before a retry.</summary>
    public RetryOptions Retry { get; set; } = new();

    /// <summary>
    /// How long a worker caches the paused classes: a pause or resume by another worker takes effect within this time
    /// (plus at most a second of waiting in a paused class).
    /// </summary>
    public double PausedClassesCacheSeconds { get; set; } = 2;
}

/// <summary>Settings under <c>Jobs:Retry</c>: backoff = BaseSeconds × 2^(attempt − 1), at most MaxSeconds, ±20 %.</summary>
public sealed class RetryOptions
{
    public double BaseSeconds { get; set; } = 10;

    public double MaxSeconds { get; set; } = 900;
}

/// <summary>Validation of <see cref="JobsOptions"/> (<c>config.jobs_invalid</c>).</summary>
internal sealed class JobsOptionsValidator : IValidateOptions<JobsOptions>
{
    public ValidateOptionsResult Validate(string? name, JobsOptions o)
    {
        var failures = new List<string>();
        if (o.DefaultMaxAttempts < 1)
        {
            failures.Add("config.jobs_invalid: Jobs:DefaultMaxAttempts (at least 1)");
        }

        if (o.Retry.BaseSeconds <= 0 || o.Retry.MaxSeconds < o.Retry.BaseSeconds)
        {
            failures.Add("config.jobs_invalid: Jobs:Retry (BaseSeconds positive, MaxSeconds at least BaseSeconds)");
        }

        if (o.PausedClassesCacheSeconds < 0)
        {
            failures.Add("config.jobs_invalid: Jobs:PausedClassesCacheSeconds (must not be negative)");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
