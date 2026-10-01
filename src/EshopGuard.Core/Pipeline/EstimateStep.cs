using EshopGuard.Core.Models;
using EshopGuard.Core.Options;
using EshopGuard.Core.Rules;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EshopGuard.Core.Pipeline;

/// <summary>
/// One estimate for the new profiles and Jev before anything is paid: it only reads the cache. With the sieve it counts the
/// sieve and all detailed questions, an upper bound (the sieve then leaves some out); new profiles only leave text out,
/// so the Jev part counted before them is an upper bound too.
/// </summary>
internal sealed class EstimateStep(
    SegmentEvaluator segmentEvaluator,
    PageSieve pageSieve,
    IOptions<EshopGuardOptions> options,
    ILogger<EstimateStep> logger)
{
    public async Task<RunEstimate> EstimateAsync(EstimateInput input, IReadOnlyList<RuleSet> ruleSets, SieveDefinition? sieve, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        var jev = await EstimateJevAsync(input, ruleSets, sieve, ct);
        var estimate = WithProfiles(jev, input.Profiles);
        logger.LogInformation("Estimate: {Calls} Jev calls, {Cost} USD; {Profiles} new profiles, {ProfileCost} USD, created: {WillCreate}",
            estimate.Calls, estimate.EstimatedCostUsd, estimate.ProfileTemplates, estimate.ProfileCostUsd, estimate.ProfilesWillRun);
        var asks = sieve is not null || estimate.Calls + estimate.CachedCalls > 0 || estimate.ProfilesWillRun;
        return new RunEstimate(estimate, asks);
    }

    private async Task<JevCallEstimate> EstimateJevAsync(EstimateInput input, IReadOnlyList<RuleSet> ruleSets, SieveDefinition? sieve, CancellationToken ct)
    {
        var detail = await segmentEvaluator.EstimateAsync(input.Segments.Segments, ruleSets, input.QuestionLanguage, ct);
        if (sieve is null)
        {
            logger.LogInformation("Jev estimate: {Calls} calls ({Cached} more from cache), about {Tokens} input tokens, about {Cost} USD, mock {Mock}",
                detail.Calls, detail.CachedCalls, detail.EstimatedInputTokens, detail.EstimatedCostUsd, detail.IsMock);
            return detail;
        }

        var (sieveEstimate, _) = await pageSieve.PrepareAsync(
            input.Segments.SieveChunks.Select(c => (c.Url, c.Chunk)).ToList(), sieve, input.SieveModules, input.QuestionLanguage, ct);
        var calls = sieveEstimate.Calls + detail.Calls;
        var tokens = sieveEstimate.EstimatedInputTokens + detail.EstimatedInputTokens;
        var estimate = new JevCallEstimate
        {
            Calls = calls,
            SieveCalls = sieveEstimate.Calls,
            CachedCalls = sieveEstimate.CachedCalls + detail.CachedCalls,
            EstimatedInputTokens = tokens,
            EstimatedCostUsd = Math.Round(segmentEvaluator.Cost(tokens), 6),
            IsMock = detail.IsMock,
            RequiresConfirmation = !detail.IsMock && calls > options.Value.Budget.MaxCallsWithoutConfirm,
            UpperBound = true,
        };
        logger.LogInformation("Jev estimate with sieve: at most {Calls} calls ({Sieve} of the sieve), about {Tokens} input tokens, at most {Cost} USD",
            estimate.Calls, estimate.SieveCalls, estimate.EstimatedInputTokens, estimate.EstimatedCostUsd);
        return estimate;
    }

    /// <summary>The estimate with the new profiles the scan would write; their price above the limit needs confirmation too.</summary>
    private JevCallEstimate WithProfiles(JevCallEstimate estimate, ProfilePlan profiles) => new()
    {
        Calls = estimate.Calls,
        SieveCalls = estimate.SieveCalls,
        CachedCalls = estimate.CachedCalls,
        EstimatedInputTokens = estimate.EstimatedInputTokens,
        EstimatedCostUsd = estimate.EstimatedCostUsd,
        IsMock = estimate.IsMock,
        UpperBound = estimate.UpperBound || profiles.WillCreate,
        ProfileTemplates = profiles.Planned.Count,
        ProfileCostUsd = Math.Round(profiles.EstimatedUsd, 4),
        ProfilesWillRun = profiles.WillCreate,
        ProfilesUnavailableReason = profiles.Planned.Count > 0 ? profiles.UnavailableReason : null,
        RequiresConfirmation = estimate.RequiresConfirmation
            || (profiles.WillCreate && profiles.EstimatedUsd > options.Value.Profiles.MaxUsdWithoutConfirm),
    };
}
