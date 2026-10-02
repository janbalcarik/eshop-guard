using System.Text.Json.Nodes;
using EshopGuard.Application.Contracts;
using EshopGuard.Application.Findings;
using EshopGuard.Application.Problems;
using EshopGuard.Application.Shops;
using EshopGuard.Data;
using EshopGuard.Data.Tenancy;
using EshopGuard.Data.Entities.Checks;
using EshopGuard.Jobs.Runs;
using Microsoft.EntityFrameworkCore;

namespace EshopGuard.Application.Runs;

/// <summary>
/// The runs of the e-shops of the tenant for the API (change 11, task 11.1): the list (newest first), the detail with the
/// position in the queue and the estimated end of change 8 while the run is not over, and its events. Everything under RLS:
/// a run of another tenant is <c>404 run.not_found</c>.
/// </summary>
public sealed class RunQueryService(EshopGuardDb db, ShopReader reader, IRunQueueEstimator queue)
{
    public static bool IsFinal(RunStatus status) => status is RunStatus.Finished or RunStatus.Partial or RunStatus.Failed or RunStatus.Canceled;

    public async Task<CursorPage<RunDto>> ListAsync(Guid shopId, string? kind, string? cursor, int? limit, CancellationToken ct)
    {
        var size = Cursor.Limit(limit);
        Guid? after = Cursor.Decode(cursor) switch
        {
            null => null,
            [{ ValueKind: System.Text.Json.JsonValueKind.String } id] when Guid.TryParse(id.GetString(), out var parsed) => parsed,
            _ => throw Cursor.Invalid(),
        };
        var (runs, total) = await db.ExecuteInTenantTransactionAsync(async () =>
        {
            await reader.ReadAsync(shopId, ct).ConfigureAwait(false);
            var query = db.Runs.AsNoTracking().Where(r => r.ShopId == shopId);
            if (kind is not null)
            {
                var wanted = Enum.GetValues<RunKind>().Where(k => FindingMapper.Text(k) == kind).ToArray();
                if (wanted.Length == 0)
                {
                    throw DomainException.Validation(new ValidationResult().Add("kind", ProblemCodes.Fields.ValueNotAllowed));
                }

                query = query.Where(r => wanted.Contains(r.Kind));
            }

            var count = await query.CountAsync(ct).ConfigureAwait(false);
            if (after is { } last)
            {
                query = query.Where(r => r.Id.CompareTo(last) < 0);
            }

            return (await query.OrderByDescending(r => r.Id).Take(size + 1).ToListAsync(ct).ConfigureAwait(false), count);
        }, ct).ConfigureAwait(false);

        var items = new List<RunDto>();
        foreach (var run in runs.Take(size))
        {
            items.Add(await DtoAsync(run, ct).ConfigureAwait(false));
        }

        return new CursorPage<RunDto>(items, runs.Count > size ? Cursor.Encode(items[^1].Id) : null, total);
    }

    public async Task<RunDto> GetAsync(Guid runId, CancellationToken ct) =>
        await DtoAsync(await RequireAsync(runId, ct).ConfigureAwait(false), ct).ConfigureAwait(false);

    /// <summary>The run as stored (<c>404 run.not_found</c> for a run the tenant does not have).</summary>
    public async Task<Run> RequireAsync(Guid runId, CancellationToken ct) =>
        await db.ExecuteInTenantTransactionAsync(() => db.Runs.AsNoTracking().FirstOrDefaultAsync(r => r.Id == runId, ct), ct).ConfigureAwait(false)
            ?? throw new DomainException(ProblemCodes.RunNotFound, 404);

    /// <summary>The events of the run after <paramref name="afterId"/> in their order (at most <paramref name="limit"/>).</summary>
    public async Task<IReadOnlyList<RunEventDto>> EventsAsync(Guid runId, long afterId, int limit, CancellationToken ct) =>
        await db.ExecuteInTenantTransactionAsync(async () => (IReadOnlyList<RunEventDto>)(await db.RunEvents.AsNoTracking()
                .Where(e => e.RunId == runId && e.Id > afterId).OrderBy(e => e.Id).Take(limit).ToListAsync(ct).ConfigureAwait(false))
            .Select(e => new RunEventDto(e.Id, e.At, e.Level, e.Code, e.Data?.RootElement.Clone())).ToList(), ct).ConfigureAwait(false);

    public static RunProgressDto Progress(Run run)
    {
        ArgumentNullException.ThrowIfNull(run);
        var view = RunReadModel.ToView(run.Id, run.ShopId, run.Kind, run.Status, run.Error, run.CancelRequested, run.CreatedAt, run.StartedAt, run.FinishedAt,
            Object(run.Progress), Object(run.Stats), null).Progress;
        return new RunProgressDto(view.PagesPlanned, view.PagesFetched, view.PagesProcessed,
            view.Steps.ToDictionary(s => s.Key, s => new RunStepDto(s.Value.Done, s.Value.Total), StringComparer.Ordinal));
    }

    public static RunStatusDto Status(Run run) => new(FindingMapper.Text(run.Status), run.Error, run.FinishedAt, run.CancelRequested);

    private async Task<RunDto> DtoAsync(Run run, CancellationToken ct)
    {
        var estimate = IsFinal(run.Status) ? new RunQueueEstimate(null, null) : await queue.EstimateAsync(run.Id, ct).ConfigureAwait(false);
        return new RunDto(
            run.Id, run.ShopId, FindingMapper.Text(run.Kind), FindingMapper.Text(run.Trigger), FindingMapper.Text(run.Status), Progress(run), run.Jurisdictions, run.Modules,
            run.CreatedAt, run.StartedAt, run.FinishedAt, run.Error, run.CancelRequested, new RunQueueDto(estimate.Position, estimate.EstimatedFinishAt));
    }

    private static JsonObject Object(System.Text.Json.JsonDocument? document) =>
        document is null ? [] : JsonNode.Parse(document.RootElement.GetRawText()) as JsonObject ?? [];
}
