using System.Text.Json;
using EshopGuard.Application.Contracts;
using EshopGuard.Application.Fixes;
using EshopGuard.Application.Problems;
using EshopGuard.Data;
using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Checks;
using EshopGuard.Data.Entities.Fixes;
using EshopGuard.Data.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace EshopGuard.Application.Findings;

/// <summary>Filters of the list of findings (change 11, design A).</summary>
public sealed record FindingFilter(
    string? Checkability, string? Status, string? Module, string? Language, string? Jurisdiction, string? Q, string? GroupBy)
{
    public static readonly string[] Groups = ["text", "assess", "verify"];

    /// <summary>Checks the values; a value outside its set is <c>400 validation.failed</c> with <c>value.not_allowed</c>.</summary>
    public IReadOnlyList<FindingStatus>? Validate()
    {
        var validation = new ValidationResult();
        if (Checkability is not null && !Groups.Contains(Checkability))
        {
            validation.Add("checkability", ProblemCodes.Fields.ValueNotAllowed);
        }

        List<FindingStatus>? statuses = null;
        if (!string.IsNullOrEmpty(Status))
        {
            statuses = [];
            foreach (var part in Status.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (part == "unfixed")
                {
                    statuses.AddRange(Enum.GetValues<FindingStatus>().Except(FindingStatusMachine.Fixed));
                }
                else if (part == "fixed")
                {
                    statuses.AddRange(FindingStatusMachine.Fixed);
                }
                else if (SnakeCaseEnumConverter<FindingStatus>.AllTexts.Contains(part))
                {
                    statuses.Add(SnakeCaseEnumConverter<FindingStatus>.FromText(part));
                }
                else
                {
                    validation.Add("status", ProblemCodes.Fields.ValueNotAllowed);
                }
            }
        }

        if (Jurisdiction is not null && (Jurisdiction.Length != 2 || !Jurisdiction.All(char.IsAsciiLetterLower)))
        {
            validation.Add("jurisdiction", ProblemCodes.Fields.ValueNotAllowed);
        }

        if (GroupBy is not null && GroupBy != "page")
        {
            validation.Add("groupBy", ProblemCodes.Fields.ValueNotAllowed);
        }

        validation.ThrowIfInvalid();
        return statuses;
    }
}

/// <summary>
/// „Nálezy“ (change 11): the findings of the e-shop filtered by the group of their strictest verdict, state, topic, version of
/// the language, country and text, ordered by the strictest verdict (or grouped by page), read by a cursor; the tabs; and
/// the detail of a finding with its context on the page, pages, questions, proposals, evidence and history.
/// </summary>
public sealed class FindingQueryService(
    EshopGuardDb db, ShopWorkLoader loader, AnswerPropagation propagation, ExtractContextReader extracts, ITenantContext tenant)
{
    public async Task<CursorPage<FindingListItemDto>> ListAsync(Guid shopId, FindingFilter filter, string? cursor, int? limit, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var statuses = filter.Validate();
        var take = Cursor.Limit(limit);
        var after = Cursor.Decode(cursor);
        return await db.ExecuteInTenantTransactionAsync(async () =>
        {
            var work = await loader.LoadAsync(shopId, ct).ConfigureAwait(false);
            var rows = Rows(work, filter, statuses).ToList();
            var start = after is null ? 0 : rows.FindIndex(r => SortKeys.Compare(r.Key, after) > 0);
            if (start < 0)
            {
                start = rows.Count;
            }

            var slice = rows.Skip(start).Take(take).ToList();
            var next = start + take < rows.Count && slice.Count > 0 ? Cursor.Encode(slice[^1].Key) : null;
            return new CursorPage<FindingListItemDto>(slice.Select(r => r.Item).ToList(), next, rows.Count);
        }, ct).ConfigureAwait(false);
    }

    public async Task<FindingTabsDto> TabsAsync(Guid shopId, string? language, CancellationToken ct) =>
        await db.ExecuteInTenantTransactionAsync(async () =>
        {
            var work = await loader.LoadAsync(shopId, ct).ConfigureAwait(false);
            var findings = work.Findings.Where(f => InLanguage(work, f, language)).ToList();
            return new FindingTabsDto(
                findings.Count(f => f.Group == "text"),
                findings.Count(f => f.Group == "assess"),
                findings.Count(f => f.Group == "verify"),
                findings.Count(f => FindingStatusMachine.Fixed.Contains(f.Status)));
        }, ct).ConfigureAwait(false);

    public async Task<FindingDetailDto> DetailAsync(Guid shopId, Guid findingId, CancellationToken ct) =>
        await db.ExecuteInTenantTransactionAsync(() => DetailInTransactionAsync(shopId, findingId, ct), ct).ConfigureAwait(false);

    /// <summary>The detail in the open transaction of the tenant (after a decision).</summary>
    public async Task<FindingDetailDto> DetailInTransactionAsync(Guid shopId, Guid findingId, CancellationToken ct)
    {
        var work = await loader.LoadAsync(shopId, ct).ConfigureAwait(false);
        var finding = work.Findings.FirstOrDefault(f => f.Id == findingId && work.IsVisible(f.Id))
            ?? throw new DomainException(ProblemCodes.FindingNotFound, 404);
        var pages = (work.PagesOf.GetValueOrDefault(finding.Id) ?? []).Select(p => work.Pages.GetValueOrDefault(p)).OfType<WorkPage>()
            .OrderBy(p => p.Title ?? p.Url, StringComparer.Ordinal).ToList();
        var primary = finding.PageId is { } own ? work.Pages.GetValueOrDefault(own) : pages.FirstOrDefault();
        var proposals = work.LiveProposals.Where(p => p.FindingIds.Contains(finding.Id)).ToList();
        var questions = work.Questions.Where(q => q.FindingId == finding.Id).ToList();
        var item = FindingMapper.Item(finding, primary, proposals.FirstOrDefault(p => primary is null || p.PageId == primary.Id) ?? proposals.FirstOrDefault(),
            questions.FirstOrDefault(q => q.Status == QuestionStatus.Open)?.Id ?? questions.FirstOrDefault()?.Id);

        var names = await NamesAsync(questions.Select(q => q.AnsweredBy).OfType<Guid>(), ct).ConfigureAwait(false);
        var questionDtos = new List<QuestionDto>();
        foreach (var question in questions)
        {
            var applies = await propagation.AppliesToAsync(question, ct).ConfigureAwait(false);
            questionDtos.Add(FindingMapper.Question(question, finding, primary, applies, question.AnsweredBy is { } by ? names.GetValueOrDefault(by) : null));
        }

        var evidence = await db.EvidenceLinks.AsNoTracking().Where(l => l.FindingId == finding.Id)
            .Join(db.EvidenceItems.AsNoTracking().Where(e => e.DeletedAt == null), l => l.EvidenceId, e => e.Id, (l, e) => e)
            .Distinct().ToListAsync(ct).ConfigureAwait(false);
        var history = await HistoryAsync(finding.Id, ct).ConfigureAwait(false);

        var (before, after) = await ContextAsync(work, finding, primary, proposals, ct).ConfigureAwait(false);
        return new FindingDetailDto(
            item, before, after,
            pages.Select(p => FindingMapper.Page(p)!).ToList(),
            questionDtos,
            proposals.Select(p => new FindingProposalDto(p.Id, p.PageId, FindingMapper.Text(p.Status), FindingMapper.Text(ProposalText.Recheck(p)), p.OriginalText, ProposalText.Text(p))).ToList(),
            evidence.Select(e => new FindingEvidenceDto(e.Id, FindingMapper.Text(e.Kind), e.Title, FindingMapper.Text(e.Status), e.ValidUntil)).ToList(),
            history);
    }

    /// <summary>The findings of the list with their sort keys, in order.</summary>
    internal static IEnumerable<(object[] Key, FindingListItemDto Item)> Rows(ShopWork work, FindingFilter filter, IReadOnlyList<FindingStatus>? statuses)
    {
        var proposals = work.LiveProposals.ToList();
        var openQuestions = work.Questions.Where(q => q.Status == QuestionStatus.Open).ToList();
        var rows = new List<(object[] Key, FindingListItemDto Item)>();
        foreach (var finding in work.Findings.Where(f => work.IsVisible(f.Id)))
        {
            if ((filter.Checkability is { } group && finding.Group != group)
                || (statuses is not null && !statuses.Contains(finding.Status))
                || (filter.Module is { } module && finding.Module != module)
                || !InLanguage(work, finding, filter.Language)
                || (filter.Jurisdiction is { } jurisdiction && !VerdictStrictness.Verdicts(finding.Verdicts).Any(v => v.Jurisdiction == jurisdiction))
                || (!string.IsNullOrWhiteSpace(filter.Q) && !(finding.Text?.Contains(filter.Q.Trim(), StringComparison.OrdinalIgnoreCase) ?? false)))
            {
                continue;
            }

            var pages = (work.PagesOf.GetValueOrDefault(finding.Id) ?? []).Select(p => work.Pages.GetValueOrDefault(p)).OfType<WorkPage>()
                .Where(p => string.IsNullOrEmpty(filter.Language) || p.Language == filter.Language).ToList();
            var primary = finding.PageId is { } own && work.Pages.GetValueOrDefault(own) is { } ownPage && pages.Contains(ownPage) ? ownPage : pages.OrderBy(p => p.Title, StringComparer.Ordinal).FirstOrDefault();
            var proposal = proposals.FirstOrDefault(p => p.FindingIds.Contains(finding.Id) && (primary is null || p.PageId == primary.Id))
                ?? proposals.FirstOrDefault(p => p.FindingIds.Contains(finding.Id));
            var question = openQuestions.FirstOrDefault(q => q.FindingId == finding.Id);
            var item = FindingMapper.Item(finding, primary, proposal, question?.Id);
            object[] key = filter.GroupBy == "page"
                ? [primary?.Title ?? primary?.Url ?? "", primary?.Id.ToString("N") ?? "", (int)finding.Rank, -finding.Occurrences, finding.Text ?? "", finding.Id.ToString("N")]
                : [(int)finding.Rank, -finding.Occurrences, finding.Text ?? "", finding.Id.ToString("N")];
            rows.Add((key, item));
        }

        rows.Sort((a, b) => SortKeys.Compare(a.Key, b.Key));
        return rows;
    }

    private static bool InLanguage(ShopWork work, WorkFinding finding, string? language) =>
        string.IsNullOrEmpty(language) || finding.Scope == FindingScope.Site
        || (work.PagesOf.GetValueOrDefault(finding.Id) ?? []).Any(p => work.Pages.GetValueOrDefault(p)?.Language == language);

    private async Task<(string? Before, string? After)> ContextAsync(ShopWork work, WorkFinding finding, WorkPage? page, IReadOnlyList<FixProposal> proposals, CancellationToken ct)
    {
        if (page?.CurrentVersionId is not { } versionId || tenant.TenantId is not { } tenantId)
        {
            return (null, null);
        }

        var key = await db.PageVersions.AsNoTracking().Where(v => v.ShopId == work.Shop.Id && v.Id == versionId).Select(v => v.ExtractBlobKey)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        var text = await extracts.ReadAsync(tenantId, work.Shop.Id, key, ct).ConfigureAwait(false);
        if (text is null)
        {
            return (null, null);
        }

        var hint = proposals.FirstOrDefault(p => p.PageId == page.Id && p.BlockIndex is not null)?.BlockIndex;
        return text.Around(text.Locate(finding.Text, hint));
    }

    private async Task<IReadOnlyList<FindingHistoryDto>> HistoryAsync(Guid findingId, CancellationToken ct)
    {
        var id = findingId.ToString("D");
        var entries = await db.AuditLog.AsNoTracking().Where(a => a.EntityType == "finding" && a.EntityId == id)
            .OrderBy(a => a.At).Select(a => new { a.At, a.Action, a.ActorUserId, a.Data }).ToListAsync(ct).ConfigureAwait(false);
        var names = await NamesAsync(entries.Select(e => e.ActorUserId).OfType<Guid>(), ct).ConfigureAwait(false);
        return entries.Select(e => new FindingHistoryDto(
                e.At, e.Action, Data(e.Data, "from"), Data(e.Data, "to"), Data(e.Data, "reasonCode"),
                e.ActorUserId is { } user ? new FindingActorDto(user, names.GetValueOrDefault(user)) : null))
            .ToList();
    }

    private async Task<Dictionary<Guid, string?>> NamesAsync(IEnumerable<Guid> userIds, CancellationToken ct)
    {
        var ids = userIds.Distinct().ToList();
        return ids.Count == 0
            ? []
            : await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct).ConfigureAwait(false);
    }

    private static string? Data(JsonDocument? data, string name) =>
        data?.RootElement is { ValueKind: JsonValueKind.Object } root && root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}

/// <summary>Comparison of sort keys made of numbers and texts (cursors of the lists).</summary>
public static class SortKeys
{
    public static int Compare(object[] a, object[] b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        for (var i = 0; i < Math.Min(a.Length, b.Length); i++)
        {
            var c = (a[i], b[i]) switch
            {
                (int x, int y) => x.CompareTo(y),
                (string x, string y) => string.CompareOrdinal(x, y),
                _ => 0,
            };
            if (c != 0)
            {
                return c;
            }
        }

        return a.Length.CompareTo(b.Length);
    }

    /// <summary>A key of a cursor compared with the key of a row.</summary>
    public static int Compare(object[] row, JsonElement[] cursor)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(cursor);
        if (cursor.Length != row.Length)
        {
            throw Cursor.Invalid();
        }

        var key = new object[cursor.Length];
        for (var i = 0; i < cursor.Length; i++)
        {
            key[i] = (row[i], cursor[i].ValueKind) switch
            {
                (int, JsonValueKind.Number) when cursor[i].TryGetInt32(out var n) => n,
                (string, JsonValueKind.String) => cursor[i].GetString()!,
                _ => throw Cursor.Invalid(),
            };
        }

        return Compare(row, key);
    }
}
