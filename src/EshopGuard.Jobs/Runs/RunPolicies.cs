using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Billing;
using Npgsql;

namespace EshopGuard.Jobs.Runs;

/// <summary>
/// Whether the tenant may run a full analysis of the e-shop (verified ownership, change 10). Returns null when allowed,
/// otherwise the code of the reason.
/// </summary>
public interface IShopOwnershipPolicy
{
    Task<string?> CheckAsync(Guid tenantId, Guid shopId, CancellationToken ct);
}

/// <summary>Default of the host until change 10 delivers the verification: nothing is allowed (fail-closed, K rozhodnutí 16).</summary>
public sealed class DenyAllOwnershipPolicy : IShopOwnershipPolicy
{
    public Task<string?> CheckAsync(Guid tenantId, Guid shopId, CancellationToken ct) => Task.FromResult<string?>(RunCodes.OwnershipNotVerified);
}

/// <summary>Jurisdictions and modules of a run of an e-shop.</summary>
public sealed record RunScopeChoice(IReadOnlyList<string> Jurisdictions, IReadOnlyList<string> Modules);

/// <summary>
/// Which jurisdictions and modules a run checks (change 10 implements it over <c>ShopScopeCalculator</c> and the markets the
/// client confirmed; K rozhodnutí 16). The free sample asks with the markets its analysis suggested.
/// </summary>
public interface IRunScopeResolver
{
    /// <param name="suggestedMarkets">Markets suggested by the analysis of the sample; null for a full analysis (the confirmed ones).</param>
    Task<RunScopeChoice> ResolveAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid shopId, IReadOnlyList<string>? suggestedMarkets, CancellationToken ct);
}

/// <summary>
/// Default until change 10: the free sample checks the markets its analysis suggested; a full analysis checks the active and
/// suggested supported markets of <c>shop.shop_markets</c>, otherwise the home country of the e-shop. Modules: those of the
/// e-shop, otherwise <c>Runs:DefaultModules</c> (empty = every enabled module, as the CLI).
/// </summary>
public sealed class ShopMarketsScopeResolver(Microsoft.Extensions.Options.IOptions<RunsOptions> options) : IRunScopeResolver
{
    public async Task<RunScopeChoice> ResolveAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid shopId, IReadOnlyList<string>? suggestedMarkets, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT s.modules, s.home_country,
                   coalesce(array_agg(m.country_code ORDER BY m.is_home DESC, m.country_code) FILTER (WHERE m.status IN ('active', 'suggested')), '{}')
            FROM shop.shops s LEFT JOIN shop.shop_markets m ON m.shop_id = s.id
            WHERE s.id = $1
            GROUP BY s.modules, s.home_country
            """, connection, transaction)
        {
            Parameters = { new NpgsqlParameter { Value = shopId } },
        };
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            return new RunScopeChoice([], options.Value.DefaultModules);
        }

        var modules = reader.GetFieldValue<string[]>(0);
        var home = reader.GetString(1).ToLowerInvariant();
        var markets = reader.GetFieldValue<string[]>(2).Select(c => c.ToLowerInvariant()).ToList();
        var jurisdictions = suggestedMarkets is { Count: > 0 } ? suggestedMarkets.ToList() : markets.Count > 0 ? markets : [home];
        return new RunScopeChoice(Normalize(jurisdictions), modules.Length > 0 ? modules : options.Value.DefaultModules);
    }

    /// <summary>Country codes are stored as ISO (<c>SK</c>, <c>CZ</c>); jurisdictions are the codes of <c>jurisdictions.yaml</c> (<c>sk</c>, <c>cz</c>).</summary>
    private static List<string> Normalize(IEnumerable<string> codes) =>
        codes.Select(c => c.ToLowerInvariant()).Distinct(StringComparer.Ordinal).ToList();
}

/// <summary>Whether a full analysis may start downloading: its order is paid, or an administrator approved it (pilot).</summary>
public interface IRunPaymentGate
{
    Task<bool> IsPaidAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid runId, CancellationToken ct);
}

/// <summary>
/// Default of the gate (change 12 may replace it): <c>billing.orders</c> with the run and <c>status = 'paid'</c>, or the
/// approval <c>run.approved_without_payment</c> in <c>ops.audit_log</c>.
/// </summary>
public sealed class OrderTablePaymentGate : IRunPaymentGate
{
    /// <summary>Action of the audit record of an approval without payment.</summary>
    public const string ApprovalAction = "run.approved_without_payment";

    public async Task<bool> IsPaidAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid runId, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT EXISTS (SELECT 1 FROM billing.orders WHERE run_id = $1 AND status = $2)
                OR EXISTS (SELECT 1 FROM ops.audit_log WHERE action = $3 AND entity_type = 'run' AND entity_id = $1::text)
            """, connection, transaction)
        {
            Parameters =
            {
                new NpgsqlParameter { Value = runId },
                new NpgsqlParameter { Value = SnakeCaseEnumConverter<OrderStatus>.ToText(OrderStatus.Paid) },
                new NpgsqlParameter { Value = ApprovalAction },
            },
        };
        return (bool)(await command.ExecuteScalarAsync(ct).ConfigureAwait(false))!;
    }
}
