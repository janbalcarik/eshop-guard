using EshopGuard.Application.Contracts;
using EshopGuard.Data;
using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Shops;
using Microsoft.EntityFrameworkCore;

namespace EshopGuard.Application.Fixes;

/// <summary>
/// Whether fixes of an e-shop can be published (change 11, AD 4 and 8): source <c>connector</c>, a connector <c>connected</c>
/// with write access, a registered <see cref="IFixPublisher"/>, the ownership verified and at least one accepted change.
/// Otherwise the reason code.
/// </summary>
public sealed class PublishAvailability(EshopGuardDb db, IServiceProvider services)
{
    public const string NoConnector = "no_connector";
    public const string ConnectorError = "connector_error";
    public const string ReadOnly = "read_only";
    public const string PublisherMissing = "publisher_missing";
    public const string NothingAccepted = "nothing_accepted";
    public const string OwnershipNotVerified = "ownership_not_verified";

    /// <summary>The reason why the e-shop cannot publish at all (without counting accepted changes), or null.</summary>
    public async Task<(string? Reason, Connector? Connector)> BlockerAsync(Shop shop, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(shop);
        if (shop.SourceMode != ShopSourceMode.Connector)
        {
            return (NoConnector, null);
        }

        var connector = await db.Connectors.AsNoTracking().Where(c => c.ShopId == shop.Id).OrderByDescending(c => c.CreatedAt).FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (connector is null)
        {
            return (NoConnector, null);
        }

        if (connector.Status != ConnectorStatus.Connected)
        {
            return (ConnectorError, connector);
        }

        if (connector.Access != ConnectorAccess.ReadWrite)
        {
            return (ReadOnly, connector);
        }

        if (Publisher is null)
        {
            return (PublisherMissing, connector);
        }

        // Writing into the e-shop needs the ownership verified (the connector itself verifies it when it connects).
        return (shop.OwnershipVerifiedAt is null ? OwnershipNotVerified : null, connector);
    }

    public IFixPublisher? Publisher => (IFixPublisher?)services.GetService(typeof(IFixPublisher));

    public async Task<PublishAvailabilityDto> ForAsync(Shop shop, int acceptedCount, CancellationToken ct)
    {
        var (reason, _) = await BlockerAsync(shop, ct).ConfigureAwait(false);
        reason ??= acceptedCount == 0 ? NothingAccepted : null;
        return new PublishAvailabilityDto(reason is null, reason, SnakeCaseEnumConverter<ShopPlatform>.ToText(shop.Platform), acceptedCount);
    }
}
