using EshopGuard.Data.Entities.Ops;
using EshopGuard.Jobs.Queue;

namespace EshopGuard.Jobs.Tests;

public sealed class RetryAndFailureTests(JobsTestDatabase db) : JobsTestBase(db)
{
    private const string Backoff = "SELECT extract(epoch FROM not_before - updated_at)::double precision FROM ops.jobs WHERE id = $1";

    [Fact]
    public async Task Backoff_GrowsTwofold_WithinTheJitter()
    {
        // Direct: Jobs:Retry:BaseSeconds = 1.
        var id = (await Direct.EnqueueAsync(TestJobs.Request(TestHandlers.Fail))).JobId;

        var first = Assert.Single(await Direct.Queue.ClaimAsync(JobResourceClass.Cpu, 1, "w", Ct));
        Assert.Equal(JobState.Queued, await Direct.Queue.FailAsync(first, new JobError("jev.http_503"), permanent: false, ct: Ct));
        var backoff1 = await Db.ScalarAsync<double>(Backoff, id);
        await Db.ExecuteAsync("Owner", "UPDATE ops.jobs SET not_before = clock_timestamp() WHERE id = $1", id);

        var second = Assert.Single(await Direct.Queue.ClaimAsync(JobResourceClass.Cpu, 1, "w", Ct));
        Assert.Equal(2, second.Attempt);
        await Direct.Queue.FailAsync(second, new JobError("jev.http_503"), permanent: false, ct: Ct);
        var backoff2 = await Db.ScalarAsync<double>(Backoff, id);

        Assert.InRange(backoff1, 0.8, 1.2);
        Assert.InRange(backoff2 / backoff1, 1.33, 3.0);
        Assert.Equal("jev.http_503", await Db.ScalarAsync<string>("SELECT last_error FROM ops.jobs WHERE id = $1", id));
    }

    [Fact]
    public async Task RetryAfter_OverridesTheBackoff()
    {
        var id = (await Direct.EnqueueAsync(TestJobs.Request(TestHandlers.Fail))).JobId;
        var job = Assert.Single(await Direct.Queue.ClaimAsync(JobResourceClass.Cpu, 1, "w", Ct));

        await Direct.Queue.FailAsync(job, new JobError("openai.http_429"), permanent: false, retryAfter: TimeSpan.FromSeconds(42), Ct);

        Assert.InRange(await Db.ScalarAsync<double>(Backoff, id), 41.9, 42.1);
    }

    [Fact]
    public async Task TwoRetries_ThenSuccess()
    {
        var id = (await Direct.EnqueueAsync(TestJobs.Request(TestHandlers.Fail, payload: new { failures = 2, mode = "retry", code = "jev.http_503" }))).JobId;
        await Workers.StartAsync("w", new Dictionary<string, string?> { ["Worker:Slots:Cpu"] = "1" });

        await Workers.WaitForEmptyQueueAsync(TimeSpan.FromSeconds(20));

        Assert.Equal(["succeeded", 3, null], Assert.Single(await Db.RowsAsync("SELECT state, attempts, last_error FROM ops.jobs WHERE id = $1", id)));
        Assert.Equal(1L, await Db.ScalarAsync<long>("SELECT count(*) FROM jobs_test.effects WHERE job_id = $1", id));
    }

    [Fact]
    public async Task AttemptsUsedUp_FailWithTheCode()
    {
        var id = (await Direct.EnqueueAsync(TestJobs.Request(TestHandlers.Fail, payload: new { mode = "retry", code = "jev.http_503" }, maxAttempts: 3))).JobId;
        await Workers.StartAsync("w", new Dictionary<string, string?> { ["Worker:Slots:Cpu"] = "1" });

        await Workers.WaitForEmptyQueueAsync(TimeSpan.FromSeconds(20));

        var row = Assert.Single(await Db.RowsAsync("SELECT state, attempts, finished_at IS NOT NULL, last_error FROM ops.jobs WHERE id = $1", id));
        Assert.Equal(["failed", 3, true], row[..3]);
        Assert.StartsWith("jev.http_503", (string)row[3]!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fail_IsPermanentAtOnce()
    {
        var id = (await Direct.EnqueueAsync(TestJobs.Request(TestHandlers.Fail, payload: new { mode = "fail", code = "shop.not_found" }))).JobId;
        await Workers.StartAsync("w", new Dictionary<string, string?> { ["Worker:Slots:Cpu"] = "1" });

        await Workers.WaitForEmptyQueueAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(["failed", 1, "shop.not_found"], Assert.Single(await Db.RowsAsync("SELECT state, attempts, last_error FROM ops.jobs WHERE id = $1", id)));
        Assert.Equal(1, State.Executions[id]);
    }

    [Fact]
    public async Task Defer_DoesNotCountTheAttempt()
    {
        var id = (await Direct.EnqueueAsync(TestJobs.Request(TestHandlers.Fail, payload: new { failures = 1, mode = "defer", code = "domain.busy" }))).JobId;
        await Workers.StartAsync("w", new Dictionary<string, string?> { ["Worker:Slots:Cpu"] = "1" });

        await Workers.WaitForEmptyQueueAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(["succeeded", 1], Assert.Single(await Db.RowsAsync("SELECT state, attempts FROM ops.jobs WHERE id = $1", id)));
        Assert.Equal(2, State.Executions[id]);
    }

    [Fact]
    public async Task UnhandledException_IsRetried_WithTypeOnly()
    {
        var retried = (await Direct.EnqueueAsync(TestJobs.Request(TestHandlers.Fail, payload: new { failures = 1, mode = "throw" }))).JobId;
        var failed = (await Direct.EnqueueAsync(TestJobs.Request(TestHandlers.Fail, payload: new { mode = "throw" }, maxAttempts: 1))).JobId;
        var worker = await Workers.StartAsync("w", new Dictionary<string, string?> { ["Worker:Slots:Cpu"] = "1" });

        await Workers.WaitForEmptyQueueAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(["succeeded", 2], Assert.Single(await Db.RowsAsync("SELECT state, attempts FROM ops.jobs WHERE id = $1", retried)));
        Assert.Equal(["failed", "job.unhandled: System.InvalidOperationException"], Assert.Single(await Db.RowsAsync("SELECT state, last_error FROM ops.jobs WHERE id = $1", failed)));
        Assert.True(worker.Logs.Contains(JobErrorCodes.Unhandled));
        Assert.DoesNotContain(worker.Logs.Logs, l => l.AllText.Contains("must not reach", StringComparison.Ordinal));
    }

    [Fact]
    public void LastError_IsCutTo500Characters()
    {
        var error = new JobError("job.unhandled", "System.Exception", new string('x', 1000));

        Assert.Equal(JobError.MaxLength, error.Format().Length);
        Assert.StartsWith("job.unhandled: System.Exception: xxx", error.Format(), StringComparison.Ordinal);
    }
}
