using EshopGuard.Core.Models;
using EshopGuard.Core.Options;

namespace EshopGuard.Core;

/// <summary>
/// Entry point of the library.
/// </summary>
public interface IEshopGuard
{
    /// <summary>
    /// Crawls the site and evaluates its pages.
    /// </summary>
    /// <param name="siteUrl">Absolute http or https URL of the site, usually its home page.</param>
    /// <param name="options">Options of this scan; unset values fall back to <see cref="EshopGuardOptions"/>.</param>
    /// <param name="progress">Optional progress callback.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="Rules.RuleValidationException">The rule sets are not valid; nothing was downloaded.</exception>
    Task<ScanResult> ScanSiteAsync(Uri siteUrl, ScanOptions options,
        IProgress<ScanProgress>? progress = null, CancellationToken ct = default);

    /// <summary>
    /// Evaluates texts without crawling, e.g. product descriptions from a database before publication.
    /// </summary>
    /// <param name="texts">Texts to evaluate.</param>
    /// <param name="options">Modules, country and question language.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="Rules.RuleValidationException">The rule sets are not valid.</exception>
    Task<AnalysisResult> AnalyzeTextsAsync(IReadOnlyList<TextInput> texts, AnalyzeOptions options,
        CancellationToken ct = default);
}
