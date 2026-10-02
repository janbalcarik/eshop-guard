using EshopGuard.Application.Problems;
using EshopGuard.Application.Shops.Ownership;
using EshopGuard.Application.Shops.Scope;
using EshopGuard.Core.Markets;
using EshopGuard.Data;
using EshopGuard.Data.Entities.Checks;
using EshopGuard.Data.Entities.Shops;
using EshopGuard.Data.Tenancy;

namespace EshopGuard.Application.Shops.Onboarding;

/// <summary>Whether an e-shop can be ordered: the codes that block it and the scope from the stored state with its hash.</summary>
public sealed record ShopOrderReadinessResult(bool Ready, IReadOnlyList<string> Blocking, ShopScope? Scope, StoredScope Stored);

/// <summary>
/// The readiness of an e-shop for the order (change 10, task 8.2; called by change 12): the scope recomputed from the stored
/// state, so the order can refuse a quote with another <c>scopeHash</c> (<c>409 quote.stale</c>).
/// </summary>
public interface IShopOrderReadiness
{
    Task<ShopOrderReadinessResult> CheckAsync(Guid shopId, CancellationToken ct);
}

/// <inheritdoc />
public sealed class ShopOrderReadiness(EshopGuardDb db, ShopCatalog catalog, ScopeInputsLoader loader, ShopOwnershipPolicy ownership) : IShopOrderReadiness
{
    public async Task<ShopOrderReadinessResult> CheckAsync(Guid shopId, CancellationToken ct)
    {
        var markets = await catalog.MarketsAsync(ct).ConfigureAwait(false);
        return await db.ExecuteInTenantTransactionAsync(async () => Evaluate(await loader.LoadAsync(shopId, markets, ct).ConfigureAwait(false), markets), ct)
            .ConfigureAwait(false);
    }

    /// <summary>The blocking codes in the order of the onboarding (sample, markets, versions, ownership, state, scope).</summary>
    public ShopOrderReadinessResult Evaluate(StoredScope stored, MarketCatalog markets)
    {
        ArgumentNullException.ThrowIfNull(stored);
        var blocking = new List<string>();
        ShopScope? scope = null;
        switch (stored.Sample?.Status)
        {
            case null or RunStatus.Failed or RunStatus.Canceled:
                blocking.Add(ProblemCodes.ScopeBasisMissing);
                break;
            case RunStatus.Finished or RunStatus.Partial:
                break;
            default:
                blocking.Add(ProblemCodes.SampleNotFinished);
                break;
        }

        if (!stored.MarketsConfirmed)
        {
            blocking.Add(ProblemCodes.MarketsNotConfirmed);
        }
        else if (stored.ActiveMarkets.Count == 0)
        {
            blocking.Add(ProblemCodes.MarketsNoneSelected);
        }

        if (stored.Languages.Any(l => l.Status == ShopLanguageStatus.NeedsConfirmation))
        {
            blocking.Add(ProblemCodes.LanguagesConfirmationPending);
        }

        if (ownership.Requires(OwnershipPolicyOptions.FullAnalysis) && stored.Shop.OwnershipVerifiedAt is null)
        {
            blocking.Add(ProblemCodes.ShopOwnershipNotVerified);
        }

        if (!ShopStatusTransitions.IsOrderable(stored.Shop.Status))
        {
            blocking.Add(ProblemCodes.ShopStatusNotOrderable);
        }

        if (!blocking.Contains(ProblemCodes.ScopeBasisMissing) && !blocking.Contains(ProblemCodes.SampleNotFinished))
        {
            scope = ShopScopeCalculator.Calculate(stored.Input(), markets);
            blocking.AddRange(scope.Issues.Where(i => !blocking.Contains(i)));
        }

        return new ShopOrderReadinessResult(blocking.Count == 0, blocking, scope, stored);
    }
}
