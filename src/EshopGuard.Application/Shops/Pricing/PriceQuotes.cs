using EshopGuard.Application.Contracts;
using EshopGuard.Application.Shops.Scope;

namespace EshopGuard.Application.Shops.Pricing;

/// <summary>What change 12 prices: the tenant, the e-shop, the scope (with its <c>scopeHash</c>) and who asked.</summary>
public sealed record PriceQuoteRequest(Guid TenantId, Guid ShopId, ShopScope Scope, Guid RequestedBy);

/// <summary>
/// The price of a scope (change 12: the band, amounts, discounts and VAT, stored in <c>billing.price_quotes</c> with the
/// <c>scopeHash</c>). Change 10 registers no implementation: <c>POST …/quote</c> then answers <c>503 billing.unavailable</c>.
/// </summary>
public interface IPriceQuoteService
{
    Task<PriceQuoteDto> QuoteAsync(PriceQuoteRequest request, CancellationToken ct);
}
