using EshopGuard.Application.Contracts;
using EshopGuard.Data;
using EshopGuard.Data.Entities.Checks;
using Microsoft.EntityFrameworkCore;

namespace EshopGuard.Application.Fixes;

/// <summary>
/// Where an answer applies (change 11, AD 6 and K rozhodnutí 9): every question with the same code about the same text
/// (<c>findings.segment_hash</c>) in every e-shop of the tenant; a question of the whole site only in its e-shop; a question
/// without a text only itself. The open ones take a new answer; when an answer changes, the questions answered with it
/// change too. RLS keeps it inside the tenant: another tenant's question with the same text is never touched.
/// </summary>
public sealed class AnswerPropagation(EshopGuardDb db)
{
    /// <summary>The questions an answer to <paramref name="question"/> applies to (itself included).</summary>
    public async Task<List<Question>> MatchingAsync(Question question, bool includeAnswered, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(question);
        var hash = await HashAsync(question, ct).ConfigureAwait(false);
        var statuses = includeAnswered ? new[] { QuestionStatus.Open, QuestionStatus.Answered } : [QuestionStatus.Open];
        IQueryable<Question> query = db.Questions.Where(q => q.Code == question.Code && statuses.Contains(q.Status));
        if (question.Scope == QuestionScope.Site)
        {
            query = query.Where(q => q.Scope == QuestionScope.Site && q.ShopId == question.ShopId);
        }
        else if (hash is { } segment)
        {
            query = query.Where(q => q.Scope == QuestionScope.Finding
                && db.Findings.Any(f => f.Id == q.FindingId && f.SegmentHash == segment));
        }
        else
        {
            query = query.Where(q => q.Id == question.Id);
        }

        var matching = await query.ToListAsync(ct).ConfigureAwait(false);
        if (matching.All(q => q.Id != question.Id))
        {
            matching.Add(question);
        }

        return matching;
    }

    /// <summary>How many questions, findings and pages an answer applies to (shown before and after answering).</summary>
    public async Task<AppliesToDto> AppliesToAsync(Question question, CancellationToken ct)
    {
        var matching = await MatchingAsync(question, includeAnswered: question.Status == QuestionStatus.Answered, ct).ConfigureAwait(false);
        return await CountAsync(matching, ct).ConfigureAwait(false);
    }

    /// <summary><see cref="AppliesToAsync(Question, CancellationToken)"/> for several questions.</summary>
    public async Task<Dictionary<Guid, AppliesToDto>> AppliesToAsync(IReadOnlyList<Question> questions, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(questions);
        var result = new Dictionary<Guid, AppliesToDto>();
        foreach (var question in questions)
        {
            result[question.Id] = await AppliesToAsync(question, ct).ConfigureAwait(false);
        }

        return result;
    }

    /// <summary>Distinct findings and pages of the questions.</summary>
    public async Task<AppliesToDto> CountAsync(IReadOnlyList<Question> questions, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(questions);
        var findingIds = questions.Select(q => q.FindingId).OfType<Guid>().Distinct().ToList();
        var occurrencePages = await db.FindingOccurrences.Where(o => findingIds.Contains(o.FindingId)).Select(o => o.PageId).Distinct()
            .ToListAsync(ct).ConfigureAwait(false);
        var ownPages = await db.Findings.Where(f => findingIds.Contains(f.Id) && f.PageId != null).Select(f => f.PageId!.Value)
            .ToListAsync(ct).ConfigureAwait(false);
        var pages = occurrencePages.Concat(ownPages).Distinct().Count();
        return new AppliesToDto(questions.Count, findingIds.Count, pages);
    }

    private async Task<long?> HashAsync(Question question, CancellationToken ct) =>
        question.FindingId is { } findingId
            ? await db.Findings.Where(f => f.Id == findingId).Select(f => f.SegmentHash).FirstOrDefaultAsync(ct).ConfigureAwait(false)
            : null;
}
