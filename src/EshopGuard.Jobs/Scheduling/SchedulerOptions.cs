using Microsoft.Extensions.Options;

namespace EshopGuard.Jobs.Scheduling;

/// <summary>Settings under <c>Scheduler</c>.</summary>
public sealed class SchedulerOptions
{
    /// <summary>Configuration section.</summary>
    public const string SectionName = "Scheduler";

    /// <summary>Whether this worker takes part in scheduling (any number may; one tick runs at a time).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Interval of the ticks.</summary>
    public double TickSeconds { get; set; } = 15;
}

/// <summary>Validation of <see cref="SchedulerOptions"/> (<c>config.scheduler_invalid</c>).</summary>
internal sealed class SchedulerOptionsValidator : IValidateOptions<SchedulerOptions>
{
    public ValidateOptionsResult Validate(string? name, SchedulerOptions options) => options.TickSeconds > 0
        ? ValidateOptionsResult.Success
        : ValidateOptionsResult.Fail("config.scheduler_invalid: Scheduler:TickSeconds (must be positive)");
}
