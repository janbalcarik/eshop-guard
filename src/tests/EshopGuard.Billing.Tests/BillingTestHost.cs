using System.Collections.Concurrent;
using EshopGuard.Billing.Stripe;
using EshopGuard.Billing.Subscriptions;
using EshopGuard.Data;
using EshopGuard.Data.Connections;
using EshopGuard.Jobs;
using EshopGuard.Tests.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Npgsql;

namespace EshopGuard.Billing.Tests;

/// <summary>
/// The services of billing as the worker has them (role <c>eshopguard_worker</c>, <c>eshopguard_test</c>), with
/// <see cref="FakeStripeGateway"/> and a fixed time; jobs are only enqueued, the tests call the services and handlers directly.
/// Setup across tenants runs as <c>eshopguard_admin</c>.
/// </summary>
internal sealed class BillingTestHost : IAsyncDisposable
{
    public const string TestKey = "sk_test_EshopGuardBillingTestsOnly00000000000";
    public const string WebhookSecret = "whsec_EshopGuardBillingTestsOnly000000";

    private readonly ServiceProvider provider;

    private BillingTestHost(ServiceProvider provider, FakeStripeGateway stripe, FakeTimeProvider time, InMemoryLoggerProvider logs, FakeCountedProducts products)
    {
        this.provider = provider;
        Stripe = stripe;
        Time = time;
        Logs = logs;
        Products = products;
    }

    public FakeStripeGateway Stripe { get; }

    /// <summary>The counted products of the e-shops (the scope and the analyses are not part of these tests).</summary>
    public FakeCountedProducts Products { get; }

    public FakeTimeProvider Time { get; }

    public InMemoryLoggerProvider Logs { get; }

    public IServiceProvider Services => provider;

    public static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static async Task<BillingTestHost> CreateAsync(DateTimeOffset? now = null, IDictionary<string, string?>? settings = null, Action<IServiceCollection>? configure = null)
    {
        await TestDatabase.EnsureMigratedAsync(Ct);
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Worker"] = TestConfiguration.ConnectionString("Worker"),
            ["Billing:Stripe:Mode"] = "test",
            ["Billing:Stripe:SecretKey"] = TestKey,
            ["Billing:Stripe:WebhookSecret"] = WebhookSecret,
            ["Billing:Tax:SupplierCountry"] = "SK",
            ["Billing:Tax:DomesticVatRate"] = "23",
            ["Frontend:BaseUrl"] = "https://app.eshopguard.test",
        };
        foreach (var (country, index) in BillingOptionsTests.Tax().EuVatCountries.Select((c, i) => (c, i)))
        {
            values[$"Billing:Tax:EuVatCountries:{index}"] = country;
        }

        foreach (var (key, value) in settings ?? new Dictionary<string, string?>())
        {
            values[key] = value;
        }

        var stripe = new FakeStripeGateway();
        var time = new FakeTimeProvider(now ?? DateTimeOffset.UtcNow);
        stripe.Now = time.GetUtcNow();
        var logs = new InMemoryLoggerProvider();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(values).Build());
        services.AddLogging(b => b.AddProvider(logs).SetMinimumLevel(LogLevel.Debug));
        services.AddSingleton<IHostEnvironment>(new TestEnvironment());
        services.AddEshopGuardData(DatabaseRole.Worker);
        services.AddEshopGuardJobQueue();
        services.AddEshopGuardBilling();
        services.AddBillingJobs();
        services.Replace(ServiceDescriptor.Singleton<IStripeGateway>(stripe));
        services.Replace(ServiceDescriptor.Singleton<TimeProvider>(time));
        var products = new FakeCountedProducts();
        services.Replace(ServiceDescriptor.Singleton<ICountedProductsReader>(products));
        configure?.Invoke(services);
        return new BillingTestHost(services.BuildServiceProvider(), stripe, time, logs, products);
    }

    /// <summary>A service of a new scope (as a job of the worker gets it).</summary>
    public async Task<T> RunAsync<T>(Func<IServiceProvider, Task<T>> work)
    {
        await using var scope = provider.CreateAsyncScope();
        return await work(scope.ServiceProvider);
    }

    public Task RunAsync(Func<IServiceProvider, Task> work) => RunAsync(async s =>
    {
        await work(s);
        return true;
    });

    public ValueTask DisposeAsync() => provider.DisposeAsync();

    /// <summary>SQL as <c>eshopguard_admin</c> (RLS bypassed).</summary>
    public static async Task<int> AdminAsync(string sql, params object?[] parameters)
    {
        await using var connection = new NpgsqlConnection(TestConfiguration.ConnectionString("Admin"));
        await connection.OpenAsync(Ct);
        await using var command = Command(connection, sql, parameters);
        return await command.ExecuteNonQueryAsync(Ct);
    }

    public static async Task<T?> AdminScalarAsync<T>(string sql, params object?[] parameters)
    {
        await using var connection = new NpgsqlConnection(TestConfiguration.ConnectionString("Admin"));
        await connection.OpenAsync(Ct);
        await using var command = Command(connection, sql, parameters);
        var value = await command.ExecuteScalarAsync(Ct);
        return value is null or DBNull ? default : (T)value;
    }

    public static async Task<List<object?[]>> AdminRowsAsync(string sql, params object?[] parameters)
    {
        await using var connection = new NpgsqlConnection(TestConfiguration.ConnectionString("Admin"));
        await connection.OpenAsync(Ct);
        await using var command = Command(connection, sql, parameters);
        await using var reader = await command.ExecuteReaderAsync(Ct);
        var rows = new List<object?[]>();
        while (await reader.ReadAsync(Ct))
        {
            var row = new object[reader.FieldCount];
            reader.GetValues(row);
            rows.Add(row.Select(v => v is DBNull ? null : v).ToArray());
        }

        return rows;
    }

    private static NpgsqlCommand Command(NpgsqlConnection connection, string sql, object?[] parameters)
    {
        var command = new NpgsqlCommand(sql, connection);
        foreach (var parameter in parameters)
        {
            command.Parameters.Add(parameter is NpgsqlParameter p ? p : new NpgsqlParameter { Value = parameter ?? DBNull.Value });
        }

        return command;
    }

    internal sealed class FakeCountedProducts : ICountedProductsReader
    {
        private readonly ConcurrentDictionary<Guid, CountedProducts> counts = new();

        public void Set(Guid shopId, int count, bool lowerBound = false) => counts[shopId] = new CountedProducts(count, lowerBound);

        public Task<CountedProducts?> ReadAsync(Guid shopId, CancellationToken ct) =>
            Task.FromResult(counts.TryGetValue(shopId, out var count) ? count : null);
    }

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";

        public string ApplicationName { get; set; } = "EshopGuard.Billing.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
