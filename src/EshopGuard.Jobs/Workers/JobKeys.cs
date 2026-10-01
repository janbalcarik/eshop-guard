using System.Globalization;

namespace EshopGuard.Jobs.Workers;

/// <summary>Concurrency keys of jobs (at most one running job per key).</summary>
public static class JobKeys
{
    /// <summary>
    /// <c>domain:{domain}</c>, lowercase, without <c>www.</c> and a trailing dot: downloads of one foreign domain run one at a
    /// time across all tenants, and runs of two tenants on the same domain take turns batch by batch.
    /// </summary>
    public static string Domain(string domain)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        var normalized = domain.Trim().TrimEnd('.').ToLowerInvariant();
        if (normalized.StartsWith("www.", StringComparison.Ordinal))
        {
            normalized = normalized[4..];
        }

        return "domain:" + normalized;
    }

    /// <summary><c>run:{runId}:{step}</c>: one running job per step of a run.</summary>
    public static string Run(Guid runId, string step)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(step);
        return string.Create(CultureInfo.InvariantCulture, $"run:{runId:D}:{step}");
    }
}
