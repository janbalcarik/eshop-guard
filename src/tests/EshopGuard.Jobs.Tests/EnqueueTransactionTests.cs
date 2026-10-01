using EshopGuard.Data.Connections;
using EshopGuard.Data.Entities.Checks;
using EshopGuard.Data.Entities.Ops;
using EshopGuard.Jobs.Queue;
using Npgsql;

namespace EshopGuard.Jobs.Tests;

/// <summary>Enqueue in the caller's transaction (as the API: <c>eshopguard_app</c>), notifications after COMMIT only.</summary>
public sealed class EnqueueTransactionTests(JobsTestDatabase db) : JobsTestBase(db)
{
    [Fact]
    public async Task RunAndJobInOneTransaction_Commit_CreatesJobAndNotifiesAfterCommit()
    {
        var seed = await TestRuns.CreateAsync();
        await using var app = new DirectQueue(DatabaseRole.App);
        await using var listener = await Listener.StartAsync();

        await using var context = seed.AppDb();
        await using var transaction = await context.Database.BeginTransactionAsync(Ct);
        var run = new Run { ShopId = seed.ShopId, Kind = RunKind.FreeSample, Trigger = RunTrigger.User, Status = RunStatus.Queued, Priority = 0, Jurisdictions = ["sk"], Modules = ["eco"] };
        context.Add(run);
        await context.SaveChangesAsync(Ct);
        var result = await context.EnqueueJobAsync(app.Queue, TestJobs.Request(TestHandlers.Record, tenantId: seed.TenantId, shopId: seed.ShopId, runId: run.Id), Ct);

        Assert.True(result.Created);
        Assert.Equal(JobState.Queued, result.State);
        Assert.Empty(await listener.ReceiveAsync(TimeSpan.FromMilliseconds(500)));

        await transaction.CommitAsync(Ct);

        Assert.Equal(["cpu"], await listener.ReceiveAsync(TimeSpan.FromSeconds(5)));
        var row = Assert.Single(await Db.RowsAsync("SELECT state, attempts, run_id, tenant_id FROM ops.jobs WHERE id = $1", result.JobId));
        Assert.Equal(["queued", 0, run.Id, seed.TenantId], row);
    }

    [Fact]
    public async Task Rollback_LeavesNoJob_AndNoNotification()
    {
        var seed = await TestRuns.CreateAsync();
        await using var app = new DirectQueue(DatabaseRole.App);
        await using var listener = await Listener.StartAsync();

        await using (var context = seed.AppDb())
        {
            await using var transaction = await context.Database.BeginTransactionAsync(Ct);
            context.Add(new Run { ShopId = seed.ShopId, Kind = RunKind.FreeSample, Trigger = RunTrigger.User, Status = RunStatus.Queued, Priority = 0 });
            await context.SaveChangesAsync(Ct);
            await context.EnqueueJobAsync(app.Queue, TestJobs.Request(TestHandlers.Record, tenantId: seed.TenantId), Ct);
            await transaction.RollbackAsync(Ct);
        }

        Assert.Empty(await listener.ReceiveAsync(TimeSpan.FromSeconds(1)));
        Assert.Equal(0L, await Db.ScalarAsync<long>("SELECT count(*) FROM ops.jobs"));
    }

    [Fact]
    public async Task WithoutTransaction_IsRefusedWithCode_AndWritesNothing()
    {
        var seed = await TestRuns.CreateAsync();
        await using var app = new DirectQueue(DatabaseRole.App);
        await using var context = seed.AppDb();

        var ex = await Assert.ThrowsAsync<JobQueueException>(() => context.EnqueueJobAsync(app.Queue, TestJobs.Request(TestHandlers.Record), Ct));

        Assert.IsAssignableFrom<InvalidOperationException>(ex);
        Assert.Equal(JobErrorCodes.EnqueueRequiresTransaction, ex.Code);
        Assert.Equal(0L, await Db.ScalarAsync<long>("SELECT count(*) FROM ops.jobs"));
    }

    [Fact]
    public async Task InvalidRequests_AreRefusedWithCodes()
    {
        var big = new string('x', JobRequest.MaxPayloadBytes);
        var cases = new (JobRequest Request, string Code)[]
        {
            (TestJobs.Request(TestHandlers.Record, payload: new { text = big }), JobErrorCodes.PayloadTooLarge),
            (TestJobs.Request(" "), JobErrorCodes.InvalidRequest),
            (TestJobs.Request(TestHandlers.Record, priority: (JobPriority)7), JobErrorCodes.InvalidRequest),
            (TestJobs.Request(TestHandlers.Record, maxAttempts: 0), JobErrorCodes.InvalidRequest),
            (TestJobs.Request(TestHandlers.Record, runId: Guid.NewGuid()), JobErrorCodes.InvalidRequest),
        };

        foreach (var (request, code) in cases)
        {
            var ex = await Assert.ThrowsAsync<JobQueueException>(() => Direct.EnqueueAsync(request));
            Assert.Equal(code, ex.Code);
        }

        Assert.Equal(0L, await Db.ScalarAsync<long>("SELECT count(*) FROM ops.jobs"));
    }

    /// <summary>LISTEN on its own connection, as the worker's listener.</summary>
    private sealed class Listener : IAsyncDisposable
    {
        private readonly NpgsqlConnection _connection;
        private readonly List<string> _payloads = [];

        private Listener(NpgsqlConnection connection)
        {
            _connection = connection;
            _connection.Notification += (_, e) =>
            {
                lock (_payloads)
                {
                    _payloads.Add(e.Payload);
                }
            };
        }

        public static async Task<Listener> StartAsync()
        {
            var connection = new NpgsqlConnection(JobsTestDatabase.ConnectionString("Worker"));
            await connection.OpenAsync();
            var listener = new Listener(connection);
            await using var listen = new NpgsqlCommand("LISTEN " + JobNames.NotificationChannel, connection);
            await listen.ExecuteNonQueryAsync();
            return listener;
        }

        /// <summary>Notifications that arrive within the timeout.</summary>
        public async Task<List<string>> ReceiveAsync(TimeSpan timeout)
        {
            await _connection.WaitAsync((int)timeout.TotalMilliseconds);
            lock (_payloads)
            {
                var received = _payloads.ToList();
                _payloads.Clear();
                return received;
            }
        }

        public ValueTask DisposeAsync() => _connection.DisposeAsync();
    }
}
