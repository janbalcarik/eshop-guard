using EshopGuard.Data.Connections;
using EshopGuard.Data.Stores;
using EshopGuard.Data.Tenancy;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Spectre.Console;
using Spectre.Console.Cli;

namespace EshopGuard.Cli.Commands;

/// <summary>
/// <c>eshopguard cache init</c>: creates the tenant <c>cli</c> of the cache in PostgreSQL when it is missing and prints what
/// the cache holds. A scan never creates the tenant, so a foreign database gets nothing without this command.
/// </summary>
internal sealed class CacheInitCommand : AsyncCommand
{
    protected override async Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        CliConfiguration configuration;
        try
        {
            configuration = CliConfiguration.Load();
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
            return 1;
        }

        if (configuration.MissingDatabaseMessage(useMock: false) is { } missing)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(missing)}[/]");
            return 1;
        }

        var services = new ServiceCollection();
        services.AddLogging();
        CliDatabase.Register(services, configuration.CacheConnectionString!, cacheAnswers: true);
        await using var provider = services.BuildServiceProvider();
        if (await CliDatabase.CheckAsync(provider, cancellationToken, tenant: false) is { } error)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(error)}[/]");
            return 1;
        }

        var dataSource = provider.GetRequiredService<EshopGuardDataSource>();
        var existed = await CliTenant.ExistsAsync(dataSource, cancellationToken);
        var tenant = await CliTenant.EnsureAsync(dataSource, cancellationToken);
        var counts = await CountAsync(dataSource, tenant, cancellationToken);
        AnsiConsole.MarkupLine(existed ? $"Tenant cli ({tenant}) už existuje." : $"[green]Tenant cli ({tenant}) založen.[/]");
        AnsiConsole.MarkupLine($"Cache: {counts.Answers} odpovědí Jevu, {counts.Sieve} odpovědí síta, {counts.Rewrites} přepisů, {counts.Profiles} profilů šablon.");
        return 0;
    }

    private static async Task<(long Answers, long Sieve, long Rewrites, long Profiles)> CountAsync(EshopGuardDataSource dataSource, Guid tenant, CancellationToken ct)
    {
        await using var connection = await dataSource.Source.OpenConnectionAsync(ct);
        await using var transaction = await TenantSql.BeginAsync(connection, tenant, ct: ct);
        await using var command = new NpgsqlCommand(
            """
            SELECT (SELECT count(*) FROM checks.jev_answers WHERE tenant_id = $1),
                   (SELECT count(*) FROM checks.sieve_answers WHERE tenant_id = $1),
                   (SELECT count(*) FROM fixes.rewrite_cache WHERE tenant_id = $1),
                   (SELECT count(*) FROM shop.page_profiles WHERE tenant_id = $1)
            """, connection, transaction)
        {
            Parameters = { new NpgsqlParameter { Value = tenant } },
        };
        await using var reader = await command.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);
        return (reader.GetInt64(0), reader.GetInt64(1), reader.GetInt64(2), reader.GetInt64(3));
    }
}
