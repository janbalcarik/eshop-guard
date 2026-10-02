using EshopGuard.Application.Contracts;
using EshopGuard.Application.Fixes;
using EshopGuard.Application.Problems;
using EshopGuard.Application.Shops;
using EshopGuard.Data;
using EshopGuard.Data.Entities.Checks;
using EshopGuard.Data.Entities.Fixes;
using EshopGuard.Data.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace EshopGuard.Application.Findings;

/// <summary>
/// Decisions about a finding (change 11, design B): „Ponechať“ (<c>kept</c>, reason <c>no_promise</c> or <c>other</c>,
/// remembered as <c>keep</c>), „Nejde o problém“ (<c>dismissed</c>, reason <c>false_positive</c>, K rozhodnutí 3) and
/// „Znovu otvoriť“ (back to <c>open</c>; the remembered decision is superseded, not deleted). Only by the table of
/// <see cref="FindingStatusMachine"/>; an e-shop with only the sample is <c>409 shop.sample_only</c>.
/// </summary>
public sealed class FindingDecisionService(
    EshopGuardDb db, ShopReader reader, FindingTransitions transitions, DecisionMemoryWriter memory, FindingQueryService queries)
{
    public static readonly string[] KeepReasons = ["no_promise", "other"];
    public static readonly string[] DismissReasons = ["false_positive"];

    public Task<FindingDetailDto> KeepAsync(Guid userId, Guid shopId, Guid findingId, string? reasonCode, CancellationToken ct) =>
        DecideAsync(userId, shopId, findingId, FindingStatus.Kept, () => Reason(reasonCode, KeepReasons), ct);

    public Task<FindingDetailDto> DismissAsync(Guid userId, Guid shopId, Guid findingId, string? reasonCode, CancellationToken ct) =>
        DecideAsync(userId, shopId, findingId, FindingStatus.Dismissed, () => Reason(reasonCode, DismissReasons), ct);

    public Task<FindingDetailDto> ReopenAsync(Guid userId, Guid shopId, Guid findingId, CancellationToken ct) =>
        DecideAsync(userId, shopId, findingId, FindingStatus.Open, null, ct);

    /// <summary>The e-shop first (another tenant's is <c>404 shop.not_found</c> whatever the body), then the reason.</summary>
    private async Task<FindingDetailDto> DecideAsync(Guid userId, Guid shopId, Guid findingId, FindingStatus to, Func<string>? reason, CancellationToken ct) =>
        await db.ExecuteInTenantTransactionAsync(async () =>
        {
            var shop = await reader.RequireAsync(shopId, ct).ConfigureAwait(false);
            var reasonCode = reason?.Invoke();
            SampleOnlyGuard.Ensure(shop);
            var finding = await db.Findings.FirstOrDefaultAsync(f => f.Id == findingId && f.ShopId == shopId, ct).ConfigureAwait(false)
                ?? throw new DomainException(ProblemCodes.FindingNotFound, 404);
            var from = finding.Status;
            await transitions.ApplyAsync(finding, to, userId, ct, reasonCode).ConfigureAwait(false);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            if (to == FindingStatus.Kept)
            {
                await memory.RecordAsync(shopId, finding.SegmentHash, finding.Text, Decision.Keep, userId, ct).ConfigureAwait(false);
            }
            else if (to == FindingStatus.Open && from is FindingStatus.Kept or FindingStatus.KeptWithEvidence)
            {
                await memory.SupersedeAsync(shopId, finding.SegmentHash, ct).ConfigureAwait(false);
            }

            return await queries.DetailInTransactionAsync(shopId, findingId, ct).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

    private static string Reason(string? reasonCode, string[] allowed) =>
        reasonCode is not null && allowed.Contains(reasonCode)
            ? reasonCode
            : throw DomainException.Validation(new ValidationResult().Add("reasonCode", reasonCode is null ? ProblemCodes.Fields.Required : ProblemCodes.Fields.ValueNotAllowed));
}
