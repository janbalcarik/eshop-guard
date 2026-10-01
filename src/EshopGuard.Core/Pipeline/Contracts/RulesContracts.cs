using EshopGuard.Core.Models;
using EshopGuard.Core.Rules;

namespace EshopGuard.Core.Pipeline;

/// <summary>
/// Input of <see cref="RulesStep"/>: the evaluated segments and what the site-level rules read from the pages. The rule
/// sets, labels and legal requirements come from the rule catalog of the run.
/// </summary>
/// <param name="Country">Jurisdiction of the rules.</param>
/// <param name="Segments">Unique segments with their probabilities.</param>
/// <param name="PageTexts">Everything a customer sees on each analyzed page, for page-level allowlists.</param>
/// <param name="PageSignals">Text, links and images of every page, for <c>site_signal</c> rules.</param>
/// <param name="PageCategories">Category text of each analyzed page.</param>
/// <param name="UncheckedDocuments">Legal documents that were found but not read.</param>
/// <param name="TextNotLoadedPages">Pages whose text was not in the HTML.</param>
/// <param name="AddMissingLegalPagesFinding">No legal page was found and the legal module runs.</param>
internal sealed record RulesInput(
    string Country,
    IReadOnlyList<Segment> Segments,
    IReadOnlyDictionary<string, string> PageTexts,
    IReadOnlyDictionary<string, PageSignals> PageSignals,
    IReadOnlyDictionary<string, string> PageCategories,
    IReadOnlyList<UncheckedDocument> UncheckedDocuments,
    IReadOnlyList<PageInfo> TextNotLoadedPages,
    bool AddMissingLegalPagesFinding) : IPipelineRecord
{
    public int SchemaVersion { get; init; } = PipelineSchema.Version;

    /// <summary>Rules of the kind <c>site_signal</c> look at <see cref="PageSignals"/> (a scan, not texts given directly).</summary>
    public bool EvaluateSiteSignals { get; init; } = true;

    /// <summary>Rules that look for information anywhere on the site run (a scan, or texts with a legal one).</summary>
    public bool EvaluateSitePresence { get; init; } = true;
}
