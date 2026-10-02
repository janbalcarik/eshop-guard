using EshopGuard.Application.Contracts;
using EshopGuard.Application.Problems;
using EshopGuard.Data;
using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Shops;
using EshopGuard.Jobs.Shops;
using Microsoft.EntityFrameworkCore;

namespace EshopGuard.Application.Shops;

/// <summary>Reads an e-shop of the tenant set in the context (in its open transaction) and builds its DTO.</summary>
public sealed class ShopReader(EshopGuardDb db, IConnectorCatalog connectors)
{
    /// <summary>The e-shop for a change (tracked), or <c>404 shop.not_found</c>; RLS hides e-shops of other tenants.</summary>
    public async Task<Shop> RequireAsync(Guid shopId, CancellationToken ct, bool forUpdate = false)
    {
        if (forUpdate)
        {
            // Serializes changes of one e-shop (recognition, sample, markets); the row of another tenant is invisible.
            await DbSql.ExecuteAsync(db, "SELECT 1 FROM shop.shops WHERE id = @id AND deleted_at IS NULL FOR UPDATE", ct, DbSql.P("id", shopId)).ConfigureAwait(false);
        }

        return await db.Shops.FirstOrDefaultAsync(s => s.Id == shopId, ct).ConfigureAwait(false)
            ?? throw new DomainException(ProblemCodes.ShopNotFound, 404);
    }

    /// <summary>The e-shop as it is in the database now (not tracked; reading after waiting for a job of the worker).</summary>
    public async Task<Shop> ReadAsync(Guid shopId, CancellationToken ct) =>
        await db.Shops.AsNoTracking().FirstOrDefaultAsync(s => s.Id == shopId, ct).ConfigureAwait(false)
            ?? throw new DomainException(ProblemCodes.ShopNotFound, 404);

    public async Task<ShopDto> DtoAsync(Shop shop, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(shop);
        var feed = shop.SourceMode == ShopSourceMode.Feed
            ? await db.Feeds.AsNoTracking().Where(f => f.ShopId == shop.Id).OrderByDescending(f => f.CreatedAt).Select(f => new FeedDto(f.Url, Text(f.Format)))
                .FirstOrDefaultAsync(ct).ConfigureAwait(false)
            : null;
        var detection = await DetectionAsync(shop, ct).ConfigureAwait(false);
        return new ShopDto(
            shop.Id, shop.Domain, shop.BaseUrl, shop.BasePath, shop.Name, shop.HomeCountry, shop.Language, Text(shop.Platform),
            ShopDetectionState.Read(shop.Detection)?.PlatformSource ?? ShopDetectionState.SourceDetected,
            Text(shop.SourceMode), Text(shop.Status), shop.ProductCount, shop.PageCount, shop.TierCode, shop.Modules, shop.CheckHiddenOnSave,
            shop.OwnershipVerifiedAt, shop.VerificationMethod is { } method ? Text(method) : null, feed, shop.Version, detection);
    }

    /// <summary>
    /// The recognition: the result in <c>shops.detection</c>; while its job waits or runs <c>pending</c>; a job that ended
    /// without a result (crashed, canceled, deleted) is <c>failed</c> with <c>fetch_failed</c>; never requested
    /// <c>failed</c> with <c>not_started</c>.
    /// </summary>
    public async Task<DetectionDto> DetectionAsync(Shop shop, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(shop);
        var state = ShopDetectionState.Read(shop.Detection);
        var status = state?.Status ?? ShopDetectionState.Failed;
        var failure = state is null ? "not_started" : state.FailureCode;
        if (state is { Status: ShopDetectionState.Pending, JobId: { } jobId })
        {
            var jobState = await DbSql.ScalarAsync<string>(db, "SELECT state FROM ops.jobs WHERE id = @id", ct, new Npgsql.NpgsqlParameter("id", jobId))
                .ConfigureAwait(false);
            if (jobState is null or "failed" or "canceled")
            {
                status = ShopDetectionState.Failed;
                failure = "fetch_failed";
            }
        }

        var available = connectors.IsAvailable(shop.Platform);
        return new DetectionDto(
            status,
            Text(shop.Platform),
            status == ShopDetectionState.Pending ? "unknown" : state?.Confidence ?? "unknown",
            state?.Signals ?? [],
            state?.FinalUrl,
            state?.RedirectedTo is { } other ? new RedirectedDomainDto(other) : null,
            status == ShopDetectionState.Failed ? failure : null,
            new ConnectorAvailabilityDto(Text(shop.Platform), available),
            available ? "connector" : "web");
    }

    public static string Text<TEnum>(TEnum value)
        where TEnum : struct, Enum => SnakeCaseEnumConverter<TEnum>.ToText(value);
}
