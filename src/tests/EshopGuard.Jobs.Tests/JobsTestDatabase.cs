using EshopGuard.Data;
using EshopGuard.Data.Migrations;
using EshopGuard.Data.Tenancy;
using EshopGuard.Tests.Shared;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EshopGuard.Jobs.Tests;

/// <summary>
/// Own test database <c>eshopguard_test_jobs</c> (K rozhodnutí 7): the tests empty <c>ops.jobs</c> before each test, which
/// must not disturb other test projects. Connections are those of <c>eshopguard-tests</c> with the database replaced.
/// </summary>
public sealed class JobsTestDatabase : IAsyncLifetime
{
    /// <summary>Name of the database.</summary>
    public const string DatabaseName = "eshopguard_test_jobs";

    private readonly Dictionary<string, NpgsqlDataSource> _sources = [];

    /// <summary>Connection string of <c>ConnectionStrings:{role}</c> to this database.</summary>
    public static string ConnectionString(string role) =>
        new NpgsqlConnectionStringBuilder(TestConfiguration.ConnectionString(role)) { Database = DatabaseName }.ConnectionString;

    /// <summary>Pool of the role (Owner, App, Worker, Admin).</summary>
    public NpgsqlDataSource For(string role)
    {
        lock (_sources)
        {
            if (!_sources.TryGetValue(role, out var source))
            {
                source = NpgsqlDataSource.Create(ConnectionString(role));
                _sources[role] = source;
            }

            return source;
        }
    }

    /// <summary>Context of the role with the application's conventions and interceptors.</summary>
    public static EshopGuardDb CreateDb(string role, ITenantContext tenant)
    {
        var options = new DbContextOptionsBuilder<EshopGuardDb>();
        options.UseEshopGuardNpgsql(ConnectionString(role)).UseEshopGuardInterceptors(TimeProvider.System);
        return new EshopGuardDb(options.Options, tenant);
    }

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<EshopGuardDb>();
        options.UseEshopGuardNpgsql(ConnectionString("Owner"));
        await using (var db = new EshopGuardDb(options.Options, new TenantContext()))
        {
            await db.Database.MigrateAsync();
        }

        await ExecuteAsync("Owner", """
            CREATE SCHEMA IF NOT EXISTS jobs_test;
            CREATE TABLE IF NOT EXISTS jobs_test.effects (
                job_id bigint NOT NULL, attempt int NOT NULL, worker_id text NOT NULL,
                started_at timestamptz NOT NULL, finished_at timestamptz NOT NULL, tag text);
            GRANT USAGE ON SCHEMA jobs_test TO eshopguard_worker;
            GRANT SELECT, INSERT ON jobs_test.effects TO eshopguard_worker;
            """);
    }

    /// <summary>Empties the queue, workers, domains and effects and restores the rows of migration F2.</summary>
    public Task ResetAsync() => ExecuteAsync("Owner", """
        TRUNCATE ops.jobs, ops.workers, ops.domains, jobs_test.effects;
        DELETE FROM ops.rate_limit_buckets;
        DELETE FROM ops.system_settings WHERE key = 'jobs.paused_classes';
        """ + SqlResource.Read("F2/01_job_queue.sql"));

    /// <summary>Runs SQL as the role.</summary>
    public async Task ExecuteAsync(string role, string sql, params object?[] parameters)
    {
        await using var command = For(role).CreateCommand(sql);
        foreach (var parameter in parameters)
        {
            command.Parameters.Add(new NpgsqlParameter { Value = parameter ?? DBNull.Value });
        }

        await command.ExecuteNonQueryAsync();
    }

    /// <summary>First column of the first row (as the owner unless given).</summary>
    public async Task<T> ScalarAsync<T>(string sql, params object?[] parameters)
    {
        await using var command = For("Owner").CreateCommand(sql);
        foreach (var parameter in parameters)
        {
            command.Parameters.Add(new NpgsqlParameter { Value = parameter ?? DBNull.Value });
        }

        var value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? default! : (T)value;
    }

    /// <summary>Rows as arrays (as the owner).</summary>
    public async Task<List<object?[]>> RowsAsync(string sql, params object?[] parameters)
    {
        await using var command = For("Owner").CreateCommand(sql);
        foreach (var parameter in parameters)
        {
            command.Parameters.Add(new NpgsqlParameter { Value = parameter ?? DBNull.Value });
        }

        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<object?[]>();
        while (await reader.ReadAsync())
        {
            var row = new object?[reader.FieldCount];
            reader.GetValues(row!);
            rows.Add([.. row.Select(v => v is DBNull ? null : v)]);
        }

        return rows;
    }

    /// <summary>State of the queue for failure messages.</summary>
    public async Task<string> DumpJobsAsync()
    {
        var rows = await RowsAsync("""
            SELECT id, kind, resource_class, state, attempts, lease_owner, concurrency_key, last_error
            FROM ops.jobs ORDER BY id LIMIT 40
            """);
        return string.Join('\n', rows.Select(r => string.Join(" | ", r.Select(v => v?.ToString() ?? "∅"))));
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        foreach (var source in _sources.Values)
        {
            await source.DisposeAsync();
        }
    }
}

/// <summary>All queue tests in one collection without parallelism (they share one queue).</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class JobsCollection : ICollectionFixture<JobsTestDatabase>
{
    /// <summary>Collection name.</summary>
    public const string Name = "Jobs";
}

/// <summary>Base of the queue tests: empties the queue before each test and stops the test's workers after it.</summary>
[Collection(JobsCollection.Name)]
[Trait("Category", "Db")]
public abstract class JobsTestBase(JobsTestDatabase database) : IAsyncLifetime
{
    private WorkerHarness? _harness;
    private DirectQueue? _direct;

    /// <summary>The database.</summary>
    protected JobsTestDatabase Db => database;

    /// <summary>State shared by the test handlers of all workers of the test.</summary>
    internal TestState State { get; } = new();

    /// <summary>Workers of the test (created on first use).</summary>
    internal WorkerHarness Workers => _harness ??= new WorkerHarness(database, State);

    /// <summary>Queue and worker store without a worker (as <c>eshopguard_worker</c>).</summary>
    internal DirectQueue Direct => _direct ??= new DirectQueue(Data.Connections.DatabaseRole.Worker);

    /// <summary>Cancellation of the test.</summary>
    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <inheritdoc />
    public virtual async ValueTask InitializeAsync() => await database.ResetAsync();

    /// <inheritdoc />
    public virtual async ValueTask DisposeAsync()
    {
        State.Gate.TrySetResult();
        if (_harness is not null)
        {
            await _harness.DisposeAsync();
        }

        if (_direct is not null)
        {
            await _direct.DisposeAsync();
        }
    }
}
