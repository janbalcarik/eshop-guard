using System.Globalization;
using System.Text.Json;
using EshopGuard.Core.Fix;
using EshopGuard.Core.Models;
using EshopGuard.Core.Pipeline;
using EshopGuard.Data;
using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Checks;
using EshopGuard.Data.Entities.Fixes;
using EshopGuard.Data.Entities.Ops;
using EshopGuard.Data.Entities.Usage;
using EshopGuard.Data.Tenancy;
using EshopGuard.Jobs.Processing;
using EshopGuard.Jobs.Runs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Finding = EshopGuard.Data.Entities.Checks.Finding;

namespace EshopGuard.Jobs.Fixes;

/// <summary>
/// The job <c>fix.generate_for_answer</c> (change 11, AD 6): after „Nie“ without a ready variant, the sentence of the finding
/// is rewritten by the rewrite of change 5 (<see cref="ITextRewriter"/>, OpenAI) with the answer as a fact, once for all its
/// pages, and the new text is checked again by the rules (Jev included) in the active markets. The result is the variant
/// <c>answer_no</c> of every open proposal of the finding (a new proposal when it has none); only when the check is
/// <c>ok</c> the variant is selected and the finding goes <c>open → proposed</c>. Otherwise the finding stays <c>open</c>
/// and the variant shows the failed check. A changed answer or a finding decided meanwhile makes the job a no-op. Usage:
/// OpenAI as <c>rewrite</c>, Jev as <c>recheck</c>, without a run. Logs carry ids and codes only.
/// </summary>
public sealed class FixGenerateForAnswerHandler(ITextRewriter rewriter, UsageRecorder usage, TimeProvider time, ILogger<FixGenerateForAnswerHandler> logger) : IJobHandler
{
    public string Kind => FixJobs.GenerateForAnswerKind;

    public JobResourceClass ResourceClass => JobResourceClass.Llm;

    public async Task<JobResult> ExecuteAsync(JobExecutionContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        var payload = context.Job.Payload.RootElement;
        var questionId = Guid.Parse(payload.GetProperty("question_id").GetString()!);
        var findingId = Guid.Parse(payload.GetProperty("finding_id").GetString()!);
        var db = context.Services.GetRequiredService<EshopGuardDb>();
        var blobs = context.Services.GetRequiredService<ExtractContextReader>();
        var input = await db.ExecuteInTenantTransactionAsync(() => LoadAsync(db, blobs, questionId, findingId, ct), ct).ConfigureAwait(false);
        if (input is null)
        {
            logger.LogInformation("fix.generate_for_answer {QuestionId} {FindingId} stale", questionId, findingId);
            return JobResult.Done;
        }

        var scope = new RunAmbientScope(input.TenantId, input.ShopId, Guid.Empty, context.Job.Id) { JevOperation = UsageOperation.Recheck };
        RewriteResult result;
        using (RunAmbient.Enter(scope))
        {
            try
            {
                result = await rewriter.RewriteAsync(input.Rewrite, null, ct).ConfigureAwait(false);
            }
            finally
            {
                await usage.FlushAsync(scope, CancellationToken.None).ConfigureAwait(false);
            }
        }

        var page = result.Pages.FirstOrDefault();
        if (page?.Error is not null && page.ErrorIsTransient)
        {
            // The attempt is repeated (FixJobs.MaxAttempts); a received rewrite is in the cache.
            throw new InvalidOperationException("fix.generate_for_answer: the rewrite is unavailable.");
        }

        var change = page?.Changes.FirstOrDefault(c => c.FindingIds.Count > 0);
        var status = change?.Status switch
        {
            RewriteStatus.Resolved => RecheckStatus.Ok,
            // A placeholder asks for a fact the merchant just said they cannot back: not a usable text.
            RewriteStatus.WaitingForFacts or RewriteStatus.StillFinding => RecheckStatus.StillFinding,
            _ => (RecheckStatus?)null,
        };
        var outcome = "not_addressed";
        await context.CompleteAsync(async tx =>
        {
            await usage.WriteAsync(tx.Connection, tx.Transaction, scope,
                [new UsageEntry(UsageProvider.Openai, UsageOperation.Rewrite, result.Model, Math.Max(0, result.Stats.Pages - result.Stats.FromCache - result.Stats.Errors),
                    result.Stats.FromCache, result.Stats.InputTokens, result.Stats.CachedTokens, result.Stats.OutputTokens, 0, result.Stats.CostUsd)], ct).ConfigureAwait(false);
            if (change is null || status is not { } recheck)
            {
                return;
            }

            var question = await tx.Db.Questions.AsNoTracking().FirstOrDefaultAsync(q => q.Id == questionId, ct).ConfigureAwait(false);
            var finding = await tx.Db.Findings.FirstOrDefaultAsync(f => f.Id == findingId, ct).ConfigureAwait(false);
            if (question is not { Status: QuestionStatus.Answered, Answer: "no" } || finding is not { Status: FindingStatus.Open })
            {
                outcome = "stale";
                return;
            }

            var now = time.GetUtcNow();
            var proposals = await tx.Db.FixProposals
                .Where(p => p.FindingIds.Contains(findingId) && (p.Status == FixProposalStatus.Proposed || p.Status == FixProposalStatus.Edited))
                .ToListAsync(ct).ConfigureAwait(false);
            var ready = proposals.Count > 0;
            foreach (var proposal in proposals)
            {
                if (Apply(proposal.OriginalText, change) is not { } text)
                {
                    ready = false;
                    continue;
                }

                proposal.Alternatives = JsonDocument.Parse(WithAnswerNo(proposal.Alternatives, text, recheck));
                if (recheck == RecheckStatus.Ok)
                {
                    proposal.SelectedAlternative = ProposalText.AnswerNo;
                    proposal.EditedText = null;
                    proposal.Status = FixProposalStatus.Proposed;
                }

                proposal.UpdatedAt = now;
            }

            if (proposals.Count == 0 && input.Page is { } target)
            {
                tx.Db.FixProposals.Add(new FixProposal
                {
                    TenantId = input.TenantId,
                    ShopId = input.ShopId,
                    PageId = target.PageId,
                    PageVersionId = target.VersionId,
                    FindingIds = [findingId],
                    Field = FixField.Block,
                    BlockIndex = Block(change.BlockIds),
                    OriginalText = change.Original,
                    ProposedText = change.Rewritten,
                    Alternatives = JsonDocument.Parse(WithAnswerNo(null, change.Rewritten, recheck)),
                    SelectedAlternative = recheck == RecheckStatus.Ok ? ProposalText.AnswerNo : null,
                    RecheckStatus = recheck,
                    Status = FixProposalStatus.Proposed,
                });
                ready = true;
            }

            outcome = recheck == RecheckStatus.Ok && ready ? "proposed" : "check_failed";
            if (outcome == "proposed")
            {
                finding.Status = FindingStatus.Proposed;
                finding.UpdatedAt = now;
                await tx.Db.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                    INSERT INTO ops.audit_log (at, tenant_id, actor_user_id, actor_kind, action, entity_type, entity_id, data, created_at)
                    VALUES ({now}, {input.TenantId}, NULL, 'system', 'finding.status_changed', 'finding', {findingId.ToString("D")},
                        jsonb_build_object('from', 'open', 'to', 'proposed', 'reasonCode', 'answer_no_generated'), {now})
                    """, ct).ConfigureAwait(false);
            }

            await tx.Db.SaveChangesAsync(ct).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);
        logger.LogInformation("fix.generate_for_answer {QuestionId} {FindingId} {Outcome}", questionId, findingId, outcome);
        return JobResult.Done;
    }

    private sealed record TargetPage(Guid PageId, Guid VersionId);

    private sealed record GenerateInput(Guid TenantId, Guid ShopId, RewriteInput Rewrite, TargetPage? Page);

    private static async Task<GenerateInput?> LoadAsync(EshopGuardDb db, ExtractContextReader blobs, Guid questionId, Guid findingId, CancellationToken ct)
    {
        var question = await db.Questions.AsNoTracking().FirstOrDefaultAsync(q => q.Id == questionId, ct).ConfigureAwait(false);
        var finding = await db.Findings.AsNoTracking().FirstOrDefaultAsync(f => f.Id == findingId, ct).ConfigureAwait(false);
        if (question is not { Status: QuestionStatus.Answered, Answer: "no" } || finding is not { Status: FindingStatus.Open } || string.IsNullOrWhiteSpace(finding.Text))
        {
            return null;
        }

        var proposalPage = await db.FixProposals.AsNoTracking()
            .Where(p => p.FindingIds.Contains(findingId) && (p.Status == FixProposalStatus.Proposed || p.Status == FixProposalStatus.Edited))
            .OrderBy(p => p.Id).Select(p => (Guid?)p.PageId).FirstOrDefaultAsync(ct).ConfigureAwait(false);
        var pageId = proposalPage ?? finding.PageId
            ?? await db.FindingOccurrences.AsNoTracking().Where(o => o.FindingId == findingId).OrderBy(o => o.PageId).Select(o => (Guid?)o.PageId).FirstOrDefaultAsync(ct).ConfigureAwait(false);
        var page = pageId is null ? null : await db.Pages.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pageId && p.ShopId == finding.ShopId, ct).ConfigureAwait(false);
        if (page?.CurrentVersionId is not { } versionId)
        {
            return null;
        }

        var key = await db.PageVersions.AsNoTracking().Where(v => v.ShopId == finding.ShopId && v.Id == versionId).Select(v => v.ExtractBlobKey)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (await blobs.ReadAsync(finding.TenantId, finding.ShopId, key, ct).ConfigureAwait(false) is not { } text)
        {
            return null;
        }

        var jurisdictions = await FixRecheckHandler.ActiveJurisdictionsAsync(db, finding.ShopId, ct).ConfigureAwait(false);
        var rewritePage = new RewritePageInput
        {
            Url = page.Url,
            Type = page.PageType is { } type ? SnakeCaseEnumConverter<Data.Entities.Content.PageType>.ToText(type) : "",
            Title = page.Title ?? text.Title,
            Category = page.Title ?? text.Title ?? "",
            MainText = string.Join('\n', text.Blocks),
            MetaDescription = text.MetaDescription,
            JsonLdDescription = text.JsonLdDescription,
            Answers = [new MerchantAnswer { QuestionCode = question.Code, Params = Params(question.Params), Yes = false }],
        };
        var rewrite = new RewriteInput
        {
            Pages = [rewritePage],
            Findings = [Core(finding, page.Url)],
            Country = jurisdictions[0],
            Jurisdictions = jurisdictions,
            ContentLanguage = page.Language,
        };
        return new GenerateInput(finding.TenantId, finding.ShopId, rewrite, new TargetPage(page.Id, versionId));
    }

    /// <summary>The finding as the library knows it (its verdicts are stored as the library wrote them, change 8).</summary>
    private static Core.Models.Finding Core(Finding finding, string url) => new()
    {
        RuleId = finding.RuleId,
        Module = finding.Module,
        Scope = "segment",
        Text = finding.Text,
        Urls = [url],
        Sources = [SegmentSource.Main],
        Verdicts = finding.Verdicts is { } verdicts
            ? JsonSerializer.Deserialize<List<JurisdictionVerdict>>(verdicts.RootElement.GetRawText(), PipelineJson.Options) ?? []
            : [],
    };

    private static Dictionary<string, string> Params(JsonDocument? parameters) =>
        parameters?.RootElement is { ValueKind: JsonValueKind.Object } root
            ? root.EnumerateObject()
                .Where(p => p.Value.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
                .ToDictionary(p => p.Name, p => p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString()! : p.Value.GetRawText(), StringComparer.Ordinal)
            : [];

    /// <summary>The new text for a proposal of another page: the same block, or the block inside its text; otherwise none.</summary>
    private static string? Apply(string original, RewriteChange change)
    {
        var (block, sentence) = (PageText.Normalize(change.Original), PageText.Normalize(original));
        return block == sentence ? change.Rewritten
            : block.Length > 0 && sentence.Contains(block, StringComparison.Ordinal) ? sentence.Replace(block, change.Rewritten, StringComparison.Ordinal)
            : null;
    }

    /// <summary>The alternatives with the variant <c>answer_no</c> replaced by the new one.</summary>
    private static string WithAnswerNo(JsonDocument? alternatives, string text, RecheckStatus recheck)
    {
        var list = ProposalText.Alternatives(alternatives).Where(a => a.Key != ProposalText.AnswerNo).ToList();
        list.Add(new ProposalAlternative(ProposalText.AnswerNo, text, recheck));
        return JsonSerializer.Serialize(list.Select(a => new Dictionary<string, string>
        {
            ["key"] = a.Key,
            ["text"] = a.Text,
            ["recheck_status"] = SnakeCaseEnumConverter<RecheckStatus>.ToText(a.RecheckStatus),
        }));
    }

    /// <summary>The 1-based block of <c>B{n}</c>; another block (title, meta) has no index.</summary>
    private static int? Block(IReadOnlyList<string> blockIds) =>
        blockIds.FirstOrDefault() is { Length: > 1 } id && id[0] == 'B' && int.TryParse(id.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : null;
}
