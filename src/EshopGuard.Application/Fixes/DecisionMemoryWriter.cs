using EshopGuard.Jobs.Fixes;
using EshopGuard.Data;
using EshopGuard.Data.Entities.Fixes;
using Microsoft.EntityFrameworkCore;

namespace EshopGuard.Application.Fixes;

/// <summary>
/// The memory of decisions (change 11, AD 9): the same text of the e-shop (<c>segment_hash</c> and the normalized text) gets
/// the same decision without a model (monitoring, change 16). A new decision about a text supersedes the older one; taking a
/// decision back sets <c>superseded_at</c> and deletes nothing. <c>auto_publish</c> stays false. A finding without a text of
/// its own (the whole site) has no key and is not remembered.
/// </summary>
public sealed class DecisionMemoryWriter(EshopGuardDb db, TimeProvider time)
{
    public async Task<DecisionMemory?> RecordAsync(
        Guid shopId, long? segmentHash, string? text, Decision decision, Guid? userId, CancellationToken ct,
        string? replacementText = null, Guid? evidenceId = null, Guid? sourceProposalId = null)
    {
        if (segmentHash is not { } hash || string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        await SupersedeAsync(shopId, hash, ct).ConfigureAwait(false);
        var entry = new DecisionMemory
        {
            ShopId = shopId,
            SegmentHash = hash,
            NormalizedText = Normalize(text),
            Decision = decision,
            ReplacementText = replacementText,
            EvidenceId = evidenceId,
            SourceProposalId = sourceProposalId,
            AutoPublish = false,
            CreatedBy = userId,
        };
        db.DecisionMemory.Add(entry);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return entry;
    }

    /// <summary>Marks the current decisions about a text as superseded; returns how many.</summary>
    public async Task<int> SupersedeAsync(Guid shopId, long? segmentHash, CancellationToken ct, Guid? evidenceId = null, Guid? sourceProposalId = null)
    {
        if (segmentHash is not { } hash)
        {
            return 0;
        }

        var now = time.GetUtcNow();
        var query = db.DecisionMemory.Where(m => m.ShopId == shopId && m.SegmentHash == hash && m.SupersededAt == null);
        if (evidenceId is { } evidence)
        {
            query = query.Where(m => m.EvidenceId == evidence);
        }

        if (sourceProposalId is { } proposal)
        {
            query = query.Where(m => m.SourceProposalId == proposal);
        }

        return await query.ExecuteUpdateAsync(s => s.SetProperty(m => m.SupersededAt, now).SetProperty(m => m.UpdatedAt, now), ct).ConfigureAwait(false);
    }

    /// <summary>Supersedes every current decision that used a piece of evidence (a deleted or expired one).</summary>
    public async Task<int> SupersedeEvidenceAsync(Guid evidenceId, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        return await db.DecisionMemory.Where(m => m.EvidenceId == evidenceId && m.SupersededAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.SupersededAt, now).SetProperty(m => m.UpdatedAt, now), ct).ConfigureAwait(false);
    }

    /// <summary>Whitespace collapsed, as the text is compared.</summary>
    public static string Normalize(string text) => PageText.Normalize(text);
}
