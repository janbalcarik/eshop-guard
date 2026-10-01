using EshopGuard.Core.Models;
using EshopGuard.Core.Profiles;

namespace EshopGuard.Core.Pipeline;

/// <summary>A page as the profile planning sees it: its structure and the profile it already uses.</summary>
internal sealed record ProfileCandidate(string Url, PageType Type, bool Eligible, string? ProfileId, HashSet<string> Tokens);

/// <summary>Input of <see cref="ProfileStep.PlanAsync"/>: the pages of the scan in their order.</summary>
internal sealed record ProfilePlanInput(string SiteKey, IReadOnlyList<ProfileCandidate> Candidates) : IPipelineRecord
{
    public int SchemaVersion { get; init; } = PipelineSchema.Version;

    /// <summary>Ids of the stored profiles of the site.</summary>
    public IReadOnlyList<string> StoredProfileIds { get; init; } = [];
}

/// <summary>A new profile the scan would write: the first page of its group, the size of the group and the estimated price.</summary>
internal sealed record PlannedProfile(string FirstUrl, int Pages, decimal EstimatedUsd)
{
    /// <summary>Pages whose outlines priced the profile.</summary>
    public IReadOnlyList<string> SampleUrls { get; init; } = [];
}

/// <summary>Output of <see cref="ProfileStep.PlanAsync"/>; no model was called.</summary>
internal sealed record ProfilePlan(string SiteKey, bool Disabled, IReadOnlyList<PlannedProfile> Planned, string? UnavailableReason) : IPipelineRecord
{
    public int SchemaVersion { get; init; } = PipelineSchema.Version;

    /// <summary>The planned profiles will be written after confirmation (the model is available).</summary>
    public bool WillCreate => Planned.Count > 0 && UnavailableReason is null;

    public decimal EstimatedUsd => Planned.Sum(p => p.EstimatedUsd);

    /// <summary>Profiling is off.</summary>
    public static ProfilePlan Off(string siteKey) => new(siteKey, true, [], null);
}

/// <summary>Output of <see cref="ProfileStep.CreateAsync"/>: new profiles, what they cost and what went wrong.</summary>
internal sealed record ProfileCreateResult(
    IReadOnlyList<PageProfile> Created,
    int Calls,
    long InputTokens,
    long OutputTokens,
    decimal CostUsd,
    IReadOnlyList<ScanWarning> Warnings) : IPipelineRecord
{
    public int SchemaVersion { get; init; } = PipelineSchema.Version;

    public static ProfileCreateResult None { get; } = new([], 0, 0, 0, 0, []);
}
