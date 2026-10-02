using EshopGuard.Data.Tenancy;
using EshopGuard.Jobs.Runs;
using EshopGuard.Jobs.Runs.Handlers;
using EshopGuard.Jobs.Tests.Runs.Support;
using Npgsql;

namespace EshopGuard.Jobs.Tests.Runs;

/// <summary>
/// The barrier of the batch steps (design of change 8, task 2.9): last batches finishing at the same moment on different
/// connections count themselves once each, and exactly one of them starts the next step.
/// </summary>
public sealed class RunBarrierTests(JobsTestDatabase database) : JobsTestBase(database)
{
    [Theory]
    [InlineData(2)]
    [InlineData(8)]
    public async Task LastBatchesCompletingTogether_StartTheNextStepExactlyOnce(int batches)
    {
        var shop = await RunTests.CreateShopAsync();
        var runId = await InsertRunAsync(shop, batches);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var completions = Enumerable.Range(0, batches).Select(async _ =>
        {
            await start.Task;
            await using var connection = await Db.For("Worker").OpenConnectionAsync(Ct);
            await using var transaction = await TenantSql.BeginAsync(connection, shop.TenantId, ct: Ct);
            var locked = await RunStore.LoadAsync(connection, transaction, runId, forUpdate: true, Ct);
            var last = await JevBarrier.AdvanceAsync(connection, transaction, locked!, "evaluate", Ct);
            await transaction.CommitAsync(Ct);
            return last;
        }).ToList();
        start.SetResult();
        var results = await Task.WhenAll(completions);

        Assert.Single(results, r => r);
        Assert.Equal(batches, await RunTests.ScalarAsync<long>(Db, shop.TenantId, "SELECT (progress -> 'steps' -> 'evaluate' ->> 'done')::bigint FROM checks.runs WHERE id = $1", runId));
        Assert.Equal(batches, await RunTests.ScalarAsync<long>(Db, shop.TenantId, "SELECT count(*) FROM checks.run_events WHERE run_id = $1 AND code = 'run.progress'", runId));
    }

    private async Task<Guid> InsertRunAsync(RunShop shop, int batches)
    {
        var runId = Guid.CreateVersion7();
        await using var connection = await Db.For("Owner").OpenConnectionAsync(Ct);
        await using var transaction = await TenantSql.BeginAsync(connection, shop.TenantId, ct: Ct);
        await using var insert = new NpgsqlCommand(
            """
            INSERT INTO checks.runs (id, tenant_id, shop_id, kind, trigger, status, priority, progress, created_at, updated_at)
            VALUES ($1, $2, $3, 'full_analysis', 'user', 'evaluating', 1, jsonb_build_object('steps', jsonb_build_object('evaluate', jsonb_build_object('done', 0, 'total', $4))), now(), now())
            """, connection, transaction)
        {
            Parameters =
            {
                new NpgsqlParameter { Value = runId },
                new NpgsqlParameter { Value = shop.TenantId },
                new NpgsqlParameter { Value = shop.ShopId },
                new NpgsqlParameter { Value = batches },
            },
        };
        await insert.ExecuteNonQueryAsync(Ct);
        await transaction.CommitAsync(Ct);
        return runId;
    }
}
