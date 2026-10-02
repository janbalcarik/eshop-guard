using System.Text.Json;
using System.Text.Json.Nodes;
using EshopGuard.Application.Audit;
using EshopGuard.Application.Contracts;
using EshopGuard.Application.Findings;
using EshopGuard.Application.Problems;
using EshopGuard.Application.Shops;
using EshopGuard.Data;
using EshopGuard.Data.Entities.Checks;
using EshopGuard.Data.Entities.Fixes;
using EshopGuard.Data.Tenancy;
using EshopGuard.Jobs.Fixes;
using EshopGuard.Jobs.Queue;
using Microsoft.EntityFrameworkCore;

namespace EshopGuard.Application.Fixes;

/// <summary>
/// Questions for the merchant and their answers (change 11, AD 6). One answer applies to every question with the same code
/// about the same text in all e-shops of the tenant (<see cref="AnswerPropagation"/>), in one transaction:
/// <list type="bullet">
/// <item>„Áno“ creates evidence of the kind <c>answer</c> linked to the findings and their pages, the findings become
/// <c>kept_with_evidence</c> and the decision is remembered;</item>
/// <item>„Nie“ selects the ready variant <c>answer_no</c> (the finding becomes <c>proposed</c>); without it the finding becomes
/// <c>open</c> and the job <c>fix.generate_for_answer</c> is queued in the daily budget of the tenant (above it
/// <c>429 budget.daily_limit_reached</c> and nothing is saved). A question of the whole site keeps the remedy of its rule
/// (K rozhodnutí 10): its finding becomes <c>open</c>, no model is called.</item>
/// </list>
/// An answer can change until a proposal of the findings is published (<c>409 question.answer_locked</c>); the effects of the
/// previous answer are taken back first. The audit <c>question.answered</c> carries codes and counts, never texts.
/// </summary>
public sealed class QuestionService(
    EshopGuardDb db,
    ShopReader reader,
    ShopWorkLoader loader,
    AnswerPropagation propagation,
    FindingTransitions transitions,
    DecisionMemoryWriter memory,
    GenerationBudget budget,
    IJobQueue queue,
    SecurityAuditWriter audit,
    TimeProvider time)
{
    public const string Yes = "yes";
    public const string No = "no";
    public const int DefaultLimit = 50;
    public const int MaxLimit = 200;

    private static readonly string[] Scopes = ["finding", "site"];
    private static readonly string[] Statuses = ["open", "answered"];

    public async Task<IReadOnlyList<QuestionDto>> ListAsync(Guid shopId, string? scope, string? status, int? limit, CancellationToken ct) =>
        await db.ExecuteInTenantTransactionAsync(async () =>
        {
            var work = await loader.LoadAsync(shopId, ct).ConfigureAwait(false);
            var validation = new ValidationResult();
            if (scope is not null && !Scopes.Contains(scope))
            {
                validation.Add("scope", ProblemCodes.Fields.ValueNotAllowed);
            }

            if (status is not null && !Statuses.Contains(status))
            {
                validation.Add("status", ProblemCodes.Fields.ValueNotAllowed);
            }

            if (limit is < 1 or > MaxLimit)
            {
                validation.Add("limit", ProblemCodes.Fields.ValueNotAllowed);
            }

            if (!validation.IsValid)
            {
                throw DomainException.Validation(validation);
            }

            var findings = work.Findings.ToDictionary(f => f.Id);
            var questions = work.Questions
                .Where(q => q.FindingId is { } id ? findings.ContainsKey(id) && work.IsVisible(id) : !work.SampleOnly)
                .Where(q => scope is null || FindingMapper.Text(q.Scope) == scope)
                .Where(q => status is null || FindingMapper.Text(q.Status) == status)
                .OrderBy(q => q.Status == QuestionStatus.Open ? 0 : 1)
                .ThenBy(q => q.FindingId is { } id ? findings[id].Rank : short.MaxValue)
                .ThenBy(q => q.Id)
                .Take(limit ?? DefaultLimit)
                .ToList();
            var answeredBy = questions.Select(q => q.AnsweredBy).OfType<Guid>().Distinct().ToList();
            var names = answeredBy.Count == 0
                ? []
                : await db.Users.AsNoTracking().Where(u => answeredBy.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct).ConfigureAwait(false);
            var result = new List<QuestionDto>();
            foreach (var question in questions)
            {
                var finding = question.FindingId is { } id ? findings[id] : null;
                var page = finding is null ? null
                    : finding.PageId is { } own && work.Pages.GetValueOrDefault(own) is { } ownPage ? ownPage
                    : (work.PagesOf.GetValueOrDefault(finding.Id) ?? []).Select(p => work.Pages.GetValueOrDefault(p)).OfType<WorkPage>().FirstOrDefault();
                result.Add(FindingMapper.Question(question, finding, page, await propagation.AppliesToAsync(question, ct).ConfigureAwait(false),
                    question.AnsweredBy is { } by ? names.GetValueOrDefault(by) : null));
            }

            return (IReadOnlyList<QuestionDto>)result;
        }, ct).ConfigureAwait(false);

    public async Task<AnswerResultDto> AnswerAsync(Guid userId, Guid shopId, Guid questionId, string? answer, CancellationToken ct) =>
        await db.ExecuteInTenantTransactionAsync(async () =>
        {
            var shop = await reader.ReadAsync(shopId, ct).ConfigureAwait(false);
            SampleOnlyGuard.Ensure(shop);
            var question = await db.Questions.FirstOrDefaultAsync(q => q.Id == questionId && q.ShopId == shopId, ct).ConfigureAwait(false)
                ?? throw new DomainException(ProblemCodes.QuestionNotFound, 404);
            if (answer is not (Yes or No))
            {
                throw DomainException.Validation(new ValidationResult().Add("answer", ProblemCodes.Fields.ValueNotAllowed));
            }

            var all = await propagation.MatchingAsync(question, includeAnswered: question.Status == QuestionStatus.Answered, ct).ConfigureAwait(false);
            var counts = await propagation.CountAsync(all, ct).ConfigureAwait(false);

            // The same answer again changes nothing; it only reaches the questions of the same text that are still open.
            var same = all.Where(q => q.Status == QuestionStatus.Answered && q.Answer == answer).ToList();
            var matching = all.Except(same).ToList();
            if (matching.Count == 0)
            {
                return new AnswerResultDto(counts.Questions, counts.Findings, counts.Pages, question.EvidenceId, false);
            }

            var findingIds = matching.Select(q => q.FindingId).OfType<Guid>().Distinct().ToArray();
            var findings = await db.Findings.Where(f => findingIds.Contains(f.Id)).OrderBy(f => f.Id).ToListAsync(ct).ConfigureAwait(false);
            var proposals = await db.FixProposals.Where(p => p.FindingIds.Any(id => findingIds.Contains(id))).ToListAsync(ct).ConfigureAwait(false);
            var changing = matching.Where(q => q.Status == QuestionStatus.Answered).ToList();

            if (changing.Count > 0 && proposals.Any(p => p.Status == FixProposalStatus.Published))
            {
                throw new DomainException(ProblemCodes.QuestionAnswerLocked, 409);
            }

            if (changing.Count > 0)
            {
                await TakeBackAsync(userId, changing, findings, proposals, ct).ConfigureAwait(false);
            }

            var now = time.GetUtcNow();
            foreach (var q in matching)
            {
                q.Status = QuestionStatus.Answered;
                q.Answer = answer;
                q.AnsweredBy = userId;
                q.AnsweredAt = now;
                q.EvidenceId = null;
                q.UpdatedAt = now;
            }

            var questionOf = matching.Where(q => q.FindingId is not null).GroupBy(q => q.FindingId!.Value).ToDictionary(g => g.Key, g => g.First());
            Guid? evidenceId = null;
            var generate = new List<(Question Question, Finding Finding)>();
            if (answer == Yes)
            {
                evidenceId = await YesAsync(userId, question, matching, findings, ct).ConfigureAwait(false);
            }
            else
            {
                foreach (var finding in findings)
                {
                    var own = questionOf[finding.Id];
                    if (!await NoAsync(userId, own, finding, proposals, ct).ConfigureAwait(false))
                    {
                        generate.Add((own, finding));
                    }
                }

                if (question.Scope == QuestionScope.Finding)
                {
                    // „Nevieme doložiť“: the answer is a record of the evidence too (claim_removed), for the protocol and the list.
                    evidenceId = await AnswerEvidenceAsync(userId, question, matching, findings, EvidenceStatus.ClaimRemoved, ct).ConfigureAwait(false);
                }

                // Taken last: above the budget the whole answer is rolled back.
                await budget.TakeAsync(shop.TenantId, generate.Count, ct).ConfigureAwait(false);
            }

            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            foreach (var (own, finding) in generate)
            {
                await queue.EnqueueAsync(FixJobs.GenerateForAnswer(finding.TenantId, finding.ShopId, own.Id, finding.Id), DbSql.Transaction(db), ct).ConfigureAwait(false);
            }

            await audit.WriteAsync(new AuditEvent(AuditActions.QuestionAnswered, shop.TenantId, userId, "question", question.Id.ToString("D"), new JsonObject
            {
                ["code"] = question.Code,
                ["answer"] = answer,
                ["changed"] = changing.Count > 0,
                ["questions"] = counts.Questions,
                ["findings"] = counts.Findings,
                ["pages"] = counts.Pages,
                ["generations"] = generate.Count,
            }), ct).ConfigureAwait(false);
            return new AnswerResultDto(counts.Questions, counts.Findings, counts.Pages, evidenceId, generate.Count > 0);
        }, ct).ConfigureAwait(false);

    /// <summary>„Áno“: one piece of evidence of the kind <c>answer</c> for all the findings; the findings are kept with it and remembered.</summary>
    private async Task<Guid> YesAsync(Guid userId, Question question, IReadOnlyList<Question> matching, IReadOnlyList<Finding> findings, CancellationToken ct)
    {
        var evidenceId = await AnswerEvidenceAsync(userId, question, matching, findings, EvidenceStatus.Valid, ct).ConfigureAwait(false);
        foreach (var finding in findings)
        {
            if (finding.Status == FindingStatus.Open)
            {
                await transitions.ApplyAsync(finding, FindingStatus.NeedsAnswer, userId, ct, "answer_yes").ConfigureAwait(false);
            }

            if (finding.Status == FindingStatus.NeedsAnswer)
            {
                await transitions.ApplyAsync(finding, FindingStatus.KeptWithEvidence, userId, ct, "answer_yes").ConfigureAwait(false);
            }
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        foreach (var finding in findings.Where(f => f.Status == FindingStatus.KeptWithEvidence))
        {
            await memory.RecordAsync(finding.ShopId, finding.SegmentHash, finding.Text, Decision.KeepWithEvidence, userId, ct, evidenceId: evidenceId).ConfigureAwait(false);
        }

        return evidenceId;
    }

    /// <summary>The evidence of the kind <c>answer</c> of an answer, linked to each finding and page and to the questions.</summary>
    private async Task<Guid> AnswerEvidenceAsync(
        Guid userId, Question question, IReadOnlyList<Question> matching, IReadOnlyList<Finding> findings, EvidenceStatus status, CancellationToken ct)
    {
        var pagesOf = await PagesAsync(findings, ct).ConfigureAwait(false);
        var pageCount = pagesOf.Values.SelectMany(p => p).Distinct().Count();
        var first = findings.FirstOrDefault(f => f.Id == question.FindingId) ?? findings.FirstOrDefault();
        var evidence = new EvidenceItem
        {
            ClaimText = first?.Text ?? question.Code,
            SubjectKind = question.Scope == QuestionScope.Site || pageCount != 1 ? EvidenceSubjectKind.Group : EvidenceSubjectKind.Product,
            SubjectLabel = Label(question.Params),
            Kind = EvidenceKind.Answer,
            Source = EvidenceSource.Answer,
            Status = status,
            CreatedBy = userId,
        };
        db.EvidenceItems.Add(evidence);
        foreach (var finding in findings)
        {
            var pages = pagesOf.GetValueOrDefault(finding.Id) ?? [];
            foreach (var page in pages.Count > 0 ? pages.Select(p => (Guid?)p) : [null])
            {
                db.EvidenceLinks.Add(new EvidenceLink { EvidenceId = evidence.Id, ShopId = finding.ShopId, PageId = page, FindingId = finding.Id });
            }
        }

        foreach (var q in matching)
        {
            q.EvidenceId = evidence.Id;
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return evidence.Id;
    }

    /// <summary>
    /// „Nie“ for one finding: the ready variant <c>answer_no</c> of every open proposal is selected and the finding becomes
    /// <c>proposed</c>; a question of the whole site keeps the remedy of its rule. False when a proposal must be generated.
    /// </summary>
    private async Task<bool> NoAsync(Guid userId, Question question, Finding finding, IReadOnlyList<FixProposal> proposals, CancellationToken ct)
    {
        if (finding.Status is not (FindingStatus.NeedsAnswer or FindingStatus.Open))
        {
            // Decided otherwise meanwhile („Ponechať“, „Nejde o problém“): the answer is kept, the finding is not moved.
            return true;
        }

        if (question.Scope == QuestionScope.Site)
        {
            await transitions.ApplyAsync(finding, FindingStatus.Open, userId, ct, "answer_no").ConfigureAwait(false);
            return true;
        }

        var own = proposals.Where(p => p.FindingIds.Contains(finding.Id) && p.Status is FixProposalStatus.Proposed or FixProposalStatus.Edited).ToList();
        var ready = own.Count > 0 && own.All(p => ProposalText.Alternatives(p.Alternatives).Any(a => a.Key == ProposalText.AnswerNo && a.RecheckStatus == RecheckStatus.Ok));
        if (!ready)
        {
            await transitions.ApplyAsync(finding, FindingStatus.Open, userId, ct, "generation_pending").ConfigureAwait(false);
            return false;
        }

        foreach (var proposal in own)
        {
            proposal.SelectedAlternative = ProposalText.AnswerNo;
            proposal.EditedText = null;
            proposal.Status = FixProposalStatus.Proposed;
            proposal.UpdatedAt = time.GetUtcNow();
        }

        await transitions.ApplyAsync(finding, FindingStatus.Proposed, userId, ct, "answer_no").ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// Takes the previous answer back before a new one: the evidence of „Áno“ is deleted (its memory superseded), the variant
    /// <c>answer_no</c> unselected (an acceptance of it taken back), and the findings go back to <c>needs_answer</c>.
    /// </summary>
    private async Task TakeBackAsync(Guid userId, IReadOnlyList<Question> answered, IReadOnlyList<Finding> findings, IReadOnlyList<FixProposal> proposals, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        foreach (var evidenceId in answered.Select(q => q.EvidenceId).OfType<Guid>().Distinct())
        {
            var evidence = await db.EvidenceItems.FirstOrDefaultAsync(e => e.Id == evidenceId && e.Kind == EvidenceKind.Answer && e.DeletedAt == null, ct).ConfigureAwait(false);
            if (evidence is not null)
            {
                evidence.DeletedAt = now;
                evidence.UpdatedAt = now;
            }

            await memory.SupersedeEvidenceAsync(evidenceId, ct).ConfigureAwait(false);
        }

        foreach (var proposal in proposals.Where(p => p.SelectedAlternative == ProposalText.AnswerNo))
        {
            if (proposal.Status == FixProposalStatus.Accepted)
            {
                proposal.DecidedBy = null;
                proposal.DecidedAt = null;
                foreach (var finding in findings.Where(f => proposal.FindingIds.Contains(f.Id)))
                {
                    await memory.SupersedeAsync(finding.ShopId, finding.SegmentHash, ct, sourceProposalId: proposal.Id).ConfigureAwait(false);
                }
            }

            proposal.SelectedAlternative = null;
            proposal.Status = proposal.EditedText is null ? FixProposalStatus.Proposed : FixProposalStatus.Edited;
            proposal.UpdatedAt = now;
        }

        var back = new Dictionary<FindingStatus, FindingStatus[]>
        {
            [FindingStatus.KeptWithEvidence] = [FindingStatus.Open, FindingStatus.NeedsAnswer],
            [FindingStatus.Approved] = [FindingStatus.Proposed, FindingStatus.Open, FindingStatus.NeedsAnswer],
            [FindingStatus.Proposed] = [FindingStatus.Open, FindingStatus.NeedsAnswer],
            [FindingStatus.Open] = [FindingStatus.NeedsAnswer],
        };
        foreach (var finding in findings)
        {
            foreach (var step in back.GetValueOrDefault(finding.Status) ?? [])
            {
                await transitions.ApplyAsync(finding, step, userId, ct, "answer_changed").ConfigureAwait(false);
            }
        }
    }

    private async Task<Dictionary<Guid, List<Guid>>> PagesAsync(IReadOnlyList<Finding> findings, CancellationToken ct)
    {
        var ids = findings.Select(f => f.Id).ToArray();
        var occurrences = await db.FindingOccurrences.AsNoTracking().Where(o => ids.Contains(o.FindingId)).Select(o => new { o.FindingId, o.PageId })
            .ToListAsync(ct).ConfigureAwait(false);
        return findings.ToDictionary(
            f => f.Id,
            f => occurrences.Where(o => o.FindingId == f.Id).Select(o => o.PageId).Concat(f.PageId is { } own ? [own] : []).Distinct().ToList());
    }

    /// <summary>The subject of the evidence from the parameters of the question (the first value, e.g. „COSMOS“).</summary>
    private static string? Label(JsonDocument? parameters) =>
        parameters?.RootElement is { ValueKind: JsonValueKind.Object } root
            ? root.EnumerateObject().Select(p => p.Value.ValueKind switch
            {
                JsonValueKind.String => p.Value.GetString(),
                JsonValueKind.Number => p.Value.GetRawText(),
                _ => null,
            }).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))
            : null;
}
