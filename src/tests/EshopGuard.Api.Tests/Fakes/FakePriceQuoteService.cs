using EshopGuard.Application.Contracts;
using EshopGuard.Application.Shops.Pricing;

namespace EshopGuard.Api.Tests.Fakes;

/// <summary>
/// Prices of the tests only (change 10, task 7.6; change 12 brings the real ones): the bands up to 500, 2 000, 5 000 and
/// 20 000 products, above that <c>isCustom</c>. No amounts.
/// </summary>
internal sealed class FakePriceQuoteService : IPriceQuoteService
{
    private static readonly int[] Bands = [500, 2000, 5000, 20000];

    public List<PriceQuoteRequest> Requests { get; } = [];

    public Task<PriceQuoteDto> QuoteAsync(PriceQuoteRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        Requests.Add(request);
        var total = request.Scope.ProductTotal ?? 0;
        int? band = Bands.Cast<int?>().FirstOrDefault(b => total <= b);
        return Task.FromResult(new PriceQuoteDto(
            Guid.CreateVersion7(), null, "EUR", band is { } b ? $"up_to_{b}" : null, band, band is null, new FairUseDto(null, false),
            null, null, null, null, null, null, DateTimeOffset.UtcNow.AddDays(1)));
    }
}
