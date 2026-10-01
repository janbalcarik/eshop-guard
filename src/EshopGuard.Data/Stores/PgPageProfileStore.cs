using System.Globalization;
using System.Text.Json;
using EshopGuard.Core.Profiles;
using EshopGuard.Core.Storage;
using EshopGuard.Data.Connections;
using EshopGuard.Data.Tenancy;
using Npgsql;
using NpgsqlTypes;

namespace EshopGuard.Data.Stores;

/// <summary>
/// Profiles of page templates of a tenant in <c>shop.page_profiles</c>, per e-shop. In a job the e-shop is the job's
/// (<see cref="IStoreTenant.ShopId"/>); in the CLI it is found by the site key (host without "www.") and created when
/// missing. A profile is never replaced: its number is taken from its id (<c>vegis.sk#2</c>).
/// </summary>
public sealed class PgPageProfileStore(EshopGuardDataSource dataSource, IStoreTenant tenant) : IPageProfileStore
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public async Task<IReadOnlyList<PageProfile>> GetAsync(string site, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(site);
        await using var connection = await dataSource.Source.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = await TenantSql.BeginAsync(connection, tenant.TenantId, ct: ct).ConfigureAwait(false);
        var shopId = await FindShopAsync(connection, transaction, site, ct).ConfigureAwait(false);
        var profiles = new List<PageProfile>();
        if (shopId is { } shop)
        {
            await using var command = new NpgsqlCommand(
                """
                SELECT number, prompt_version, model, regions::text, sample_urls::text, created_at
                FROM shop.page_profiles
                WHERE tenant_id = $1 AND shop_id = $2 AND retired_at IS NULL
                ORDER BY created_at, number
                """, connection, transaction)
            {
                Parameters = { new NpgsqlParameter { Value = tenant.TenantId }, new NpgsqlParameter { Value = shop } },
            };
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                var model = reader.GetString(2);
                profiles.Add(new PageProfile
                {
                    Id = $"{site}#{reader.GetInt32(0).ToString(CultureInfo.InvariantCulture)}",
                    Site = site,
                    PromptVersion = reader.GetString(1),
                    Model = model.Length == 0 ? null : model,
                    Regions = JsonSerializer.Deserialize<List<ProfileRegion>>(reader.GetString(3), Json) ?? [],
                    SampleUrls = JsonSerializer.Deserialize<List<string>>(reader.GetString(4), Json) ?? [],
                    CreatedAt = reader.GetFieldValue<DateTimeOffset>(5),
                });
            }
        }

        await transaction.CommitAsync(ct).ConfigureAwait(false);
        return profiles;
    }

    public async Task AddAsync(PageProfile profile, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var hash = profile.Id.LastIndexOf('#');
        if (hash < 0 || !int.TryParse(profile.Id.AsSpan(hash + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var number))
        {
            throw new ArgumentException($"Profile id {profile.Id} has no number after '#'.", nameof(profile));
        }

        await using var connection = await dataSource.Source.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = await TenantSql.BeginAsync(connection, tenant.TenantId, ct: ct).ConfigureAwait(false);
        var shopId = await FindShopAsync(connection, transaction, profile.Site, ct).ConfigureAwait(false)
            ?? await CreateShopAsync(connection, transaction, profile.Site, ct).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO shop.page_profiles (tenant_id, shop_id, number, prompt_version, model, regions, sample_urls, created_at, updated_at)
            VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $8)
            ON CONFLICT (shop_id, number) DO NOTHING
            """, connection, transaction)
        {
            Parameters =
            {
                new NpgsqlParameter { Value = tenant.TenantId },
                new NpgsqlParameter { Value = shopId },
                new NpgsqlParameter { Value = number },
                new NpgsqlParameter { Value = profile.PromptVersion },
                new NpgsqlParameter { Value = profile.Model ?? "" },
                new NpgsqlParameter { Value = JsonSerializer.Serialize(profile.Regions, Json), NpgsqlDbType = NpgsqlDbType.Jsonb },
                new NpgsqlParameter { Value = JsonSerializer.Serialize(profile.SampleUrls, Json), NpgsqlDbType = NpgsqlDbType.Jsonb },
                new NpgsqlParameter { Value = profile.CreatedAt == default ? DateTimeOffset.UtcNow : profile.CreatedAt },
            },
        };
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
    }

    private async Task<Guid?> FindShopAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string site, CancellationToken ct)
    {
        if (tenant.ShopId is { } shopId)
        {
            return shopId;
        }

        await using var command = new NpgsqlCommand(
            "SELECT id FROM shop.shops WHERE tenant_id = $1 AND domain = $2 AND base_path = '/' AND deleted_at IS NULL", connection, transaction)
        {
            Parameters = { new NpgsqlParameter { Value = tenant.TenantId }, new NpgsqlParameter { Value = site } },
        };
        return await command.ExecuteScalarAsync(ct).ConfigureAwait(false) is Guid id ? id : null;
    }

    /// <summary>The e-shop of a site the CLI scans for the first time (draft data; only its profiles hang on it).</summary>
    private async Task<Guid> CreateShopAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string site, CancellationToken ct)
    {
        await using (var insert = new NpgsqlCommand(
            """
            INSERT INTO shop.shops (tenant_id, domain, base_url, base_path, home_country, platform, source_mode, status)
            VALUES ($1, $2, $3, '/', 'SK', 'unknown', 'web', 'active')
            ON CONFLICT (tenant_id, domain, base_path) WHERE deleted_at IS NULL DO NOTHING
            """, connection, transaction)
        {
            Parameters =
            {
                new NpgsqlParameter { Value = tenant.TenantId },
                new NpgsqlParameter { Value = site },
                new NpgsqlParameter { Value = $"https://{site}/" },
            },
        })
        {
            await insert.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        return await FindShopAsync(connection, transaction, site, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"The e-shop {site} was not created.");
    }
}
