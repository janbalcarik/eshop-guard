using EshopGuard.Application.Contracts;
using EshopGuard.Application.Findings;
using EshopGuard.Data;
using EshopGuard.Data.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace EshopGuard.Application.Runs;

/// <summary>
/// The state of an entity of an e-shop after its notification (<c>GET S/events</c>): proposal, group, publication, question or
/// run, read under RLS of the stream's request; null when the tenant does not see it.
/// </summary>
public sealed class ShopChangeReader(EshopGuardDb db, Shops.ShopReader shops)
{
    /// <summary>The e-shop of the stream (<c>404 shop.not_found</c> for one the tenant does not have).</summary>
    public async Task RequireShopAsync(Guid shopId, CancellationToken ct) =>
        await db.ExecuteInTenantTransactionAsync(() => shops.ReadAsync(shopId, ct), ct).ConfigureAwait(false);

    public async Task<ShopChangeDto?> ReadAsync(Guid shopId, string entity, Guid id, CancellationToken ct) =>
        await db.ExecuteInTenantTransactionAsync(async () => entity switch
        {
            "proposal" => await db.FixProposals.AsNoTracking().Where(p => p.ShopId == shopId && p.Id == id)
                .Select(p => new { p.Status, p.RecheckStatus }).FirstOrDefaultAsync(ct).ConfigureAwait(false) is { } p
                ? new ShopChangeDto(entity, id, FindingMapper.Text(p.Status), FindingMapper.Text(p.RecheckStatus)) : null,
            "group" => await db.FixGroups.AsNoTracking().Where(g => g.ShopId == shopId && g.Id == id)
                .Select(g => new { g.Status, g.RecheckStatus }).FirstOrDefaultAsync(ct).ConfigureAwait(false) is { } g
                ? new ShopChangeDto(entity, id, FindingMapper.Text(g.Status), g.RecheckStatus is { } r ? FindingMapper.Text(r) : null) : null,
            "publication" => await db.Publications.AsNoTracking().Where(p => p.ShopId == shopId && p.Id == id)
                .Select(p => (Data.Entities.Fixes.PublicationStatus?)p.Status).FirstOrDefaultAsync(ct).ConfigureAwait(false) is { } s
                ? new ShopChangeDto(entity, id, FindingMapper.Text(s), null) : null,
            "question" => await db.Questions.AsNoTracking().Where(q => q.ShopId == shopId && q.Id == id)
                .Select(q => (Data.Entities.Checks.QuestionStatus?)q.Status).FirstOrDefaultAsync(ct).ConfigureAwait(false) is { } q
                ? new ShopChangeDto(entity, id, FindingMapper.Text(q), null) : null,
            "run" => await db.Runs.AsNoTracking().Where(r => r.ShopId == shopId && r.Id == id)
                .Select(r => (Data.Entities.Checks.RunStatus?)r.Status).FirstOrDefaultAsync(ct).ConfigureAwait(false) is { } r
                ? new ShopChangeDto(entity, id, FindingMapper.Text(r), null) : null,
            _ => null,
        }, ct).ConfigureAwait(false);
}
