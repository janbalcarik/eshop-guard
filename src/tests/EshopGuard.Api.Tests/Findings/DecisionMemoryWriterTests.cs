using EshopGuard.Application.Fixes;
using EshopGuard.Data;
using EshopGuard.Data.Entities.Fixes;
using EshopGuard.Data.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EshopGuard.Api.Tests.Findings;

/// <summary>The memory of decisions (change 11, AD 9): a new decision supersedes the older one, taking it back deletes nothing.</summary>
public sealed class DecisionMemoryWriterTests : FindingsTestBase
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task NewDecision_SupersedesTheOlder_TextIsNormalized_AndNothingIsPublishedAutomatically()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        var shopId = (await CreateShopAsync(owner)).GetProperty("id").GetGuid();

        var rows = await InTenantAsync(factory, owner, async (writer, db) =>
        {
            await writer.RecordAsync(shopId, 9001, "Ekologický  šampón\n pre deti", Decision.Replace, owner.UserId, Ct, replacementText: "Šampón pre deti.");
            await writer.RecordAsync(shopId, 9001, "Ekologický šampón pre deti", Decision.Keep, owner.UserId, Ct);
            return await db.DecisionMemory.AsNoTracking().Where(m => m.ShopId == shopId).OrderBy(m => m.CreatedAt).ToListAsync(Ct);
        });

        Assert.Equal(2, rows.Count);
        Assert.Equal((Decision.Replace, true), (rows[0].Decision, rows[0].SupersededAt is not null));
        Assert.Equal((Decision.Keep, false), (rows[1].Decision, rows[1].SupersededAt is not null));
        Assert.All(rows, r => Assert.Equal("Ekologický šampón pre deti", r.NormalizedText));
        Assert.All(rows, r => Assert.False(r.AutoPublish));
        Assert.Equal("Šampón pre deti.", rows[0].ReplacementText);
    }

    [Fact]
    public async Task FindingWithoutItsOwnText_IsNotRemembered()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        var shopId = (await CreateShopAsync(owner)).GetProperty("id").GetGuid();

        var (withoutHash, withoutText, count) = await InTenantAsync(factory, owner, async (writer, db) =>
        {
            var a = await writer.RecordAsync(shopId, null, "Celý web", Decision.Keep, owner.UserId, Ct);
            var b = await writer.RecordAsync(shopId, 9002, "  ", Decision.Keep, owner.UserId, Ct);
            return (a, b, await db.DecisionMemory.CountAsync(m => m.ShopId == shopId, Ct));
        });

        Assert.Null(withoutHash);
        Assert.Null(withoutText);
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task TakingBack_SupersedesOnlyTheDecisionOfTheProposalOrEvidence()
    {
        var (factory, owner, data) = await BylinkovoAsync();
        await using var _ = factory;
        using var __ = owner;
        var shopId = data.ShopId;
        var proposalA = data.Proposals["zubna.3"];
        var proposalB = data.Proposals["zubna.4"];

        var (byOtherProposal, byProposal, byEvidence, current) = await InTenantAsync(factory, owner, async (writer, db) =>
        {
            var evidence = new EvidenceItem { ClaimText = "COSMOS Organic", SubjectKind = EvidenceSubjectKind.Brand, Kind = EvidenceKind.Certificate, Source = EvidenceSource.Upload, Status = EvidenceStatus.Valid, CreatedBy = owner.UserId };
            db.Add(evidence);
            await db.SaveChangesAsync(Ct);
            await writer.RecordAsync(shopId, 9003, "Prírodné sérum", Decision.Replace, owner.UserId, Ct, replacementText: "Sérum.", sourceProposalId: proposalA);
            await writer.RecordAsync(shopId, 9004, "Certifikovaná kozmetika", Decision.KeepWithEvidence, owner.UserId, Ct, evidenceId: evidence.Id);
            var other = await writer.SupersedeAsync(shopId, 9003, Ct, sourceProposalId: proposalB);
            var own = await writer.SupersedeAsync(shopId, 9003, Ct, sourceProposalId: proposalA);
            var byEvidence = await writer.SupersedeEvidenceAsync(evidence.Id, Ct);
            return (other, own, byEvidence, await db.DecisionMemory.CountAsync(m => m.ShopId == shopId && m.SegmentHash >= 9003 && m.SegmentHash <= 9004 && m.SupersededAt == null, Ct));
        });

        Assert.Equal((0, 1, 1, 0), (byOtherProposal, byProposal, byEvidence, current));
        Assert.Equal(2L, await ShopSeed.ScalarAsync<long>("SELECT count(*) FROM fixes.decision_memory WHERE shop_id = $1 AND segment_hash IN (9003, 9004)", shopId));
    }

    private static async Task<T> InTenantAsync<T>(ApiFactory factory, Person owner, Func<DecisionMemoryWriter, EshopGuardDb, Task<T>> action)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(owner.TenantId, owner.UserId);
        var db = scope.ServiceProvider.GetRequiredService<EshopGuardDb>();
        var writer = scope.ServiceProvider.GetRequiredService<DecisionMemoryWriter>();
        return await db.ExecuteInTenantTransactionAsync(() => action(writer, db), Ct);
    }
}
