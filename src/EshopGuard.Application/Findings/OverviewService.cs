using System.Text.Json.Nodes;
using EshopGuard.Application.Contracts;
using EshopGuard.Application.Fixes;
using EshopGuard.Application.Shops;
using EshopGuard.Data;
using EshopGuard.Data.Entities.Checks;
using EshopGuard.Data.Entities.Fixes;
using EshopGuard.Data.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace EshopGuard.Application.Findings;

/// <summary>
/// „Prehľad“ of an e-shop (change 11, design A): the last finished check, the counts of findings by group, the tabs of the
/// pages, at most 5 items to solve first (the first of <c>to_resolve</c>), at most 3 quick answers with the number of open
/// questions, and the counts of the side menu. Monitoring comes with change 16 (<c>null</c>).
/// </summary>
public sealed class OverviewService(EshopGuardDb db, ShopWorkLoader loader, AnswerPropagation propagation)
{
    private static readonly RunStatus[] Checked = [RunStatus.Finished, RunStatus.Partial];

    public async Task<OverviewDto> GetAsync(Guid shopId, CancellationToken ct) =>
        await db.ExecuteInTenantTransactionAsync(async () =>
        {
            var work = await loader.LoadAsync(shopId, ct).ConfigureAwait(false);
            var run = await db.Runs.AsNoTracking().Where(r => r.ShopId == shopId && Checked.Contains(r.Status))
                .OrderByDescending(r => r.FinishedAt).FirstOrDefaultAsync(ct).ConfigureAwait(false);
            var all = PageItems.Build(work, visibleOnly: false);
            var visible = PageItems.Build(work, visibleOnly: true);
            var tabs = PageItems.Tabs(all);

            var openQuestions = visible.SelectMany(i => i.OpenQuestions).DistinctBy(q => q.Id).ToList();
            var quick = new List<QuestionDto>();
            foreach (var question in openQuestions.Take(3))
            {
                var finding = question.FindingId is { } id ? work.Findings.FirstOrDefault(f => f.Id == id) : null;
                var page = finding is null ? null : (work.PagesOf.GetValueOrDefault(finding.Id) ?? []).Select(p => work.Pages.GetValueOrDefault(p)).OfType<WorkPage>().FirstOrDefault();
                quick.Add(FindingMapper.Question(question, finding, page, await propagation.AppliesToAsync(question, ct).ConfigureAwait(false), null));
            }

            var evidence = await db.EvidenceItems.AsNoTracking()
                .CountAsync(e => e.DeletedAt == null && (e.Status == EvidenceStatus.Expiring || e.Status == EvidenceStatus.AwaitingAnswer), ct).ConfigureAwait(false);
            return new OverviewDto(
                new OverviewShopDto(work.Shop.Id, work.Shop.Domain),
                run is null ? null : LastRun(run),
                new FindingCountsByGroupDto(
                    work.Findings.Count,
                    work.Findings.Count(f => f.Group == "text"),
                    work.Findings.Count(f => f.Group == "assess"),
                    work.Findings.Count(f => f.Group == "verify")),
                tabs,
                visible.Where(i => i.ToResolve).Take(5).Select(PageWorkQueryService.Dto).ToList(),
                new QuickQuestionsDto(work.Questions.Count(q => q.Status == QuestionStatus.Open), quick),
                new BadgesDto(tabs.ToApprove, evidence, null),
                null);
        }, ct).ConfigureAwait(false);

    private static OverviewRunDto LastRun(Run run)
    {
        var stats = run.Stats is null ? null : JsonNode.Parse(run.Stats.RootElement.GetRawText()) as JsonObject;
        var pagesChecked = stats?["pages_checked"] is JsonValue value && value.TryGetValue<int>(out var pages) ? pages : 0;
        return new OverviewRunDto(
            run.Id, FindingMapper.Text(run.Kind), FindingMapper.Text(run.Status), run.FinishedAt, pagesChecked,
            SampleResultReader.NotChecked(stats?["unchecked"] as JsonObject), run.Jurisdictions);
    }
}
