using System.Text.Json;
using EshopGuard.Application.Contracts;
using EshopGuard.Application.Findings;
using EshopGuard.Application.Problems;
using EshopGuard.Data;
using EshopGuard.Data.Entities.Checks;
using EshopGuard.Data.Entities.Content;
using EshopGuard.Data.Entities.Fixes;
using EshopGuard.Data.Tenancy;
using EshopGuard.Jobs.Fixes;
using Microsoft.EntityFrameworkCore;

namespace EshopGuard.Application.Fixes;

/// <summary>
/// „Oprava stránky“ (change 11, AD 4): every change of a page in the order of its text — a replacement, a removed sentence, a
/// question or a change of a bulk fix — with its context from the extraction of the version (<see cref="ExtractContextReader"/>,
/// nothing copied into the database), variants, facts, verdicts by country and the recheck; the number of blocks without a
/// change; the place of the page in the queue of a tab; where the text comes from; and whether it can be published.
/// </summary>
public sealed class PageReviewService(EshopGuardDb db, ShopWorkLoader loader, ExtractContextReader extracts, PublishAvailability publish, ITenantContext tenant)
{
    public async Task<PageReviewDto> GetAsync(Guid shopId, Guid pageId, string? tab, string? language, CancellationToken ct)
    {
        tab ??= PageItems.TabToResolve;
        if (!PageItems.TabNames.Contains(tab))
        {
            throw DomainException.Validation(new ValidationResult().Add("tab", ProblemCodes.Fields.ValueNotAllowed));
        }

        return await db.ExecuteInTenantTransactionAsync(async () =>
        {
            var work = await loader.LoadAsync(shopId, ct).ConfigureAwait(false);
            var page = work.Pages.GetValueOrDefault(pageId)
                ?? await db.Pages.AsNoTracking().Where(p => p.ShopId == shopId && p.Id == pageId)
                    .Select(p => new WorkPage(p.Id, p.Title, p.Path, p.Url, p.Language, p.PageType, p.Source, p.ExternalId, p.CurrentVersionId))
                    .FirstOrDefaultAsync(ct).ConfigureAwait(false)
                ?? throw new DomainException(ProblemCodes.PageNotFound, 404);
            var text = await TextAsync(work, page, ct).ConfigureAwait(false);
            var changes = Changes(work, page, text);
            var items = PageWorkQueryService.Filter(PageItems.Build(work, visibleOnly: true), tab, language, null).ToList();
            var accepted = work.LiveProposals.Count(p => p.PageId == pageId && p.Status == FixProposalStatus.Accepted);
            var groupIds = changes.Select(c => c.GroupId).OfType<Guid>().ToHashSet();
            var changedBlocks = changes.Select(c => c.BlockIndex).OfType<int>().Distinct().Count();
            var templateHashes = work.TemplateHashes;
            return new PageReviewDto(
                new ReviewPageDto(page.Id, page.Title, page.Url, page.Language, Source(work, page)),
                Position(items, page.Id, tab),
                changes.Select((c, i) => c.Dto with { Index = i + 1 }).ToList(),
                text is null ? 0 : Math.Max(0, text.Blocks.Count - changedBlocks),
                work.Findings.Any(f => f.SegmentHash is { } hash && templateHashes.Contains(hash) && (work.PagesOf.GetValueOrDefault(f.Id) ?? []).Contains(page.Id)),
                new ReviewSummaryDto(accepted, changes.Count, work.Groups.Count(g => groupIds.Contains(g.Id) && GroupValues.Missing(g).Count > 0)),
                await publish.ForAsync(work.Shop, accepted, ct).ConfigureAwait(false));
        }, ct).ConfigureAwait(false);
    }

    private sealed record Change(int? BlockIndex, int Order, Guid? GroupId, ChangeDto Dto);

    private async Task<PageText?> TextAsync(ShopWork work, WorkPage page, CancellationToken ct)
    {
        if (page.CurrentVersionId is not { } versionId || tenant.TenantId is not { } tenantId)
        {
            return null;
        }

        var key = await db.PageVersions.AsNoTracking().Where(v => v.ShopId == work.Shop.Id && v.Id == versionId).Select(v => v.ExtractBlobKey)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        return await extracts.ReadAsync(tenantId, work.Shop.Id, key, ct).ConfigureAwait(false);
    }

    private static List<Change> Changes(ShopWork work, WorkPage page, PageText? text)
    {
        var findings = work.Findings.Where(f => work.IsVisible(f.Id)).ToDictionary(f => f.Id);
        var openQuestions = work.Questions.Where(q => q.Status == QuestionStatus.Open && q.FindingId is not null).ToLookup(q => q.FindingId!.Value);
        var proposals = work.LiveProposals.Where(p => p.PageId == page.Id && p.FindingIds.Any(findings.ContainsKey)).ToList();
        var changes = new List<Change>();
        var order = 0;
        foreach (var proposal in proposals)
        {
            var linked = proposal.FindingIds.Select(id => findings.GetValueOrDefault(id)).OfType<WorkFinding>().OrderBy(f => f.Rank).ToList();
            var strictest = linked[0];
            var question = linked.SelectMany(f => openQuestions[f.Id]).FirstOrDefault();
            var group = proposal.GroupId is { } groupId ? work.Groups.FirstOrDefault(g => g.Id == groupId) : null;
            var current = ProposalText.Text(proposal);
            var kind = question is not null ? "question" : group is not null ? "group" : current.Trim().Length == 0 ? "remove" : "replace";
            var block = text?.Locate(proposal.OriginalText, proposal.BlockIndex) ?? proposal.BlockIndex;
            var (before, after) = text?.Around(block) ?? (null, null);
            changes.Add(new Change(block, order++, group?.Id, new ChangeDto(
                0, kind, proposal.Id, question?.Id, group?.Id, proposal.FindingIds, strictest.RuleId, strictest.RuleSetId,
                VerdictStrictness.Verdicts(strictest.Verdicts), VerdictStrictness.Strictest(strictest.Verdicts),
                proposal.OriginalText, proposal.ProposedText, current, before, after,
                ProposalMapper.Alternatives(proposal), proposal.SelectedAlternative, proposal.EditedText, ProposalMapper.Placeholders(proposal),
                group is null ? null : Group(group), ProposalMapper.Recheck(proposal), FindingMapper.Text(proposal.Status), proposal.Version,
                question?.Code, question?.Params?.RootElement.Clone())));
        }

        // Findings of the page with an open question and no proposal yet are changes of their own.
        var withProposal = proposals.SelectMany(p => p.FindingIds).ToHashSet();
        foreach (var finding in findings.Values.Where(f => !withProposal.Contains(f.Id) && (work.PagesOf.GetValueOrDefault(f.Id) ?? []).Contains(page.Id)))
        {
            var question = openQuestions[finding.Id].FirstOrDefault();
            if (question is null)
            {
                continue;
            }

            var block = text?.Locate(finding.Text);
            var (before, after) = text?.Around(block) ?? (null, null);
            changes.Add(new Change(block, order++, null, new ChangeDto(
                0, "question", null, question.Id, null, [finding.Id], finding.RuleId, finding.RuleSetId,
                VerdictStrictness.Verdicts(finding.Verdicts), VerdictStrictness.Strictest(finding.Verdicts),
                finding.Text ?? "", null, null, before, after, [], null, null, [], null, new RecheckDto("pending", null, null),
                FindingStatusMachine.Text(finding.Status), null, question.Code, question.Params?.RootElement.Clone())));
        }

        return changes.OrderBy(c => c.BlockIndex ?? int.MaxValue).ThenBy(c => c.Order).ToList();
    }

    public static ChangeGroupDto Group(FixGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        var individual = Individual(group).Count;
        return new ChangeGroupDto(group.Id, group.PageCount, Math.Max(0, group.PageCount - individual - group.ExcludedPageIds.Length), individual, FindingMapper.Text(group.Status));
    }

    /// <summary>The pages of a group where the fix does not fit (<c>fix_groups.fit.individual</c>).</summary>
    public static IReadOnlyList<(Guid PageId, string ReasonCode, JsonElement? Params)> Individual(FixGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        return group.Fit?.RootElement is { ValueKind: JsonValueKind.Object } root && root.TryGetProperty("individual", out var list) && list.ValueKind == JsonValueKind.Array
            ? list.EnumerateArray()
                .Where(e => e.TryGetProperty("page_id", out var id) && id.TryGetGuid(out _))
                .Select(e => (e.GetProperty("page_id").GetGuid(),
                    e.TryGetProperty("reason_code", out var reason) && reason.ValueKind == JsonValueKind.String ? reason.GetString()! : "not_fit",
                    e.TryGetProperty("params", out var p) ? p.Clone() : (JsonElement?)null))
                .ToList()
            : [];
    }

    private static PageSourceDto Source(ShopWork work, WorkPage page) => page.Source switch
    {
        PageSource.Connector => new PageSourceDto("connector", FindingMapper.Text(work.Shop.Platform), page.ExternalId),
        PageSource.Feed => new PageSourceDto("feed", null, page.ExternalId),
        _ => new PageSourceDto("crawl", null, null),
    };

    private static ReviewPositionDto Position(IReadOnlyList<WorkItem> items, Guid pageId, string tab)
    {
        var index = items.ToList().FindIndex(i => i.Page?.Id == pageId);
        if (index < 0)
        {
            return new ReviewPositionDto(tab, 0, items.Count, null, null, null);
        }

        var previous = items.Take(index).LastOrDefault(i => i.Page is not null);
        var next = items.Skip(index + 1).FirstOrDefault(i => i.Page is not null);
        return new ReviewPositionDto(tab, index + 1, items.Count, previous?.Page?.Id, next?.Page?.Id, next?.Page?.Title);
    }
}
