using EshopGuard.Data.Entities.Fixes;

namespace EshopGuard.Jobs.Evidence;

/// <summary>Settings <c>Evidence</c> (change 11, AD 10; proposals, not measured).</summary>
public sealed class EvidenceOptions
{
    public const string SectionName = "Evidence";

    /// <summary>Largest file of a piece of evidence (20 MB).</summary>
    public long MaxFileBytes { get; set; } = 20 * 1024 * 1024;

    /// <summary>A piece of evidence whose validity ends within this many days is <c>expiring</c>.</summary>
    public int ExpiringDays { get; set; } = 30;

    /// <summary>The day of the merchant („today“) is in this time zone (<c>Localization:TimeZone</c>).</summary>
    public string TimeZone { get; set; } = "Europe/Bratislava";
}

/// <summary>
/// The state of a piece of evidence (AD 10): <c>awaiting_answer</c> and <c>claim_removed</c> come from questions and stay;
/// otherwise <c>expired</c> after the last day of validity, <c>expiring</c> from <c>Evidence:ExpiringDays</c> days before it
/// (the last day included), else <c>valid</c>. Validity is a date: stored as midnight UTC, compared with the merchant's today.
/// </summary>
public static class EvidenceStatusCalculator
{
    public static (EvidenceStatus Status, int? DaysToExpiry) Calculate(EvidenceStatus stored, DateOnly? validUntil, DateOnly today, int expiringDays)
    {
        if (stored is EvidenceStatus.AwaitingAnswer or EvidenceStatus.ClaimRemoved)
        {
            return (stored, null);
        }

        if (validUntil is not { } until)
        {
            return (EvidenceStatus.Valid, null);
        }

        var days = until.DayNumber - today.DayNumber;
        return days < 0 ? (EvidenceStatus.Expired, days)
            : days <= expiringDays ? (EvidenceStatus.Expiring, days)
            : (EvidenceStatus.Valid, days);
    }

    /// <summary>The date of a stored validity.</summary>
    public static DateOnly? Date(DateTimeOffset? value) => value is { } v ? DateOnly.FromDateTime(v.UtcDateTime) : null;

    /// <summary>A date of validity as stored (midnight UTC).</summary>
    public static DateTimeOffset? Stored(DateOnly? date) => date is { } d ? new DateTimeOffset(d.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero) : null;

    /// <summary>The merchant's today in <paramref name="timeZone"/>.</summary>
    public static DateOnly Today(DateTimeOffset now, string timeZone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(timeZone)).DateTime);
}
