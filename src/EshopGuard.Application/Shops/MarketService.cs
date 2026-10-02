using System.Text.Json.Nodes;
using EshopGuard.Application.Audit;
using EshopGuard.Application.Contracts;
using EshopGuard.Application.Localization;
using EshopGuard.Application.Problems;
using EshopGuard.Core.Markets;
using EshopGuard.Data;
using EshopGuard.Data.Entities.Checks;
using EshopGuard.Data.Entities.Shops;
using EshopGuard.Data.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace EshopGuard.Application.Shops;

/// <summary>
/// The places of sale of an e-shop (change 10, AD 5): only markets with checks (<c>ref.markets.checks_status</c> not
/// <c>none</c> and an enabled rule set), unsupported countries stay stored and are never returned; the confirmation of the
/// client in one transaction (at least one market, only supported ones, not during an analysis or monitoring).
/// </summary>
public sealed class MarketService(EshopGuardDb db, ShopReader reader, ShopCatalog catalog, IRefCatalog refCatalog, SecurityAuditWriter audit, TimeProvider time)
{
    private static readonly RunKind[] LockingKinds = [RunKind.FullAnalysis, RunKind.Monitoring];
    private static readonly RunStatus[] FinalRunStates = [RunStatus.Finished, RunStatus.Partial, RunStatus.Failed, RunStatus.Canceled];

    public async Task<ShopMarketsDto> GetAsync(Guid shopId, CancellationToken ct)
    {
        var markets = await catalog.MarketsAsync(ct).ConfigureAwait(false);
        var checks = (await refCatalog.GetMarketsAsync(ct).ConfigureAwait(false)).ToDictionary(m => m.Code, m => m.ChecksStatus, StringComparer.Ordinal);
        return await db.ExecuteInTenantTransactionAsync(async () =>
        {
            await reader.ReadAsync(shopId, ct).ConfigureAwait(false);
            var rows = await db.ShopMarkets.AsNoTracking().Where(m => m.ShopId == shopId).OrderByDescending(m => m.IsHome).ThenBy(m => m.CountryCode)
                .ToListAsync(ct).ConfigureAwait(false);
            return ToDto(rows, markets, checks);
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// <c>PUT …/markets</c>: the ticked markets become <c>active</c>, the other supported ones <c>declined</c>, a supported
    /// market without a row gets one with <c>source = user</c>; all with <c>confirmed_by</c> and <c>confirmed_at</c>.
    /// </summary>
    public async Task<ShopMarketsDto> ConfirmAsync(Guid userId, Guid shopId, IReadOnlyList<string>? active, CancellationToken ct)
    {
        var markets = await catalog.MarketsAsync(ct).ConfigureAwait(false);
        var codes = Validate(active, markets);
        var now = time.GetUtcNow();
        await db.ExecuteInTenantTransactionAsync(async () =>
        {
            var shop = await reader.RequireAsync(shopId, ct, forUpdate: true).ConfigureAwait(false);
            if (await db.Runs.AnyAsync(r => r.ShopId == shopId && LockingKinds.Contains(r.Kind) && !FinalRunStates.Contains(r.Status), ct).ConfigureAwait(false))
            {
                throw new DomainException(ProblemCodes.MarketsLockedDuringRun, 409);
            }

            var rows = await db.ShopMarkets.Where(m => m.ShopId == shopId).ToListAsync(ct).ConfigureAwait(false);
            foreach (var market in markets.Supported)
            {
                var row = rows.FirstOrDefault(r => string.Equals(r.CountryCode, market.Country, StringComparison.OrdinalIgnoreCase));
                var ticked = codes.Contains(market.Code);
                if (row is null && !ticked)
                {
                    continue;
                }

                if (row is null)
                {
                    row = new ShopMarket { ShopId = shopId, CountryCode = market.Country, Source = MarketSource.User };
                    db.ShopMarkets.Add(row);
                }

                row.Status = ticked ? ShopMarketStatus.Active : ShopMarketStatus.Declined;
                row.ConfirmedBy = userId;
                row.ConfirmedAt = now;
            }

            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            await audit.WriteAsync(new AuditEvent(AuditActions.MarketsConfirmed, shop.TenantId, userId, "shop", shopId.ToString("D"),
                new JsonObject { ["active"] = new JsonArray([.. codes.Order(StringComparer.Ordinal).Select(c => (JsonNode)c)]) }), ct).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);
        return await GetAsync(shopId, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The ticked markets of a request: at least one (<c>400 markets.none_selected</c>), every one a code of two letters
    /// (<c>markets.unknown</c>) of a market with checks (<c>markets.unsupported</c>, <c>params.code</c>).
    /// </summary>
    public static HashSet<string> Validate(IReadOnlyList<string>? active, MarketCatalog markets)
    {
        ArgumentNullException.ThrowIfNull(markets);
        var codes = (active ?? []).Select(c => c?.Trim().ToLowerInvariant() ?? "").ToHashSet(StringComparer.Ordinal);
        if (codes.Count == 0)
        {
            throw new DomainException(ProblemCodes.MarketsNoneSelected, 400);
        }

        foreach (var code in codes.Order(StringComparer.Ordinal))
        {
            // A code of a market is two letters; another country (Poland) is a market without checks, not an unknown value.
            if (code.Length != 2 || !code.All(char.IsAsciiLetterLower))
            {
                throw new DomainException(ProblemCodes.MarketsUnknown, 400, new Dictionary<string, object?> { ["code"] = code });
            }

            if (!markets.Supported.Any(m => m.Code == code))
            {
                throw new DomainException(ProblemCodes.MarketsUnsupported, 400, new Dictionary<string, object?> { ["code"] = code });
            }
        }

        return codes;
    }

    private static ShopMarketsDto ToDto(IReadOnlyList<ShopMarket> rows, MarketCatalog markets, IReadOnlyDictionary<string, string> checks)
    {
        var list = new List<ShopMarketDto>();
        foreach (var row in rows.Where(r => r.Status != ShopMarketStatus.Unsupported))
        {
            if (markets.ByCountry(row.CountryCode) is not { } market || !markets.Supported.Contains(market)
                || checks.GetValueOrDefault(market.Code) is null or "none")
            {
                continue;
            }

            list.Add(new ShopMarketDto(
                market.Country, market.Code, row.IsHome, ShopReader.Text(row.Status), MarketEvidenceMapper.Preselected(row),
                row.EvidenceLevel is { } level ? ShopReader.Text(level) : null, ShopReader.Text(row.Source),
                MarketEvidenceMapper.Map(row.Evidence, row.IsHome), checks[market.Code]));
        }

        var confirmed = rows.Where(r => r.ConfirmedAt is not null).OrderByDescending(r => r.ConfirmedAt).FirstOrDefault();
        return new ShopMarketsDto(list, confirmed?.ConfirmedAt, confirmed?.ConfirmedBy);
    }
}
