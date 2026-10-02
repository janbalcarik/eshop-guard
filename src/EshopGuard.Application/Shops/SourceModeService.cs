using System.Text.Json.Nodes;
using EshopGuard.Application.Audit;
using EshopGuard.Application.Contracts;
using EshopGuard.Application.Problems;
using EshopGuard.Data;
using EshopGuard.Data.Entities.Shops;
using EshopGuard.Data.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EshopGuard.Application.Shops;

/// <summary>A feed in the request of <see cref="SourceModeService"/>.</summary>
public sealed record FeedInput(string? Url, string? Format);

/// <summary>
/// How the texts of an e-shop are read (change 10, AD 3): <c>web</c> (default), <c>feed</c> (the address through the same
/// check as an e-shop, format <c>heureka</c> or <c>google</c>; the feed is downloaded only by the worker) or <c>connector</c>
/// (only with a connected connector, <c>409 shop.connector_not_connected</c>). Not during a running run.
/// </summary>
public sealed class SourceModeService(EshopGuardDb db, ShopReader reader, ShopService shops, SecurityAuditWriter audit, IOptions<ShopsOptions> options)
{
    public async Task<ShopDto> SetAsync(Guid userId, Guid shopId, string? mode, FeedInput? feed, CancellationToken ct)
    {
        ShopSourceMode sourceMode;
        switch (mode)
        {
            case "web":
                sourceMode = ShopSourceMode.Web;
                break;
            case "feed":
                sourceMode = ShopSourceMode.Feed;
                break;
            case "connector":
                sourceMode = ShopSourceMode.Connector;
                break;
            default:
                throw DomainException.Validation(new ValidationResult().Add("mode", ProblemCodes.SourceModeUnknown));
        }

        ShopAddress? feedAddress = null;
        FeedFormat format = default;
        if (sourceMode == ShopSourceMode.Feed)
        {
            if (feed?.Format is not ("heureka" or "google"))
            {
                throw new DomainException(ProblemCodes.FeedFormatUnknown, 400, new Dictionary<string, object?> { ["allowed"] = new[] { "heureka", "google" } });
            }

            format = feed.Format == "heureka" ? FeedFormat.Heureka : FeedFormat.Google;
            feedAddress = ShopUrlNormalizer.Normalize(feed.Url, options.Value.AllowedDevHosts, ProblemCodes.FeedUrlInvalid, ProblemCodes.FeedUrlInvalid);
        }

        return await db.ExecuteInTenantTransactionAsync(async () =>
        {
            var shop = await reader.RequireAsync(shopId, ct, forUpdate: true).ConfigureAwait(false);
            await shops.ThrowIfRunInProgressAsync(shopId, ct).ConfigureAwait(false);
            if (sourceMode == ShopSourceMode.Connector
                && !await db.Connectors.AnyAsync(c => c.ShopId == shopId && c.Status == ConnectorStatus.Connected, ct).ConfigureAwait(false))
            {
                throw new DomainException(ProblemCodes.ShopConnectorNotConnected, 409);
            }

            if (feedAddress is not null)
            {
                // One feed per e-shop: the address of the feed (with its query, e.g. a key of the export) as the client gave it.
                var url = FeedUrl(feed!.Url!);
                var existing = await db.Feeds.Where(f => f.ShopId == shopId).OrderByDescending(f => f.CreatedAt).ToListAsync(ct).ConfigureAwait(false);
                if (existing.FirstOrDefault() is { } current)
                {
                    current.Url = url;
                    current.Format = format;
                    current.Etag = null;
                    db.Feeds.RemoveRange(existing.Skip(1));
                }
                else
                {
                    db.Feeds.Add(new Feed { ShopId = shopId, Url = url, Format = format });
                }
            }

            var previous = shop.SourceMode;
            shop.SourceMode = sourceMode;
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            await audit.WriteAsync(new AuditEvent(AuditActions.ShopSourceChanged, shop.TenantId, userId, "shop", shop.Id.ToString("D"),
                new JsonObject { ["from"] = ShopReader.Text(previous), ["to"] = mode }), ct).ConfigureAwait(false);
            return await reader.DtoAsync(shop, ct).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);
    }

    /// <summary>The address of the feed with a scheme, without a fragment; the query stays (exports carry their key in it).</summary>
    private static string FeedUrl(string input)
    {
        var text = input.Trim();
        var uri = new Uri(text.Contains("://", StringComparison.Ordinal) ? text : "https://" + text);
        return uri.GetLeftPart(UriPartial.Query);
    }
}
