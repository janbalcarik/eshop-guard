using System.Text.Json.Nodes;
using EshopGuard.Data.Entities.Checks;
using EshopGuard.Jobs.Queue;
using Npgsql;

namespace EshopGuard.Jobs.Runs;

/// <summary>Transitions that more places need: start of the downloads, cancellation and failure of a run.</summary>
internal static class RunTransitions
{
    /// <summary>
    /// The run goes to <c>crawling</c> and the first batch of downloads of every scope is enqueued, in the caller's
    /// transaction (the run row is locked by the caller).
    /// </summary>
    public static async Task<bool> StartCrawlAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, IJobQueue queue, RunRow run, RunStatus from, CancellationToken ct)
    {
        if (!await RunStateMachine.TryTransitionAsync(connection, transaction, run.TenantId, run.Id, from, RunStatus.Crawling, ct).ConfigureAwait(false))
        {
            return false;
        }

        var scopes = await RunScopeStore.LoadAllAsync(connection, transaction, run.Id, ct).ConfigureAwait(false);
        for (var i = 0; i < scopes.Count; i++)
        {
            if (!scopes[i].Exhausted)
            {
                await queue.EnqueueAsync(RunPlan.Fetch(run, i, scopes[i].ScopeKey, scopes[i].Domain, 1), transaction, ct).ConfigureAwait(false);
            }
        }

        if (scopes.All(s => s.Exhausted))
        {
            // Nothing to download (every scope stopped in discovery): straight to the profiles, which find no page.
            await RunStateMachine.TryTransitionAsync(connection, transaction, run.TenantId, run.Id, RunStatus.Crawling, RunStatus.Profiling, ct).ConfigureAwait(false);
            await queue.EnqueueAsync(RunPlan.Profile(run), transaction, ct).ConfigureAwait(false);
        }

        return true;
    }

    /// <summary>Ends the run <c>canceled</c> (its cancellation was requested) and cancels its waiting jobs.</summary>
    public static async Task CancelAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, IJobQueue queue, RunRow run, CancellationToken ct)
    {
        if (await RunStateMachine.TryTransitionAsync(connection, transaction, run.TenantId, run.Id, run.Status, RunStatus.Canceled, ct).ConfigureAwait(false))
        {
            await RunEventWriter.WriteAsync(connection, transaction, run.TenantId, run.Id, "info", RunCodes.EventCanceled, new JsonObject(), ct).ConfigureAwait(false);
        }

        await queue.CancelRunJobsAsync(run.Id, transaction, ct).ConfigureAwait(false);
    }

    /// <summary>Ends the run <c>failed</c> with a code (nothing could be checked) and cancels its waiting jobs.</summary>
    public static async Task FailAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, IJobQueue queue, RunRow run, string code, CancellationToken ct)
    {
        if (await RunStateMachine.TryTransitionAsync(connection, transaction, run.TenantId, run.Id, run.Status, RunStatus.Failed, ct, code).ConfigureAwait(false))
        {
            await RunEventWriter.WriteAsync(connection, transaction, run.TenantId, run.Id, "error", RunCodes.EventFailed, new JsonObject { ["code"] = code }, ct).ConfigureAwait(false);
        }

        await queue.CancelRunJobsAsync(run.Id, transaction, ct).ConfigureAwait(false);
    }
}
