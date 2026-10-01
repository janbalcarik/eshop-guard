namespace EshopGuard.Core.Models;

/// <summary>
/// State of an obligation for the whole site in one jurisdiction.
/// </summary>
public enum ObligationStatus
{
    /// <summary>The information or sign was found.</summary>
    Met,

    /// <summary>It was not found; there is a finding.</summary>
    Missing,

    /// <summary>It was not found and the obligation applies only from <see cref="SiteObligation.EffectiveFrom"/>.</summary>
    Upcoming,

    /// <summary>It could not be checked; <see cref="SiteObligation.Reason"/> says why. Never counts as met.</summary>
    NotChecked,
}

/// <summary>
/// An obligation for the whole site (a rule with scope <c>site_presence</c> or <c>site_signal</c>) in one jurisdiction.
/// </summary>
public sealed class SiteObligation
{
    /// <summary>Jurisdiction code.</summary>
    public required string Jurisdiction { get; init; }

    /// <summary>Rule id.</summary>
    public required string RuleId { get; init; }

    /// <summary>Module of the rule.</summary>
    public required string Module { get; init; }

    /// <summary>Name of the rule set whose texts describe the obligation.</summary>
    public required string RuleSet { get; init; }

    /// <summary>State of the obligation.</summary>
    public ObligationStatus Status { get; init; }

    /// <summary>
    /// Code of the reason for <see cref="ObligationStatus.NotChecked"/>: <c>evaluation_skipped</c>,
    /// <c>site_signals_not_evaluated</c>, <c>legal_texts_not_given</c>, <c>no_legal_pages</c>.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>Date from which the obligation applies in the jurisdiction, if the rule has one.</summary>
    public DateOnly? EffectiveFrom { get; init; }

    /// <summary>Pages where the information or sign was found (for a met obligation) or the closest paragraph is.</summary>
    public IReadOnlyList<string> Urls { get; init; } = [];
}

/// <summary>
/// Which modules ran for one chosen jurisdiction and which did not.
/// </summary>
public sealed class JurisdictionCoverage
{
    /// <summary>Jurisdiction code.</summary>
    public required string Jurisdiction { get; init; }

    /// <summary>Modules whose rules ran for the jurisdiction.</summary>
    public IReadOnlyList<string> Modules { get; init; } = [];

    /// <summary>Requested modules that did not run, with the reason.</summary>
    public IReadOnlyList<ModuleNotRun> NotRun { get; init; } = [];
}

/// <summary>
/// A module that did not run for a jurisdiction: <c>no_rules_for_jurisdiction</c> (the module has no rules for it) or
/// <c>rule_set_disabled</c> (its rules for the jurisdiction are switched off).
/// </summary>
public sealed record ModuleNotRun(string Module, string Reason);
