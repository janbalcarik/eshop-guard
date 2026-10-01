using EshopGuard.Core.Models;

namespace EshopGuard.Core.Options;

/// <summary>
/// Options of <see cref="IEshopGuard.AnalyzeTextsAsync"/>.
/// </summary>
public sealed class AnalyzeOptions
{
    /// <summary>Rule modules to run.</summary>
    public IReadOnlyList<string> Modules { get; init; } = [];

    /// <summary>The jurisdiction when the run is for one country (a code from <c>config/jurisdictions.yaml</c>, e.g. <c>sk</c>).</summary>
    public string Country { get; init; } = "sk";

    /// <summary>Jurisdictions to evaluate the rules for; empty means only <see cref="Country"/>.</summary>
    public IReadOnlyList<string> Jurisdictions { get; init; } = [];

    /// <summary>Date of the evaluation for effective dates of rules; null means today.</summary>
    public DateOnly? AsOf { get; init; }

    /// <summary>The jurisdictions of the run: <see cref="Jurisdictions"/>, or <see cref="Country"/> alone.</summary>
    public IReadOnlyList<string> ResolvedJurisdictions => Jurisdictions.Count > 0 ? Jurisdictions.Distinct().ToList() : [Country];

    /// <summary>Language of the questions sent to Jev (<c>en</c> or <c>cs</c>).</summary>
    public string QuestionLanguage { get; init; } = "en";

    /// <summary>Parallel requests to Jev.</summary>
    public int? Concurrency { get; init; }

    /// <summary>Called with the estimate before Jev is called; returning false skips the evaluation.</summary>
    public Func<JevCallEstimate, CancellationToken, Task<bool>>? ConfirmJevCalls { get; init; }
}
