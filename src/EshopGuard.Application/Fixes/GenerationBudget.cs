using EshopGuard.Application.Problems;
using EshopGuard.Application.RateLimits;
using Microsoft.Extensions.Options;

namespace EshopGuard.Application.Fixes;

/// <summary>
/// The daily budget of proposals generated after the answer „Nie“ (change 11, AD 6): the bucket
/// <c>fixes:generate:tenant:{id}</c> in <c>ops.rate_limit_buckets</c> holds <c>Fixes:DailyGenerationsPerTenant</c> and refills
/// over a day. Above it the answer is <c>429 budget.daily_limit_reached</c>; the caller takes the budget in the transaction of
/// the answer, so nothing of the answer is saved.
/// </summary>
public sealed class GenerationBudget(IRateLimitBuckets buckets, IOptions<FixesOptions> options)
{
    public const string Prefix = "fixes:generate:tenant:";

    public async Task TakeAsync(Guid tenantId, int count, CancellationToken ct)
    {
        if (count <= 0)
        {
            return;
        }

        var capacity = Math.Max(1, options.Value.DailyGenerationsPerTenant);
        var result = await buckets.TryTakeAsync(Prefix + tenantId.ToString("D"), count, capacity, capacity / TimeSpan.FromDays(1).TotalSeconds, ct).ConfigureAwait(false);
        if (!result.Granted)
        {
            throw new DomainException(ProblemCodes.BudgetDailyLimitReached, 429, new Dictionary<string, object?>
            {
                ["limit"] = capacity,
                ["requested"] = count,
                ["retryAfterSeconds"] = result.RetryAfter >= TimeSpan.FromDays(1) ? (int)TimeSpan.FromDays(1).TotalSeconds : (int)Math.Ceiling(Math.Max(1, result.RetryAfter.TotalSeconds)),
            });
        }
    }
}
