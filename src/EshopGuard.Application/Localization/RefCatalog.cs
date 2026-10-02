using EshopGuard.Data.Connections;
using Microsoft.Extensions.Caching.Memory;

namespace EshopGuard.Application.Localization;

/// <summary>A language of the interface (<c>ref.locales</c>).</summary>
public sealed record LocaleInfo(string Code, string Name, string? FallbackCode, bool Enabled);

/// <summary>A market (<c>ref.markets</c>); <see cref="DefaultLocale"/> is the primary subtag (<c>sk-SK</c> → <c>sk</c>).</summary>
public sealed record MarketInfo(
    string Code, string CountryCode, string DefaultLocale, IReadOnlyList<string> UiLocales, string Currency, string WebStatus, string ChecksStatus);

/// <summary>The catalog of languages and markets.</summary>
public interface IRefCatalog
{
    Task<IReadOnlyList<LocaleInfo>> GetLocalesAsync(CancellationToken ct = default);

    Task<IReadOnlyList<MarketInfo>> GetMarketsAsync(CancellationToken ct = default);
}

/// <summary><c>ref.locales</c> and <c>ref.markets</c> held in <see cref="IMemoryCache"/> for 5 minutes (AD 10); global tables, no tenant.</summary>
public sealed class RefCatalog(EshopGuardDataSource dataSource, IMemoryCache cache) : IRefCatalog
{
    public static readonly TimeSpan CacheTime = TimeSpan.FromMinutes(5);
    private const string LocalesKey = "eg:ref:locales";
    private const string MarketsKey = "eg:ref:markets";

    public async Task<IReadOnlyList<LocaleInfo>> GetLocalesAsync(CancellationToken ct = default) =>
        (await cache.GetOrCreateAsync(LocalesKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTime;
            var list = new List<LocaleInfo>();
            await using var command = dataSource.Source.CreateCommand("SELECT code, name, fallback_code, enabled FROM ref.locales ORDER BY code");
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                list.Add(new LocaleInfo(reader.GetString(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2), reader.GetBoolean(3)));
            }

            return (IReadOnlyList<LocaleInfo>)list;
        }).ConfigureAwait(false))!;

    public async Task<IReadOnlyList<MarketInfo>> GetMarketsAsync(CancellationToken ct = default) =>
        (await cache.GetOrCreateAsync(MarketsKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTime;
            var list = new List<MarketInfo>();
            await using var command = dataSource.Source.CreateCommand(
                "SELECT code, country_code, default_locale, ui_locales, currency, web_status, checks_status FROM ref.markets ORDER BY code");
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                list.Add(new MarketInfo(
                    reader.GetString(0), reader.GetString(1), LocaleResolver.Primary(reader.GetString(2)), reader.GetFieldValue<string[]>(3),
                    reader.GetString(4), reader.GetString(5), reader.GetString(6)));
            }

            return (IReadOnlyList<MarketInfo>)list;
        }).ConfigureAwait(false))!;
}
