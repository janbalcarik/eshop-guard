using EshopGuard.Application.Contracts;
using EshopGuard.Application.Shops.Ownership;
using EshopGuard.Application.Shops.Scope;
using EshopGuard.Data;
using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Checks;
using EshopGuard.Data.Entities.Shops;
using EshopGuard.Data.Tenancy;

namespace EshopGuard.Application.Shops.Onboarding;

/// <summary>
/// The step of the onboarding of an e-shop (change 10, task 8.1): <c>connect</c> before the sample, <c>sample</c> while it
/// runs, <c>scope</c> after it until the order, <c>payment</c>, <c>analysis</c> and <c>done</c> by the state of the e-shop;
/// with the codes that block the order (<see cref="ShopOrderReadiness"/>).
/// </summary>
public sealed class OnboardingStateService(EshopGuardDb db, ShopCatalog catalog, ScopeInputsLoader loader, ShopOrderReadiness readiness, ShopOwnershipPolicy ownership)
{
    public async Task<OnboardingStateDto> GetAsync(Guid shopId, CancellationToken ct)
    {
        var markets = await catalog.MarketsAsync(ct).ConfigureAwait(false);
        return await db.ExecuteInTenantTransactionAsync(async () =>
        {
            var stored = await loader.LoadAsync(shopId, markets, ct).ConfigureAwait(false);
            var ready = readiness.Evaluate(stored, markets);
            var step = stored.Shop.Status switch
            {
                ShopStatus.AwaitingPayment => "payment",
                ShopStatus.Analyzing => "analysis",
                ShopStatus.Active or ShopStatus.Paused => "done",
                _ when stored.Sample is null => "connect",
                _ when stored.Sample.Status is RunStatus.Finished or RunStatus.Partial or RunStatus.Failed or RunStatus.Canceled => "scope",
                _ => "sample",
            };
            return new OnboardingStateDto(
                step,
                ready.Blocking,
                new OnboardingSampleDto(stored.Sample is null ? null : SnakeCaseEnumConverter<RunStatus>.ToText(stored.Sample.Status), stored.Sample?.Id),
                stored.MarketsConfirmed,
                stored.Languages.Where(l => l.Status == ShopLanguageStatus.NeedsConfirmation).Select(l => l.Language).ToList(),
                new OnboardingOwnershipDto(
                    ownership.Requires(OwnershipPolicyOptions.Sample) || ownership.Requires(OwnershipPolicyOptions.FullAnalysis),
                    stored.Shop.OwnershipVerifiedAt is not null),
                ready.Scope?.ScopeHash);
        }, ct).ConfigureAwait(false);
    }
}
