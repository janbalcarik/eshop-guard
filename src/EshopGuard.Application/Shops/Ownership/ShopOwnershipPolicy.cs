using EshopGuard.Application.Problems;
using EshopGuard.Data.Connections;
using EshopGuard.Data.Tenancy;
using EshopGuard.Jobs.Runs;
using Microsoft.Extensions.Options;
using Npgsql;

namespace EshopGuard.Application.Shops.Ownership;

/// <summary>
/// Whether the ownership of an e-shop must be verified before a step (change 10, AD 9): <c>sample</c> or <c>full_analysis</c>
/// in <c>Shops:Ownership:RequiredBefore</c>. The free sample asks through <see cref="EnsureAsync"/>, the full analysis of
/// change 8 through <see cref="IShopOwnershipPolicy.CheckAsync"/> (the same answer, <c>shop.ownership_not_verified</c>).
/// </summary>
public sealed class ShopOwnershipPolicy(EshopGuardDataSource dataSource, IOptions<OwnershipPolicyOptions> options) : IShopOwnershipPolicy
{
    /// <summary>Whether the gate requires a verified e-shop.</summary>
    public bool Requires(string gate) => options.Value.Requires(gate);

    /// <summary><c>409 shop.ownership_not_verified</c> when the gate requires the verification and the e-shop is not verified.</summary>
    public async Task EnsureAsync(Guid tenantId, Guid shopId, string gate, CancellationToken ct)
    {
        if (Requires(gate) && !await IsVerifiedAsync(tenantId, shopId, ct).ConfigureAwait(false))
        {
            throw new DomainException(ProblemCodes.ShopOwnershipNotVerified, 409, new Dictionary<string, object?> { ["gate"] = gate });
        }
    }

    /// <inheritdoc />
    public async Task<string?> CheckAsync(Guid tenantId, Guid shopId, CancellationToken ct) =>
        Requires(OwnershipPolicyOptions.FullAnalysis) && !await IsVerifiedAsync(tenantId, shopId, ct).ConfigureAwait(false)
            ? RunCodes.OwnershipNotVerified
            : null;

    private async Task<bool> IsVerifiedAsync(Guid tenantId, Guid shopId, CancellationToken ct)
    {
        await using var connection = await dataSource.Source.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = await TenantSql.BeginAsync(connection, tenantId, ct: ct).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(
            "SELECT ownership_verified_at IS NOT NULL FROM shop.shops WHERE id = $1 AND deleted_at IS NULL", connection, transaction)
        {
            Parameters = { new NpgsqlParameter { Value = shopId } },
        };
        var verified = await command.ExecuteScalarAsync(ct).ConfigureAwait(false) is true;
        await transaction.CommitAsync(ct).ConfigureAwait(false);
        return verified;
    }
}
