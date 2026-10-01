using EshopGuard.Core.Models;

namespace EshopGuard.Core.Options;

/// <summary>
/// Options of <see cref="IEshopGuard.AnalyzeTextsAsync"/>.
/// </summary>
public sealed class AnalyzeOptions
{
    /// <summary>Rule modules to run.</summary>
    public IReadOnlyList<string> Modules { get; init; } = [];

    /// <summary>Jurisdiction of legal rules (<c>cz</c> or <c>sk</c>).</summary>
    public string Country { get; init; } = "sk";

    /// <summary>Language of the questions sent to Jev (<c>en</c> or <c>cs</c>).</summary>
    public string QuestionLanguage { get; init; } = "en";

    /// <summary>Parallel requests to Jev.</summary>
    public int? Concurrency { get; init; }

    /// <summary>Called with the estimate before Jev is called; returning false skips the evaluation.</summary>
    public Func<JevCallEstimate, CancellationToken, Task<bool>>? ConfirmJevCalls { get; init; }
}
