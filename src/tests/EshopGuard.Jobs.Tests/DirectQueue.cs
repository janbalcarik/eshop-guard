using EshopGuard.Data;
using EshopGuard.Data.Connections;
using EshopGuard.Jobs.Queue;
using EshopGuard.Jobs.Workers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EshopGuard.Jobs.Tests;

/// <summary>Queue and worker store of one role over <c>eshopguard_test_jobs</c>, without any worker running.</summary>
internal sealed class DirectQueue : IAsyncDisposable
{
    private readonly ServiceProvider _provider;

    public DirectQueue(DatabaseRole role, IDictionary<string, string?>? settings = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:" + role.ConnectionStringName()] = JobsTestDatabase.ConnectionString(role.ConnectionStringName()),
                ["Worker:LeaseSeconds"] = "2",
                ["Jobs:Retry:BaseSeconds"] = "1",
                ["Jobs:Retry:MaxSeconds"] = "60",
                ["Jobs:PausedClassesCacheSeconds"] = "0",
            })
            .AddInMemoryCollection(settings ?? new Dictionary<string, string?>())
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddEshopGuardData(role);
        services.AddEshopGuardJobQueue();
        services.AddSingleton<IWorkerStore, PgWorkerStore>();
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    public IJobQueue Queue => _provider.GetRequiredService<IJobQueue>();

    public IWorkerStore Store => _provider.GetRequiredService<IWorkerStore>();

    public IServiceProvider Services => _provider;

    /// <summary>Enqueues in an own transaction and commits.</summary>
    public async Task<EnqueueResult> EnqueueAsync(JobRequest request)
    {
        var source = _provider.GetRequiredService<EshopGuardDataSource>().Source;
        await using var connection = await source.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var result = await Queue.EnqueueAsync(request, transaction);
        await transaction.CommitAsync();
        return result;
    }

    /// <summary>Enqueues many jobs in one transaction.</summary>
    public async Task<List<long>> EnqueueManyAsync(IEnumerable<JobRequest> requests)
    {
        var source = _provider.GetRequiredService<EshopGuardDataSource>().Source;
        await using var connection = await source.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var ids = new List<long>();
        foreach (var request in requests)
        {
            ids.Add((await Queue.EnqueueAsync(request, transaction)).JobId);
        }

        await transaction.CommitAsync();
        return ids;
    }

    public ValueTask DisposeAsync() => _provider.DisposeAsync();
}
