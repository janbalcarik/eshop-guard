using System.Text.Json.Nodes;
using EshopGuard.Application.Audit;
using EshopGuard.Application.Contracts;
using EshopGuard.Application.Findings;
using EshopGuard.Application.Problems;
using EshopGuard.Data.Entities.Checks;
using EshopGuard.Jobs.Runs;

namespace EshopGuard.Application.Runs;

/// <summary>
/// Canceling a run (change 11, task 11.1; K rozhodnutí 14): only the free sample and a recheck while they are not over; the
/// flag <c>cancel_requested</c> and the end are the run service's of change 8. A paid initial analysis cannot be canceled here
/// (refunds are change 12). Otherwise <c>409 run.not_cancelable</c> with the kind and the state.
/// </summary>
public sealed class RunCancelService(RunQueryService query, IRunService runs, SecurityAuditWriter audit)
{
    private static readonly RunKind[] Cancelable = [RunKind.FreeSample, RunKind.Recheck];

    public async Task<RunDto> CancelAsync(Guid userId, Guid runId, CancellationToken ct)
    {
        var run = await query.RequireAsync(runId, ct).ConfigureAwait(false);
        if (!Cancelable.Contains(run.Kind) || RunQueryService.IsFinal(run.Status))
        {
            throw NotCancelable(run);
        }

        var result = await runs.RequestCancelAsync(runId, userId, ct).ConfigureAwait(false);
        if (result.ErrorCode == RunCodes.RunNotFound)
        {
            throw new DomainException(ProblemCodes.RunNotFound, 404);
        }

        if (!result.Succeeded)
        {
            throw NotCancelable(await query.RequireAsync(runId, ct).ConfigureAwait(false));
        }

        await audit.WriteAsync(new AuditEvent(AuditActions.RunCancelRequested, run.TenantId, userId, "run", runId.ToString("D"), new JsonObject
        {
            ["shopId"] = run.ShopId.ToString("D"),
            ["kind"] = FindingMapper.Text(run.Kind),
        }), ct).ConfigureAwait(false);
        return await query.GetAsync(runId, ct).ConfigureAwait(false);
    }

    private static DomainException NotCancelable(Run run) => new(ProblemCodes.RunNotCancelable, 409, new Dictionary<string, object?>
    {
        ["kind"] = FindingMapper.Text(run.Kind),
        ["status"] = FindingMapper.Text(run.Status),
    });
}
