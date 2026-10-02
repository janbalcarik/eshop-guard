using System.Globalization;
using EshopGuard.Data.Connections;
using EshopGuard.Data.Stores;
using EshopGuard.Data.Tenancy;
using EshopGuard.Jobs.Runs;
using Npgsql;

namespace EshopGuard.Worker.Dev;

/// <summary>
/// <c>EshopGuard.Worker dev seed-run --tenant &lt;id|cli&gt; --shop-url &lt;url&gt; --kind free_sample|full_analysis [--approve]</c>:
/// creates the e-shop of the tenant when it is missing and a run through <see cref="IRunService"/> (with <c>--approve</c> a full
/// analysis is approved without payment, with an audit record). Only with <c>DOTNET_ENVIRONMENT=Development</c>; the ownership
/// of the e-shop is not verified here (change 10 verifies it in the application).
/// </summary>
public static class SeedRunCommand
{
    /// <summary>The administrator of approvals made by this command (audit).</summary>
    public static readonly Guid DevAdmin = new("00000000-0000-0000-0000-00000000de01");

    public static bool Matches(string[] args) => args.Length >= 2 && args[0] == "dev" && args[1] == "seed-run";

    public static async Task<int> RunAsync(string[] args)
    {
        var options = Parse(args.Skip(2).ToArray());
        if (options is null)
        {
            Console.Error.WriteLine("Použití: dev seed-run --tenant <id|cli> --shop-url <url> --kind free_sample|full_analysis [--approve]");
            return 2;
        }

        var builder = WorkerHost.CreateBuilder(new HostApplicationBuilderSettings { Args = [] });
        if (!builder.Environment.IsDevelopment())
        {
            Console.Error.WriteLine("dev seed-run běží jen s DOTNET_ENVIRONMENT=Development.");
            return 2;
        }

        builder.Services.AddSingleton<IShopOwnershipPolicy, DevOwnershipPolicy>();
        using var host = builder.Build();
        var services = host.Services;
        var dataSource = services.GetRequiredService<EshopGuardDataSource>();
        var tenantId = options.Tenant == "cli" ? CliTenant.Id : Guid.Parse(options.Tenant, CultureInfo.InvariantCulture);
        var shopId = await EnsureShopAsync(dataSource, tenantId, options.ShopUrl);

        await using var scope = services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenantId);
        var runs = scope.ServiceProvider.GetRequiredService<IRunService>();
        var created = options.Kind == "free_sample"
            ? await runs.CreateFreeSampleAsync(shopId, null)
            : await runs.CreateFullAnalysisAsync(shopId, null);
        if (!created.Succeeded)
        {
            Console.Error.WriteLine($"Běh nevznikl: {created.ErrorCode}");
            return 1;
        }

        if (options.Approve && options.Kind == "full_analysis")
        {
            var approved = await runs.ApproveWithoutPaymentAsync(created.RunId!.Value, DevAdmin, "dev seed-run");
            if (!approved.Succeeded)
            {
                Console.Error.WriteLine($"Schválení selhalo: {approved.ErrorCode}");
                return 1;
            }
        }

        Console.WriteLine($"run {created.RunId} shop {shopId} tenant {tenantId}");
        return 0;
    }

    private static async Task<Guid> EnsureShopAsync(EshopGuardDataSource dataSource, Guid tenantId, Uri url)
    {
        var domain = RunService.NormalizeDomain(url.Host);
        await using var connection = await dataSource.Source.OpenConnectionAsync();
        await using var transaction = await TenantSql.BeginAsync(connection, tenantId);
        await using (var insert = new NpgsqlCommand(
            """
            INSERT INTO shop.shops (tenant_id, domain, base_url, base_path, home_country, platform, source_mode, status)
            VALUES ($1, $2, $3, '/', $4, 'unknown', 'web', 'draft')
            ON CONFLICT (tenant_id, domain, base_path) WHERE deleted_at IS NULL DO NOTHING
            """, connection, transaction)
        {
            Parameters =
            {
                new NpgsqlParameter { Value = tenantId },
                new NpgsqlParameter { Value = domain },
                new NpgsqlParameter { Value = url.GetLeftPart(UriPartial.Authority) + "/" },
                new NpgsqlParameter { Value = domain.EndsWith(".cz", StringComparison.Ordinal) ? "cz" : "sk" },
            },
        })
        {
            await insert.ExecuteNonQueryAsync();
        }

        await using var select = new NpgsqlCommand("SELECT id FROM shop.shops WHERE tenant_id = $1 AND domain = $2 AND base_path = '/' AND deleted_at IS NULL", connection, transaction)
        {
            Parameters = { new NpgsqlParameter { Value = tenantId }, new NpgsqlParameter { Value = domain } },
        };
        var id = (Guid)(await select.ExecuteScalarAsync())!;
        await transaction.CommitAsync();
        return id;
    }

    private static SeedOptions? Parse(string[] args)
    {
        string? tenant = null;
        Uri? url = null;
        string? kind = null;
        var approve = false;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--tenant" when i + 1 < args.Length:
                    tenant = args[++i];
                    break;
                case "--shop-url" when i + 1 < args.Length && Uri.TryCreate(args[i + 1], UriKind.Absolute, out var parsed) && parsed.Scheme is "http" or "https":
                    url = parsed;
                    i++;
                    break;
                case "--kind" when i + 1 < args.Length && args[i + 1] is "free_sample" or "full_analysis":
                    kind = args[++i];
                    break;
                case "--approve":
                    approve = true;
                    break;
                default:
                    return null;
            }
        }

        return tenant is null || url is null || kind is null || (tenant != "cli" && !Guid.TryParse(tenant, out _)) ? null : new SeedOptions(tenant, url, kind, approve);
    }

    private sealed record SeedOptions(string Tenant, Uri ShopUrl, string Kind, bool Approve);

    /// <summary>Development only: the developer owns the e-shops of the dev database.</summary>
    private sealed class DevOwnershipPolicy : IShopOwnershipPolicy
    {
        public Task<string?> CheckAsync(Guid tenantId, Guid shopId, CancellationToken ct) => Task.FromResult<string?>(null);
    }
}
