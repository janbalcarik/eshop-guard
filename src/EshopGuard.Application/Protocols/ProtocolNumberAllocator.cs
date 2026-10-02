using EshopGuard.Application.Options;
using EshopGuard.Data;
using Microsoft.Extensions.Options;

namespace EshopGuard.Application.Protocols;

/// <summary>
/// The number of a protocol, <c>EG-{year}-{order, at least 4 digits}</c>, one series per tenant and year (change 11, AD 11).
/// The year is the one of the day of issue in <c>Localization:TimeZone</c> (31. 12. 23:30 UTC is already 1. 1. in
/// Bratislava). In the transaction of the request: an advisory lock of the tenant and year, then the highest number + 1; the
/// unique index (<c>tenant_id</c>, <c>number</c>) is the safety net.
/// </summary>
public sealed class ProtocolNumberAllocator(EshopGuardDb db, IOptions<LocalizationOptions> options)
{
    public int YearOf(DateTimeOffset issuedAt) => TimeZoneInfo.ConvertTime(issuedAt, TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZone)).Year;

    /// <summary>The next number; call it inside the tenant transaction that inserts the protocol.</summary>
    public async Task<string> NextAsync(Guid tenantId, DateTimeOffset issuedAt, CancellationToken ct)
    {
        var year = YearOf(issuedAt).ToString(System.Globalization.CultureInfo.InvariantCulture);
        await DbSql.ExecuteAsync(db, "SELECT pg_advisory_xact_lock(hashtextextended('protocol:' || @tenant || ':' || @year, 0))", ct,
            DbSql.P("tenant", tenantId.ToString("D")), DbSql.P("year", year)).ConfigureAwait(false);
        var next = await DbSql.ScalarAsync<int>(db,
            "SELECT coalesce(max(substring(number FROM 9)::int), 0) + 1 FROM fixes.protocols WHERE tenant_id = @tenant AND number LIKE 'EG-' || @year || '-%'", ct,
            DbSql.P("tenant", tenantId), DbSql.P("year", year)).ConfigureAwait(false);
        return $"EG-{year}-{next:D4}";
    }
}
