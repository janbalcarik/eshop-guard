using EshopGuard.Core.Fix;
using EshopGuard.Core.Models;

namespace EshopGuard.Core.Pipeline;

/// <summary>
/// Rewrites the pages with findings in batches of at most <see cref="BatchPages"/> pages (<see cref="ITextRewriter"/>): the
/// pages are chosen and ordered as in one run, each batch carries its own pages and their findings, and the results of the
/// batches together are the result of one run.
/// </summary>
internal sealed class RewriteStep(ITextRewriter rewriter)
{
    /// <summary>Pages of one batch of the worker.</summary>
    public const int BatchPages = 25;

    /// <summary>The batches of a rewrite: the pages with a finding to rewrite, by URL, at most <see cref="RewriteInput.MaxPages"/>.</summary>
    public static List<RewriteInput> Batches(RewriteInput input, int batchPages = BatchPages)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchPages);
        var pages = input.Pages.GroupBy(p => p.Url, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var byPage = new Dictionary<string, List<Finding>>(StringComparer.Ordinal);
        foreach (var finding in input.Findings.Where(PageRewriter.IsRewritable))
        {
            if (finding.Urls.FirstOrDefault(pages.ContainsKey) is { } url)
            {
                (byPage.TryGetValue(url, out var list) ? list : byPage[url] = []).Add(finding);
            }
        }

        var urls = byPage.Keys.Order(StringComparer.Ordinal).Take(input.MaxPages ?? int.MaxValue).ToList();
        return urls.Chunk(batchPages).Select(chunk => new RewriteInput
        {
            Pages = chunk.Select(u => pages[u]).ToList(),
            Findings = chunk.SelectMany(u => byPage[u]).ToList(),
            Country = input.Country,
        }).ToList();
    }

    public Task<RewriteResult> RewriteBatchAsync(RewriteInput batch, IProgress<RewriteProgress>? progress, CancellationToken ct) =>
        rewriter.RewriteAsync(batch, progress, ct);

    /// <summary>The results of the batches as one result, pages in batch order.</summary>
    public static RewriteResult Merge(IReadOnlyList<RewriteResult> batches)
    {
        if (batches.Count == 0)
        {
            return new RewriteResult { Pages = [], Stats = new RewriteStats() };
        }

        var stats = batches.Select(b => b.Stats).ToList();
        return new RewriteResult
        {
            Pages = batches.SelectMany(b => b.Pages).ToList(),
            PromptVersion = batches[0].PromptVersion,
            Model = batches[0].Model,
            Warnings = batches.SelectMany(b => b.Warnings).Distinct().ToList(),
            Stats = new RewriteStats
            {
                Pages = stats.Sum(s => s.Pages),
                FromCache = stats.Sum(s => s.FromCache),
                Errors = stats.Sum(s => s.Errors),
                InputTokens = stats.Sum(s => s.InputTokens),
                CachedTokens = stats.Sum(s => s.CachedTokens),
                OutputTokens = stats.Sum(s => s.OutputTokens),
                ReasoningTokens = stats.Sum(s => s.ReasoningTokens),
                CostUsd = stats.Sum(s => s.CostUsd),
                CheckCalls = stats.Sum(s => s.CheckCalls),
                CheckCostUsd = stats.Sum(s => s.CheckCostUsd),
                Duration = TimeSpan.FromTicks(stats.Sum(s => s.Duration.Ticks)),
            },
        };
    }
}
