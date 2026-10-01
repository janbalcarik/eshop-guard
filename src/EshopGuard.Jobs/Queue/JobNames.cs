using EshopGuard.Data.Entities.Ops;

namespace EshopGuard.Jobs.Queue;

/// <summary>Text values of <see cref="JobResourceClass"/> and <see cref="JobState"/> as stored in <c>ops.jobs</c>.</summary>
public static class JobNames
{
    /// <summary>Notification channel of new jobs; the payload is the resource class.</summary>
    public const string NotificationChannel = "eshopguard_jobs";

    /// <summary>All resource classes in a fixed order.</summary>
    public static IReadOnlyList<JobResourceClass> ResourceClasses { get; } = Enum.GetValues<JobResourceClass>();

    /// <summary><c>fetch</c>, <c>cpu</c>, <c>jev</c>, <c>llm</c>, <c>io</c>, <c>system</c>.</summary>
    public static string ToDb(this JobResourceClass resourceClass) => resourceClass switch
    {
        JobResourceClass.Fetch => "fetch",
        JobResourceClass.Cpu => "cpu",
        JobResourceClass.Jev => "jev",
        JobResourceClass.Llm => "llm",
        JobResourceClass.Io => "io",
        JobResourceClass.System => "system",
        _ => throw new ArgumentOutOfRangeException(nameof(resourceClass)),
    };

    /// <summary>Resource class from its text; <c>null</c> for an unknown text.</summary>
    public static JobResourceClass? ParseResourceClass(string? value) =>
        ResourceClasses.Cast<JobResourceClass?>().FirstOrDefault(c => string.Equals(c!.Value.ToDb(), value, StringComparison.Ordinal));

    /// <summary>State from its text (<c>queued</c> … <c>canceled</c>).</summary>
    public static JobState ParseState(string value) => value switch
    {
        "queued" => JobState.Queued,
        "running" => JobState.Running,
        "succeeded" => JobState.Succeeded,
        "failed" => JobState.Failed,
        "canceled" => JobState.Canceled,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown job state."),
    };
}
