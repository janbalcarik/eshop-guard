using EshopGuard.Application.Security;
using EshopGuard.Data.Tenancy;
using EshopGuard.Tests.Shared;
using Npgsql;

namespace EshopGuard.Api.Tests.Identity;

/// <summary>
/// The policies of migration F5 as <c>eshopguard_app</c> (change 9, task 2.6): in a transaction of a user without a tenant
/// (<c>app.tenant_id</c> = nil uuid) he reads only his memberships, an invitation only by the hash of its token, and the audit
/// takes events without a tenant only for <c>auth.*</c> and <c>user.*</c>; a transaction of a tenant sees only its tenant.
/// </summary>
public sealed class IdentityPoliciesTests : ApiTestBase
{
    [Fact]
    public async Task UserReadsOnlyHisMemberships_AndATenantTransactionOnlyItsTenant()
    {
        await using var factory = Factory();
        using var jana = await People.OwnerAsync(factory);
        using var peter = await People.OwnerAsync(factory);
        using var janaInPeter = await People.MemberAsync(factory, peter, "viewer");

        var asUser = await AppRowsAsync(TenantSql.NoTenant, jana.UserId, "SELECT tenant_id, user_id FROM iam.memberships");
        Assert.Equal([(jana.TenantId, jana.UserId)], asUser.Select(r => ((Guid)r[0]!, (Guid)r[1]!)));

        var asTenant = await AppRowsAsync(peter.TenantId, janaInPeter.UserId, "SELECT tenant_id FROM iam.memberships");
        Assert.All(asTenant, r => Assert.Equal(peter.TenantId, (Guid)r[0]!));
        Assert.Equal(2, asTenant.Count);

        var anonymous = await AppRowsAsync(TenantSql.NoTenant, null, "SELECT tenant_id FROM iam.memberships");
        Assert.Empty(anonymous);
    }

    [Fact]
    public async Task InvitationIsVisibleOnlyByTheHashOfItsToken()
    {
        await using var factory = Factory();
        using var owner = await People.OwnerAsync(factory);
        var hashes = new List<byte[]>();
        for (var i = 0; i < 2; i++)
        {
            var (_, hash) = OneTimeTokens.Create();
            hashes.Add(hash);
            await AdminAsync(
                "INSERT INTO iam.invitations (id, tenant_id, email, role, token_hash, expires_at, invited_by, created_at, updated_at) VALUES ($1, $2, $3, 'viewer', $4, now() + interval '1 day', $5, now(), now())",
                Guid.CreateVersion7(), owner.TenantId, NewEmail("x"), hash, owner.UserId);
        }

        var rows = await AppRowsAsync(TenantSql.NoTenant, null, "SELECT token_hash FROM iam.invitations", Convert.ToHexStringLower(hashes[0]));

        Assert.Equal([Convert.ToHexStringLower(hashes[0])], rows.Select(r => Convert.ToHexStringLower((byte[])r[0]!)));
        Assert.Empty(await AppRowsAsync(TenantSql.NoTenant, null, "SELECT token_hash FROM iam.invitations"));
    }

    [Fact]
    public async Task AuditWithoutTenant_OnlyForAuthAndUser()
    {
        await TestDatabase.EnsureMigratedAsync(Ct);
        await AppRowsAsync(TenantSql.NoTenant, null,
            "INSERT INTO ops.audit_log (at, tenant_id, actor_kind, action, created_at) VALUES (now(), NULL, 'system', 'auth.test', now())");

        var ex = await Assert.ThrowsAsync<PostgresException>(() => AppRowsAsync(TenantSql.NoTenant, null,
            "INSERT INTO ops.audit_log (at, tenant_id, actor_kind, action, created_at) VALUES (now(), NULL, 'system', 'shop.x', now())"));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, ex.SqlState);
    }

    /// <summary>Rows of a query as <c>eshopguard_app</c> in a transaction with the context set (and the hash of an invitation).</summary>
    private static async Task<List<object?[]>> AppRowsAsync(Guid tenant, Guid? user, string sql, string? invitationHash = null)
    {
        await using var connection = new NpgsqlConnection(TestConfiguration.ConnectionString("App"));
        await connection.OpenAsync(Ct);
        await using var transaction = tenant == TenantSql.NoTenant
            ? await TenantSql.BeginUserAsync(connection, user, Ct)
            : await TenantSql.BeginAsync(connection, tenant, user, Ct);
        if (invitationHash is not null)
        {
            await using var set = new NpgsqlCommand("SELECT set_config('app.invitation_hash', $1, true)", connection, transaction) { Parameters = { new() { Value = invitationHash } } };
            await set.ExecuteNonQueryAsync(Ct);
        }

        var rows = new List<object?[]>();
        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        await using (var reader = await command.ExecuteReaderAsync(Ct))
        {
            while (await reader.ReadAsync(Ct))
            {
                var row = new object[reader.FieldCount];
                reader.GetValues(row);
                rows.Add(row);
            }
        }

        await transaction.CommitAsync(Ct);
        return rows;
    }
}
