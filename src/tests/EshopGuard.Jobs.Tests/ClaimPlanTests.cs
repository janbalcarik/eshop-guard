using System.Diagnostics;
using EshopGuard.Data.Entities.Ops;
using EshopGuard.Jobs.Queue;
using Npgsql;
using NpgsqlTypes;

namespace EshopGuard.Jobs.Tests;

/// <summary>
/// Claim with a large queue (task 11.6): 100 000 waiting jobs without a key and 20 000 with a key on 500 domains. The claim
/// statements must use the queue indexes, never a sequential scan of ops.jobs (the row of paused classes in the small
/// ops.system_settings, checked by the claim since change 8, may be scanned); the measured time goes to the test output as a
/// baseline for the load test of change 17.
/// </summary>
public sealed class ClaimPlanTests(JobsTestDatabase db) : JobsTestBase(db)
{
    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        await Db.ExecuteAsync("Owner", """
            INSERT INTO ops.jobs (tenant_id, kind, resource_class, priority, payload, state, attempts, max_attempts, not_before)
            SELECT ('00000000-0000-0000-0000-' || lpad((g % 100)::text, 12, '0'))::uuid, 'test.record', 'cpu', (g % 5)::smallint, '{}', 'queued', 0, 5,
                   now() - interval '1 minute' + CASE WHEN g % 10 = 0 THEN interval '1 hour' ELSE interval '0' END
            FROM generate_series(1, 100000) g;
            INSERT INTO ops.jobs (tenant_id, kind, resource_class, priority, payload, state, attempts, max_attempts, not_before, concurrency_key)
            SELECT ('00000000-0000-0000-0000-' || lpad((g % 100)::text, 12, '0'))::uuid, 'test.fetch_domain', 'fetch', 2, '{}', 'queued', 0, 5,
                   now() - interval '1 minute', 'domain:d' || (g % 500)
            FROM generate_series(1, 20000) g;
            """);
        await Db.ExecuteAsync("Owner", "VACUUM ANALYZE ops.jobs");
    }

    [Fact]
    public async Task ClaimWithoutKey_UsesTheQueueIndex()
    {
        var plan = await ExplainAsync(JobQueueSql.ClaimUnkeyed, JobResourceClass.Cpu, 10, tenantCap: 4);

        Assert.Contains("ix_jobs_queued_unkeyed", plan, StringComparison.Ordinal);
        Assert.DoesNotContain("Seq Scan on jobs", plan, StringComparison.Ordinal);
        TestContext.Current.SendDiagnosticMessage("ClaimUnkeyed plan:\n" + plan);
    }

    [Fact]
    public async Task ClaimWithKey_UsesTheQueueIndexes()
    {
        var plan = await ExplainAsync(JobQueueSql.ClaimKeyedAhead, JobResourceClass.Fetch, 1, tenantCap: 20);

        Assert.Contains("ix_jobs_queued_keyed", plan, StringComparison.Ordinal);
        Assert.Contains("ix_jobs_queued_key_head", plan, StringComparison.Ordinal);
        Assert.DoesNotContain("Seq Scan on jobs", plan, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ClaimOfTenJobs_IsFast()
    {
        // Without a tenant cap: with capped tenants at the head of the queue the claim walks past their jobs
        // (measured 1. 10. 2026: about 30 ms for 20 000 skipped jobs), a question for the load test of change 17.
        await using var queue = new DirectQueue(Data.Connections.DatabaseRole.Worker, new Dictionary<string, string?> { ["Worker:TenantCaps:Cpu"] = "0", ["Worker:TenantCaps:Fetch"] = "0" });
        await queue.Queue.ClaimAsync(JobResourceClass.Cpu, 1, "warm-up", Ct);

        var unkeyed = await MeasureAsync(() => queue.Queue.ClaimAsync(JobResourceClass.Cpu, 10, "w", Ct), 10);
        var keyed = await MeasureAsync(() => queue.Queue.ClaimAsync(JobResourceClass.Fetch, 1, "w", Ct), 1);

        TestContext.Current.SendDiagnosticMessage($"claim 10 of 100 000 (cpu): median {unkeyed:0.0} ms; claim 1 keyed of 20 000 (fetch): median {keyed:0.0} ms");
        Assert.True(unkeyed < 100, $"claim took {unkeyed:0.0} ms");
        Assert.True(keyed < 100, $"keyed claim took {keyed:0.0} ms");
    }

    private static async Task<double> MeasureAsync(Func<Task<IReadOnlyList<ClaimedJob>>> claim, int expected)
    {
        var times = new List<double>();
        for (var i = 0; i < 7; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            Assert.Equal(expected, (await claim()).Count);
            times.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

        return times.Order().ElementAt(times.Count / 2);
    }

    private async Task<string> ExplainAsync(string sql, JobResourceClass resourceClass, int n, int tenantCap)
    {
        await using var connection = await Db.For("Worker").OpenConnectionAsync(Ct);
        await using var transaction = await connection.BeginTransactionAsync(Ct);
        await using var command = new NpgsqlCommand("EXPLAIN (ANALYZE, COSTS OFF) " + sql, connection, transaction)
        {
            Parameters =
            {
                new NpgsqlParameter("class", NpgsqlDbType.Text) { Value = resourceClass.ToDb() },
                new NpgsqlParameter("n", NpgsqlDbType.Integer) { Value = n },
                new NpgsqlParameter("tenant_cap", NpgsqlDbType.Integer) { Value = tenantCap },
                new NpgsqlParameter("worker", NpgsqlDbType.Text) { Value = "explain" },
                new NpgsqlParameter("lease", NpgsqlDbType.Interval) { Value = TimeSpan.FromMinutes(2) },
            },
        };
        var lines = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync(Ct))
        {
            while (await reader.ReadAsync(Ct))
            {
                lines.Add(reader.GetString(0));
            }
        }

        await transaction.RollbackAsync(Ct);
        return string.Join('\n', lines);
    }
}
