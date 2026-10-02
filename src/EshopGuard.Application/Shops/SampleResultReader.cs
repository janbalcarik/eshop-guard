using System.Text.Json;
using System.Text.Json.Nodes;
using EshopGuard.Application.Contracts;
using EshopGuard.Application.Problems;
using EshopGuard.Data;
using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Checks;
using EshopGuard.Data.Entities.Fixes;
using EshopGuard.Data.Tenancy;
using EshopGuard.Jobs.Runs;
using Microsoft.EntityFrameworkCore;

namespace EshopGuard.Application.Shops;

/// <summary>
/// <c>GET …/sample</c> (change 10, AD 4): the newest run <c>free_sample</c> of the e-shop with its progress and place in the
/// queue (<see cref="IRunQueueEstimator"/>), and after its end the summary written by change 8 (<c>runs.stats</c>): what was
/// checked and what not (every reason, also for <c>partial</c>), the counts of findings, the 5 findings of
/// <c>top_finding_ids</c> (ordered by the strictest verdict, change 6) and the example fix when it passed the recheck.
/// </summary>
public sealed class SampleResultReader(EshopGuardDb db, ShopReader reader, IRunReadModel runs, IRunQueueEstimator queue)
{
    private static readonly RunStatus[] Ended = [RunStatus.Finished, RunStatus.Partial, RunStatus.Failed, RunStatus.Canceled];

    public async Task<SampleDto> GetAsync(Guid shopId, CancellationToken ct)
    {
        await db.ExecuteInTenantTransactionAsync(() => reader.ReadAsync(shopId, ct), ct).ConfigureAwait(false);
        var run = await runs.GetLatestAsync(shopId, RunKind.FreeSample, ct).ConfigureAwait(false)
            ?? throw new DomainException(ProblemCodes.SampleNotStarted, 404);
        var ended = Ended.Contains(run.Status);
        var estimate = ended ? new RunQueueEstimate(null, null) : await queue.EstimateAsync(run.Id, ct).ConfigureAwait(false);
        var result = run.Status is RunStatus.Finished or RunStatus.Partial ? await ResultAsync(run, ct).ConfigureAwait(false) : null;
        return new SampleDto(
            run.Id,
            SnakeCaseEnumConverter<RunStatus>.ToText(run.Status),
            run.Error,
            run.StartedAt,
            run.FinishedAt,
            new SampleProgressDto(run.Progress.PagesPlanned, run.Progress.PagesFetched, run.Progress.PagesProcessed),
            new SampleQueueDto(estimate.Position, estimate.EstimatedFinishAt),
            result);
    }

    /// <summary>The groups of the reasons of 3c; every other reason counts into <c>other</c> and every reason stays in <c>byReason</c>.</summary>
    public static NotCheckedDto NotChecked(JsonObject? counts)
    {
        var byReason = Counts(counts);
        int Of(params string[] reasons) => reasons.Sum(r => byReason.GetValueOrDefault(r));
        string[] grouped = ["robots_blocked", "not_loaded", "too_large", "failed", "ssrf_blocked", "offsite_redirect"];
        return new NotCheckedDto(
            Of("robots_blocked"),
            Of("not_loaded"),
            Of("too_large"),
            Of("failed", "ssrf_blocked", "offsite_redirect"),
            byReason.Where(r => !grouped.Contains(r.Key)).Sum(r => r.Value),
            byReason);
    }

    private async Task<SampleResultDto> ResultAsync(RunView run, CancellationToken ct)
    {
        var sample = run.Sample ?? [];
        var bySeverity = Counts(sample["findings_by_severity"] as JsonObject ?? run.FindingsBySeverity);
        var byCheckability = Counts(sample["findings_by_checkability"] as JsonObject);
        var topIds = (sample["top_finding_ids"] as JsonArray ?? []).Select(n => Guid.TryParse((string?)n, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty).Take(5).ToList();
        var exampleId = Guid.TryParse((string?)sample["example_fix_proposal_id"], out var proposal) ? proposal : (Guid?)null;

        return await db.ExecuteInTenantTransactionAsync(async () =>
        {
            var findings = await db.Findings.AsNoTracking().Where(f => topIds.Contains(f.Id)).ToListAsync(ct).ConfigureAwait(false);
            var pageIds = findings.Select(f => f.PageId).OfType<Guid>().Distinct().ToList();
            var pages = await db.Pages.AsNoTracking().Where(p => pageIds.Contains(p.Id))
                .Select(p => new SamplePageDto(p.Id, p.Title, p.Url, p.Language)).ToDictionaryAsync(p => p.Id, ct).ConfigureAwait(false);
            var top = topIds.Select(id => findings.FirstOrDefault(f => f.Id == id)).OfType<Finding>()
                .Select(f => ToDto(f, f.PageId is { } page ? pages.GetValueOrDefault(page) : null)).ToList();

            ExampleFixDto? example = null;
            if (exampleId is { } id)
            {
                example = await db.FixProposals.AsNoTracking()
                    .Where(p => p.Id == id && p.RecheckStatus == RecheckStatus.Ok)
                    .Select(p => new ExampleFixDto(p.Id, p.FindingIds.Length > 0 ? p.FindingIds[0] : null, p.OriginalText, p.ProposedText, "ok"))
                    .FirstOrDefaultAsync(ct).ConfigureAwait(false);
            }

            return new SampleResultDto(
                run.Progress.PagesPlanned,
                run.PagesChecked ?? 0,
                NotChecked(run.Unchecked),
                (sample["pages_checked_by_language"] as JsonObject ?? []).Select(p => p.Key).Where(l => l != "und").Order(StringComparer.Ordinal).ToList(),
                (sample["jurisdictions"] as JsonArray ?? []).Select(n => (string?)n).OfType<string>().ToList(),
                new FindingCountsDto(bySeverity.Values.Sum(), byCheckability, bySeverity),
                top,
                example,
                example is null ? (string?)sample["example_fix_missing_reason"] ?? (exampleId is null ? null : "recheck_not_ok") : null);
        }, ct).ConfigureAwait(false);
    }

    private static SampleFindingDto ToDto(Finding finding, SamplePageDto? page)
    {
        var verdicts = Findings.VerdictStrictness.Verdicts(finding.Verdicts);
        var checkability = SnakeCaseEnumConverter<Checkability>.ToText(finding.Checkability);
        var band = finding.Band == FindingBand.High ? "high" : "review";
        var strictest = verdicts.FirstOrDefault(v => v.Checkability == checkability && v.Severity == finding.Severity && v.Band == band)
            ?? new VerdictDto(verdicts.FirstOrDefault()?.Jurisdiction ?? "", checkability, finding.Severity, band, finding.LegalRefs?.RootElement.Clone());
        return new SampleFindingDto(
            finding.Id, finding.RuleSetId, finding.RuleId, finding.Module, SnakeCaseEnumConverter<FindingScope>.ToText(finding.Scope),
            strictest, verdicts, finding.Text, page, finding.Params?.RootElement.Clone());
    }

    private static Dictionary<string, int> Counts(JsonObject? counts) =>
        (counts ?? []).Where(p => p.Value is JsonValue v && v.TryGetValue<int>(out _))
            .ToDictionary(p => p.Key, p => p.Value!.GetValue<int>(), StringComparer.Ordinal);
}
