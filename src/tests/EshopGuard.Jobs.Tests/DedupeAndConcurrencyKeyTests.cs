using EshopGuard.Data.Entities.Ops;
using EshopGuard.Jobs.Queue;
using EshopGuard.Jobs.Workers;
using Npgsql;

namespace EshopGuard.Jobs.Tests;

public sealed class DedupeAndConcurrencyKeyTests(JobsTestDatabase db) : JobsTestBase(db)
{
    [Fact]
    public async Task SameDedupeKey_ReturnsTheExistingJob()
    {
        var first = await Direct.EnqueueAsync(TestJobs.Request(TestHandlers.Record, dedupeKey: "monitor:shop:2026-10-05"));
        var second = await Direct.EnqueueAsync(TestJobs.Request(TestHandlers.Record, dedupeKey: "monitor:shop:2026-10-05"));

        Assert.True(first.Created);
        Assert.False(second.Created);
        Assert.Equal(first.JobId, second.JobId);
        Assert.Equal(JobState.Queued, second.State);
        Assert.Equal(1L, await Db.ScalarAsync<long>("SELECT count(*) FROM ops.jobs WHERE dedupe_key = 'monitor:shop:2026-10-05'"));
    }

    [Fact]
    public async Task TenJobsWithOneKey_OnFourWorkers_NeverRunAtTheSameTime()
    {
        var key = JobKeys.Run(Guid.NewGuid(), "rules");
        await Direct.EnqueueManyAsync(Enumerable.Range(0, 10).Select(i => TestJobs.Request(TestHandlers.Record, payload: new { sleepMs = 50, tag = "k" + i }, concurrencyKey: key)));
        await Workers.StartManyAsync(4, new Dictionary<string, string?> { ["Worker:Slots:Cpu"] = "4" });

        await Workers.WaitForEmptyQueueAsync(TimeSpan.FromSeconds(60));

        Assert.Equal(10L, await Db.ScalarAsync<long>("SELECT count(*) FROM ops.jobs WHERE state = 'succeeded'"));
        var intervals = State.Intervals.OrderBy(i => i.Start).ToList();
        Assert.Equal(10, intervals.Count);
        for (var i = 1; i < intervals.Count; i++)
        {
            Assert.True(intervals[i].Start >= intervals[i - 1].End, $"job {intervals[i].Tag} started before {intervals[i - 1].Tag} ended");
        }

        Assert.Equal(Enumerable.Range(0, 10).Select(i => "k" + i), intervals.Select(i => i.Tag));
    }

    [Fact]
    public async Task TwoClaimsOfOneKeyAtOnce_SecondHitsTheUniqueIndex()
    {
        var ids = await Direct.EnqueueManyAsync(
            [TestJobs.Request(TestHandlers.Record, concurrencyKey: "run:R:rules"), TestJobs.Request(TestHandlers.Record, concurrencyKey: "run:R:rules")]);
        const string take = """
            UPDATE ops.jobs SET state = 'running', attempts = attempts + 1, lease_owner = $2, lease_until = clock_timestamp() + interval '1 minute'
            WHERE id = $1
            """;

        await using var first = await Db.For("Worker").OpenConnectionAsync(Ct);
        await using var second = await Db.For("Worker").OpenConnectionAsync(Ct);
        await using var firstTx = await first.BeginTransactionAsync(Ct);
        await using var secondTx = await second.BeginTransactionAsync(Ct);
        await Execute(first, firstTx, take, ids[0], "w1");
        var blocked = Execute(second, secondTx, take, ids[1], "w2");
        await Task.Delay(300, Ct);
        Assert.False(blocked.IsCompleted, "the second claim waits for the first transaction");
        await firstTx.CommitAsync(Ct);

        var ex = await Assert.ThrowsAsync<PostgresException>(() => blocked);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, ex.SqlState);
        Assert.Equal("ux_jobs_concurrency_running", ex.ConstraintName);
        await secondTx.RollbackAsync(Ct);
        Assert.Equal(["running", "queued"], await Db.RowsAsync("SELECT state FROM ops.jobs ORDER BY id").ContinueWith(t => t.Result.Select(r => (string)r[0]!).ToList()));
    }

    [Fact]
    public async Task ParallelClaims_TakeAtMostOneJobPerKey()
    {
        await Direct.EnqueueManyAsync(Enumerable.Range(0, 6).Select(i => TestJobs.Request(TestHandlers.Record, concurrencyKey: "domain:k" + (i % 2))));

        var claims = await Task.WhenAll(Enumerable.Range(0, 4).Select(i => Direct.Queue.ClaimAsync(JobResourceClass.Cpu, 6, "w" + i)));

        var claimed = claims.SelectMany(c => c).ToList();
        Assert.Equal(2, claimed.Count);
        Assert.Equal(["domain:k0", "domain:k1"], claimed.Select(c => c.ConcurrencyKey).Order());
    }

    private static async Task Execute(NpgsqlConnection connection, NpgsqlTransaction transaction, string sql, long id, string worker)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction) { Parameters = { new() { Value = id }, new() { Value = worker } } };
        await command.ExecuteNonQueryAsync();
    }
}
