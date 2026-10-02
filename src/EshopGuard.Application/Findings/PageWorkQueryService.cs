using EshopGuard.Jobs.Fixes;
using EshopGuard.Application.Contracts;
using EshopGuard.Application.Fixes;
using EshopGuard.Application.Problems;
using EshopGuard.Data;
using EshopGuard.Data.Entities.Checks;
using EshopGuard.Data.Tenancy;

namespace EshopGuard.Application.Findings;

/// <summary>
/// „Opravy“ by pages (change 11, AD 3): the items of a tab ordered by the strictest verdict, the number of findings and the
/// title, filtered by the version of the language and by a text, read by a cursor; and the counts of the tabs. The items are
/// built from the findings of the e-shop in memory (<see cref="PageItems"/>); the cursor is the sort key of the last item, so
/// a page does not shift while the merchant reads.
/// </summary>
public sealed class PageWorkQueryService(EshopGuardDb db, ShopWorkLoader loader)
{
    public async Task<CursorPage<PageWorkItemDto>> ListAsync(Guid shopId, string? tab, string? language, string? q, string? cursor, int? limit, CancellationToken ct)
    {
        tab ??= PageItems.TabToResolve;
        if (!PageItems.TabNames.Contains(tab))
        {
            throw DomainException.Validation(new ValidationResult().Add("tab", ProblemCodes.Fields.ValueNotAllowed));
        }

        var take = Cursor.Limit(limit);
        var after = Cursor.Decode(cursor);
        return await db.ExecuteInTenantTransactionAsync(async () =>
        {
            var work = await loader.LoadAsync(shopId, ct).ConfigureAwait(false);
            var items = Filter(PageItems.Build(work, visibleOnly: true), tab, language, q).ToList();
            var start = after is null ? 0 : items.FindIndex(i => IsAfter(i, after));
            if (start < 0)
            {
                start = items.Count;
            }

            var pageItems = items.Skip(start).Take(take).ToList();
            var next = start + take < items.Count && pageItems.Count > 0 ? Key(pageItems[^1]) : null;
            return new CursorPage<PageWorkItemDto>(pageItems.Select(Dto).ToList(), next, items.Count);
        }, ct).ConfigureAwait(false);
    }

    public async Task<PageTabsDto> TabsAsync(Guid shopId, string? language, CancellationToken ct) =>
        await db.ExecuteInTenantTransactionAsync(async () =>
        {
            var work = await loader.LoadAsync(shopId, ct).ConfigureAwait(false);
            return PageItems.Tabs(PageItems.InLanguage(PageItems.Build(work, visibleOnly: false), language).ToList());
        }, ct).ConfigureAwait(false);

    /// <summary>The items of a tab, a version of the language and a text in the title, address or a finding.</summary>
    public static IEnumerable<WorkItem> Filter(IEnumerable<WorkItem> items, string tab, string? language, string? q)
    {
        var result = PageItems.InLanguage(items, language).Where(i => i.InTab(tab));
        if (!string.IsNullOrWhiteSpace(q))
        {
            var text = q.Trim();
            result = result.Where(i =>
                (i.Page?.Title?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false)
                || (i.Page?.Path?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false)
                || i.Findings.Any(f => f.Text?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        return result;
    }

    public static string Key(WorkItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Cursor.Encode(item.Rank, item.Count, item.SortTitle, item.Key);
    }

    private static bool IsAfter(WorkItem item, System.Text.Json.JsonElement[] key)
    {
        if (key.Length != 4)
        {
            throw Cursor.Invalid();
        }

        try
        {
            var rank = item.Rank.CompareTo(key[0].GetInt16());
            if (rank != 0)
            {
                return rank > 0;
            }

            var count = key[1].GetInt32().CompareTo(item.Count);
            if (count != 0)
            {
                return count > 0;
            }

            var title = string.Compare(item.SortTitle, key[2].GetString(), StringComparison.Ordinal);
            return title != 0 ? title > 0 : string.CompareOrdinal(item.Key, key[3].GetString()) > 0;
        }
        catch (Exception e) when (e is InvalidOperationException or FormatException)
        {
            throw Cursor.Invalid();
        }
    }

    public static PageWorkItemDto Dto(WorkItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var counting = item.Findings.Where(f => FindingStatusMachine.Unresolved.Contains(f.Status) || f.Status == FindingStatus.Approved).ToList();
        var counts = new CheckCountsDto(counting.Count(f => f.Group == "text"), counting.Count(f => f.Group == "assess"), counting.Count(f => f.Group == "verify"));
        var ready = item.Proposals.Where(p => ShopWork.IsReady(p)).ToList();
        var work = new WorkCountsDto(
            ready.Count,
            item.Proposals.Count(p => p.Status == Data.Entities.Fixes.FixProposalStatus.Accepted),
            item.OpenQuestions.Count);
        WorkPreviewDto? preview = null;
        if (item.OpenQuestions.FirstOrDefault() is { } question)
        {
            preview = new WorkPreviewDto("question", null, null, question.Code, question.Params?.RootElement.Clone());
        }
        else if ((ready.FirstOrDefault() ?? item.Proposals.FirstOrDefault()) is { } proposal)
        {
            preview = new WorkPreviewDto("diff", proposal.OriginalText, ProposalText.Text(proposal), null, null);
        }

        var strictest = item.Current.OrderBy(f => f.Rank).FirstOrDefault();
        return new PageWorkItemDto(
            item.Kind, item.Page?.Id, item.Page?.Title, item.Page?.Path, item.Page?.Url, item.Page?.Language,
            item.Page?.Type is { } type ? FindingMapper.Text(type) : null,
            counts, work, preview, item.Status, strictest is null ? null : VerdictStrictness.Strictest(strictest.Verdicts));
    }
}
