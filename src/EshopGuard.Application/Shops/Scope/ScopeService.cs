using EshopGuard.Application.Contracts;
using EshopGuard.Application.Problems;
using EshopGuard.Application.Shops.Pricing;
using EshopGuard.Data;
using EshopGuard.Data.Tenancy;
using Microsoft.Extensions.DependencyInjection;

namespace EshopGuard.Application.Shops.Scope;

/// <summary>
/// The scope of an e-shop (<c>GET …/scope</c>, the stored state) and the quote (<c>POST …/quote</c>, change 10, AD 8): the
/// quote computes the scope for the ticked markets and the excluded versions of the request without storing anything and
/// asks <see cref="IPriceQuoteService"/> (change 12) for the price; without it <c>503 billing.unavailable</c> (fail-closed).
/// The basis is always a finished sample (<c>409 quote.sample_not_finished</c>, <c>quote.basis_missing</c>).
/// </summary>
public sealed class ScopeService(EshopGuardDb db, ShopCatalog catalog, ScopeInputsLoader loader, IServiceProvider services)
{
    public async Task<ScopeDto> GetAsync(Guid shopId, CancellationToken ct)
    {
        var markets = await catalog.MarketsAsync(ct).ConfigureAwait(false);
        return await db.ExecuteInTenantTransactionAsync(async () =>
        {
            var stored = await loader.LoadAsync(shopId, markets, ct).ConfigureAwait(false);
            ScopeInputsLoader.RequireBasis(stored, ProblemCodes.ScopeBasisMissing);
            return ToDto(ShopScopeCalculator.Calculate(stored.Input(), markets), stored);
        }, ct).ConfigureAwait(false);
    }

    public async Task<QuoteDto> QuoteAsync(Guid userId, Guid shopId, IReadOnlyList<string>? activeMarkets, IReadOnlyList<string>? excludedLanguages, CancellationToken ct)
    {
        var markets = await catalog.MarketsAsync(ct).ConfigureAwait(false);
        var active = activeMarkets is null ? null : MarketService.Validate(activeMarkets, markets).ToList();
        var (scope, stored) = await db.ExecuteInTenantTransactionAsync(async () =>
        {
            var stored = await loader.LoadAsync(shopId, markets, ct).ConfigureAwait(false);
            ScopeInputsLoader.RequireBasis(stored, ProblemCodes.QuoteBasisMissing);
            var excluded = excludedLanguages?.Select(l => l.Trim().ToLowerInvariant()).ToList();
            return (ShopScopeCalculator.Calculate(stored.Input(active, excluded), markets), stored);
        }, ct).ConfigureAwait(false);

        if (scope.Issues.Contains(ProblemCodes.MarketsNoneSelected))
        {
            throw new DomainException(ProblemCodes.MarketsNoneSelected, 400);
        }

        foreach (var code in new[] { ProblemCodes.ScopeNoCheckableVersion, ProblemCodes.ScopeProductCountUnknown })
        {
            if (scope.Issues.Contains(code))
            {
                throw new DomainException(code, 409);
            }
        }

        var pricing = services.GetService<IPriceQuoteService>()
            ?? throw new DomainException(ProblemCodes.BillingUnavailable, 503);
        var price = await pricing.QuoteAsync(new PriceQuoteRequest(db.TenantContext.RequireTenantId(), shopId, scope, userId), ct).ConfigureAwait(false);
        return new QuoteDto(ToDto(scope, stored), price);
    }

    public static ScopeDto ToDto(ShopScope scope, StoredScope stored)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(stored);
        return new ScopeDto(
            scope.ByMarket.Select(m => new ScopeMarketDto(m.MarketCode, m.Language, m.ProductCount, m.PageCount)).ToList(),
            scope.CheckedVersions.Select(v => new ScopeCheckedVersionDto(v.Language, v.BaseUrl, v.IsMain, v.ProductCount, v.OtherPageCount, v.Jurisdictions, v.Markets, v.Reason))
                .ToList(),
            scope.NotCheckedVersions.Select(v => new ScopeNotCheckedVersionDto(v.Language, v.BaseUrl, v.Reason)).ToList(),
            scope.Jurisdictions,
            scope.ProductTotal,
            scope.OtherPagesTotal,
            new ScopeBasisDto(scope.SampleRunId, scope.SampleRunId is null ? null : stored.Sample?.FinishedAt),
            scope.Issues,
            scope.ScopeHash,
            scope.PriceUnit is { } unit && scope.PriceCount is { } count ? new ScopePriceBasisDto(unit, count) : null);
    }
}
