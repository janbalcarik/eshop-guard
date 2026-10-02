using System.Text.Json;
using EshopGuard.Data.Entities.Ops;
using EshopGuard.Jobs.Queue;

namespace EshopGuard.Jobs.Fixes;

/// <summary>
/// The jobs of the fixes (change 11): the recheck of a text by Jev (<c>fix.recheck</c>, P0, class <c>jev</c>; one at a time
/// for a proposal or group, deduplicated by the text) and the proposal after the answer „Nie“
/// (<c>fix.generate_for_answer</c>, P0, class <c>llm</c>). Payloads carry only ids and the fingerprint of the text.
/// </summary>
public static class FixJobs
{
    public const string RecheckKind = "fix.recheck";
    public const string GenerateForAnswerKind = "fix.generate_for_answer";

    public const string TargetProposal = "proposal";
    public const string TargetGroup = "group";

    /// <summary>Attempts of a job (a crashed worker or a failed call of Jev).</summary>
    public const int MaxAttempts = 3;

    /// <summary>
    /// The recheck of a text; <paramref name="jurisdictions"/> (the active markets when asked) are part of the dedupe key, so a
    /// text is checked again when a market was added since.
    /// </summary>
    public static JobRequest Recheck(Guid tenantId, Guid shopId, string target, Guid id, string textHash, IEnumerable<string> jurisdictions) => new(
        RecheckKind, JobResourceClass.Jev, JobPriority.P0,
        JsonDocument.Parse($$"""{"target":"{{target}}","id":"{{id:D}}","text_hash":"{{textHash}}"}"""),
        TenantId: tenantId, ShopId: shopId, DedupeKey: $"recheck:{id:N}:{textHash}:{string.Join('+', jurisdictions.Order(StringComparer.Ordinal))}",
        ConcurrencyKey: $"recheck:{id:N}", MaxAttempts: MaxAttempts);

    public static JobRequest GenerateForAnswer(Guid tenantId, Guid shopId, Guid questionId, Guid findingId) => new(
        GenerateForAnswerKind, JobResourceClass.Llm, JobPriority.P0,
        JsonDocument.Parse($$"""{"question_id":"{{questionId:D}}","finding_id":"{{findingId:D}}"}"""),
        TenantId: tenantId, ShopId: shopId, DedupeKey: $"answer:{questionId:N}:{findingId:N}", ConcurrencyKey: $"answer:{findingId:N}", MaxAttempts: MaxAttempts);
}
