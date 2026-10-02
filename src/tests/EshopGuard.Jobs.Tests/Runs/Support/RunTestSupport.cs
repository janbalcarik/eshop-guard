using System.Globalization;
using System.Text.Json.Nodes;
using EshopGuard.Core.Crawl;
using EshopGuard.Core.Jev;
using EshopGuard.Core.Options;
using EshopGuard.Core.Tests;
using EshopGuard.Data.Entities.Iam;
using EshopGuard.Data.Entities.Shops;
using EshopGuard.Data.Tenancy;
using EshopGuard.Jobs.Runs;
using EshopGuard.Storage;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace EshopGuard.Jobs.Tests.Runs.Support;

/// <summary>
/// Jev of the tests of runs: the answers of the library's mock (keywords and a stable hash), but under the model
/// <c>test-deterministic</c> and cached like real answers (the mock itself is never cached, design decision 11). Counts calls.
/// </summary>
internal sealed class DeterministicTestJevClient : IJevClient
{
    public const string ModelName = "test-deterministic";

    private readonly MockJevClient _inner = new();
    private int _calls;

    public int Calls => Volatile.Read(ref _calls);

    /// <summary>Fails every call with this status (503: temporary, 401: fatal) while set.</summary>
    public int? FailWith { get; set; }

    public async Task<JevResult> EvaluateAsync(object state, IReadOnlyDictionary<string, JevQuestion> questions, CancellationToken ct)
    {
        if (FailWith is { } status)
        {
            throw new JevApiException("test failure", status, null, isFatal: status is 401 or 403);
        }

        Interlocked.Increment(ref _calls);
        var result = await _inner.EvaluateAsync(state, questions, ct);
        return new JevResult { Model = ModelName, Answers = result.Answers, Usage = result.Usage };
    }
}

/// <summary>Every tenant may run a full analysis of its e-shop (the verification of ownership is change 10).</summary>
internal sealed class AllowOwnershipPolicy : IShopOwnershipPolicy
{
    public Task<string?> CheckAsync(Guid tenantId, Guid shopId, CancellationToken ct) => Task.FromResult<string?>(null);
}

/// <summary>A tenant with its e-shop at the address the fixture is served under.</summary>
internal sealed record RunShop(Guid TenantId, Guid ShopId, string Domain, Uri BaseUrl);

/// <summary>Options and helpers of the tests of runs: the same library settings for the worker and for the CLI's runner.</summary>
internal static class RunTests
{
    public static void Configure(EshopGuardOptions options)
    {
        var root = AppContext.BaseDirectory;
        options.Crawl.RequestsPerSecond = 0;
        options.Jev.UseMock = false;
        options.Jev.Model = DeterministicTestJevClient.ModelName;
        options.Rules.Directory = Path.Combine(root, "rules");
        options.Rules.LabelsFile = Path.Combine(root, "config", "labels.yaml");
        options.Rules.LegalRequirementsFile = Path.Combine(root, "config", "legal_requirements.yaml");
        options.Rules.SieveFile = Path.Combine(root, "config", "sieve.yaml");
        options.Rules.JurisdictionsFile = Path.Combine(root, "config", "jurisdictions.yaml");
        options.Rewrite.PromptFile = Path.Combine(root, "config", "rewrite.yaml");
        options.Rewrite.UseMock = true;
    }

    /// <summary>The Slovak fixture e-shop, served under the domain of the test.</summary>
    public static FileSystemPageFetcher SlovakSite(Uri baseUrl) => new(Path.Combine(AppContext.BaseDirectory, "Fixtures", "site-sk"), baseUrl);

    /// <summary>The Czech fixture e-shop.</summary>
    public static FileSystemPageFetcher CzechSite(Uri baseUrl) => new(Path.Combine(AppContext.BaseDirectory, "Fixtures", "site"), baseUrl);

    /// <summary>Settings of a worker of the tests: every class has slots, no tenant caps, files in a temporary folder.</summary>
    public static Dictionary<string, string?> WorkerSettings(string storageRoot, int slots = 2) => new()
    {
        ["Worker:Slots:Fetch"] = slots.ToString(CultureInfo.InvariantCulture),
        ["Worker:Slots:Cpu"] = slots.ToString(CultureInfo.InvariantCulture),
        ["Worker:Slots:Jev"] = slots.ToString(CultureInfo.InvariantCulture),
        ["Worker:Slots:Llm"] = slots.ToString(CultureInfo.InvariantCulture),
        ["Worker:Slots:System"] = slots.ToString(CultureInfo.InvariantCulture),
        ["Storage:Provider"] = "FileSystem",
        ["Storage:FileSystem:Root"] = storageRoot,
        ["Runs:FullAnalysis:MaxPages"] = "200",
        ["Runs:FullAnalysis:SampleProducts"] = "100",
        ["Runs:UsageFlushSeconds"] = "0.5",
    };

    /// <summary>The services of the runs in a worker of the tests, with the fixture, the Jev of the tests and the ownership allowed.</summary>
    public static Action<IServiceCollection> Services(IPageFetcher fetcher, IJevClient jev) => services =>
    {
        services.AddSingleton(fetcher);
        services.AddSingleton(jev);
        services.AddSingleton<IShopOwnershipPolicy, AllowOwnershipPolicy>();
        services.AddEshopGuardStorage();
        services.AddAnalysisRuns(Configure);
    };

    /// <summary>A new tenant with an e-shop on a domain of its own (a free sample is once per domain).</summary>
    public static async Task<RunShop> CreateShopAsync(string? domain = null, string homeCountry = "sk")
    {
        domain ??= $"shop-{Guid.NewGuid():N}"[..17] + ".test";
        Guid tenantId;
        await using (var global = JobsTestDatabase.CreateDb("App", new TenantContext()))
        {
            var tenant = new Tenant { Name = "Runs " + domain, CountryCode = "SK", Locale = "sk", MarketCode = "sk", Status = TenantStatus.Active };
            global.Add(tenant);
            await global.SaveChangesAsync();
            tenantId = tenant.Id;
        }

        var shop = new Shop
        {
            Domain = domain, BaseUrl = $"http://{domain}/", BasePath = "/", HomeCountry = homeCountry,
            Platform = ShopPlatform.Unknown, SourceMode = ShopSourceMode.Web, Status = ShopStatus.Draft, Modules = [],
        };
        var context = new TenantContext();
        context.Set(tenantId);
        await using var db = JobsTestDatabase.CreateDb("App", context);
        db.Add(shop);
        await db.ExecuteInTenantTransactionAsync(() => db.SaveChangesAsync());
        return new RunShop(tenantId, shop.Id, domain, new Uri(shop.BaseUrl));
    }

    /// <summary>Calls the run service as the tenant (a scope of the worker's container).</summary>
    public static async Task<RunServiceResult> ServiceAsync(IServiceProvider services, Guid tenantId, Func<IRunService, Task<RunServiceResult>> call)
    {
        await using var scope = services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenantId);
        return await call(scope.ServiceProvider.GetRequiredService<IRunService>());
    }

    /// <summary>Rows of a query in a transaction of the tenant (tables of tenants have RLS even for their owner).</summary>
    public static async Task<List<object?[]>> RowsAsync(JobsTestDatabase db, Guid tenantId, string sql, params object?[] parameters)
    {
        await using var connection = await db.For("Owner").OpenConnectionAsync();
        await using var transaction = await TenantSql.BeginAsync(connection, tenantId);
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        foreach (var parameter in parameters)
        {
            command.Parameters.Add(new NpgsqlParameter { Value = parameter ?? DBNull.Value });
        }

        var rows = new List<object?[]>();
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var row = new object?[reader.FieldCount];
                reader.GetValues(row!);
                rows.Add([.. row.Select(v => v is DBNull ? null : v)]);
            }
        }

        await transaction.CommitAsync();
        return rows;
    }

    /// <summary>The first column of the first row, in a transaction of the tenant.</summary>
    public static async Task<T> ScalarAsync<T>(JobsTestDatabase db, Guid tenantId, string sql, params object?[] parameters)
    {
        var rows = await RowsAsync(db, tenantId, sql, parameters);
        return rows.Count == 0 || rows[0][0] is null ? default! : (T)rows[0][0]!;
    }

    /// <summary>Waits until the run is in one of the states (or fails the test with the queue).</summary>
    public static async Task<string> WaitForStatusAsync(JobsTestDatabase db, RunShop shop, Guid runId, TimeSpan timeout, params string[] statuses) =>
        await WaitForStatusAsync(db, shop, runId, timeout, [], statuses);

    /// <summary>As above; a failure shows the warnings and errors of the workers too.</summary>
    public static async Task<string> WaitForStatusAsync(JobsTestDatabase db, RunShop shop, Guid runId, TimeSpan timeout, IReadOnlyList<TestWorker> workers, params string[] statuses)
    {
        var deadline = DateTime.UtcNow + timeout;
        string? status = null;
        while (DateTime.UtcNow < deadline)
        {
            status = await ScalarAsync<string>(db, shop.TenantId, "SELECT status FROM checks.runs WHERE id = $1", runId);
            if (statuses.Contains(status))
            {
                return status;
            }

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }

        var logs = workers.SelectMany(w => w.Logs.Logs).Where(l => l.Level >= Microsoft.Extensions.Logging.LogLevel.Warning)
            .OrderByDescending(l => l.Level).Select(l => l.AllText).Take(20);
        Assert.Fail($"Run {runId} is {status}, expected {string.Join("/", statuses)}.\n{await db.DumpJobsAsync()}\n{string.Join("\n---\n", logs)}");
        return status!;
    }

    /// <summary>The final states.</summary>
    public static readonly string[] Final = ["finished", "partial", "failed", "canceled"];

    /// <summary>A JSON column of the run.</summary>
    public static async Task<JsonObject> RunJsonAsync(JobsTestDatabase db, RunShop shop, Guid runId, string column) =>
        JsonNode.Parse(await ScalarAsync<string>(db, shop.TenantId, $"SELECT coalesce({column}, '{{}}'::jsonb)::text FROM checks.runs WHERE id = $1", runId))!.AsObject();

    /// <summary>The statuses of the run in the order of its events.</summary>
    public static async Task<List<string>> StatusTrailAsync(JobsTestDatabase db, RunShop shop, Guid runId) =>
        (await RowsAsync(db, shop.TenantId, "SELECT data ->> 'to' FROM checks.run_events WHERE run_id = $1 AND code = 'run.status' ORDER BY id", runId)).Select(r => (string)r[0]!).ToList();
}
