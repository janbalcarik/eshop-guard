using EshopGuard.Data;
using EshopGuard.Data.Tenancy;
using Npgsql;

namespace EshopGuard.Application.Security;

/// <summary>Purpose of a token in <c>iam.user_tokens</c> (<c>purpose</c>).</summary>
public enum OneTimeTokenPurpose
{
    MagicLink,
    Reset,
    Confirm,
}

/// <summary>State of a token presented by a client.</summary>
public enum TokenState
{
    Valid,
    Invalid,
    Expired,
    Used,
}

/// <summary>A row of <c>iam.user_tokens</c> (never the token, the hash stays in the database).</summary>
public sealed record TokenRow(Guid Id, Guid? UserId, string Email, DateTimeOffset ExpiresAt);

/// <summary>
/// Sign-in and reset tokens in <c>iam.user_tokens</c> (AD 2): a new token expires the unused ones of the same e-mail and purpose
/// in the same transaction; <see cref="ConsumeAsync"/> is one <c>UPDATE … RETURNING</c>, so of two clicks only one signs in,
/// and a failed one is told apart as <c>used</c>, <c>expired</c> or <c>invalid</c>. Times come from <see cref="TimeProvider"/>.
/// Runs in the caller's open transaction, or in one of its own.
/// </summary>
public sealed class OneTimeTokenService(EshopGuardDb db, TimeProvider time)
{
    public static string Code(OneTimeTokenPurpose purpose) => purpose switch
    {
        OneTimeTokenPurpose.MagicLink => "magic_link",
        OneTimeTokenPurpose.Reset => "reset",
        _ => "confirm",
    };

    /// <summary>When the last token of the e-mail and purpose was created (the pause between links).</summary>
    public Task<DateTimeOffset?> LastCreatedAsync(string email, OneTimeTokenPurpose purpose, CancellationToken ct) => InTransactionAsync(async () =>
    {
        var last = await DbSql.ScalarAsync<object>(db,
            "SELECT max(created_at) FROM iam.user_tokens WHERE email = @email AND purpose = @purpose", ct,
            DbSql.P("email", email), DbSql.P("purpose", Code(purpose))).ConfigureAwait(false);
        return last is DateTime at ? new DateTimeOffset(DateTime.SpecifyKind(at, DateTimeKind.Utc)) : (DateTimeOffset?)null;
    }, ct);

    /// <summary>Expires the unused tokens of the e-mail and purpose and creates a new one valid for <paramref name="validity"/>.</summary>
    public Task<(string Token, TokenRow Row)> IssueAsync(
        string email, Guid? userId, OneTimeTokenPurpose purpose, TimeSpan validity, byte[]? requestedIpHash, CancellationToken ct) => InTransactionAsync(async () =>
    {
        var now = time.GetUtcNow();
        await DbSql.ExecuteAsync(db,
            "UPDATE iam.user_tokens SET expires_at = @now, updated_at = @now WHERE email = @email AND purpose = @purpose AND used_at IS NULL AND expires_at > @now",
            ct, DbSql.P("now", now), DbSql.P("email", email), DbSql.P("purpose", Code(purpose))).ConfigureAwait(false);
        var (token, hash) = OneTimeTokens.Create();
        var row = new TokenRow(Guid.CreateVersion7(), userId, email, now + validity);
        await DbSql.ExecuteAsync(db,
            """
            INSERT INTO iam.user_tokens (id, user_id, email, purpose, token_hash, expires_at, requested_ip_hash, created_at, updated_at)
            VALUES (@id, @user, @email, @purpose, @hash, @expires, @ip, @now, @now)
            """, ct,
            DbSql.P("id", row.Id), DbSql.P("user", userId), DbSql.P("email", email), DbSql.P("purpose", Code(purpose)), DbSql.P("hash", hash),
            DbSql.P("expires", row.ExpiresAt), DbSql.P("ip", requestedIpHash), DbSql.P("now", now)).ConfigureAwait(false);
        return (token, row);
    }, ct);

    /// <summary>Makes a token unusable (its e-mail could not be sent).</summary>
    public Task ExpireAsync(Guid id, CancellationToken ct) => InTransactionAsync(async () =>
    {
        var now = time.GetUtcNow();
        return await DbSql.ExecuteAsync(db, "UPDATE iam.user_tokens SET expires_at = least(expires_at, @now), updated_at = @now WHERE id = @id", ct,
            DbSql.P("now", now), DbSql.P("id", id)).ConfigureAwait(false);
    }, ct);

    /// <summary>The state of a token without using it.</summary>
    public Task<(TokenState State, TokenRow? Row)> InspectAsync(string? token, OneTimeTokenPurpose purpose, CancellationToken ct)
    {
        var hash = OneTimeTokens.TryHash(token);
        return hash is null ? Task.FromResult<(TokenState, TokenRow?)>((TokenState.Invalid, null)) : InTransactionAsync(() => ClassifyAsync(hash, purpose, ct), ct);
    }

    /// <summary>Uses a valid token atomically; otherwise its state (<c>used</c>, <c>expired</c>, <c>invalid</c>). Joins the open transaction.</summary>
    public Task<(TokenState State, TokenRow? Row)> ConsumeAsync(string? token, OneTimeTokenPurpose purpose, CancellationToken ct)
    {
        var hash = OneTimeTokens.TryHash(token);
        return hash is null ? Task.FromResult<(TokenState, TokenRow?)>((TokenState.Invalid, null)) : InTransactionAsync(async () =>
        {
            var now = time.GetUtcNow();
            await using (var command = DbSql.Command(db,
                """
                UPDATE iam.user_tokens SET used_at = @now, updated_at = @now
                WHERE token_hash = @hash AND purpose = @purpose AND used_at IS NULL AND expires_at > @now
                RETURNING id, user_id, email, expires_at
                """,
                DbSql.P("now", now), DbSql.P("hash", hash), DbSql.P("purpose", Code(purpose))))
            await using (var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false))
            {
                if (await reader.ReadAsync(ct).ConfigureAwait(false))
                {
                    return (TokenState.Valid, Read(reader));
                }
            }

            return await ClassifyAsync(hash, purpose, ct).ConfigureAwait(false);
        }, ct);
    }

    private async Task<(TokenState, TokenRow?)> ClassifyAsync(byte[] hash, OneTimeTokenPurpose purpose, CancellationToken ct)
    {
        await using var command = DbSql.Command(db,
            "SELECT id, user_id, email, expires_at, used_at FROM iam.user_tokens WHERE token_hash = @hash AND purpose = @purpose",
            DbSql.P("hash", hash), DbSql.P("purpose", Code(purpose)));
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            return (TokenState.Invalid, null);
        }

        var row = Read(reader);
        var state = !reader.IsDBNull(4) ? TokenState.Used : row.ExpiresAt <= time.GetUtcNow() ? TokenState.Expired : TokenState.Valid;
        return (state, row);
    }

    private static TokenRow Read(NpgsqlDataReader reader) => new(
        reader.GetGuid(0),
        reader.IsDBNull(1) ? null : reader.GetGuid(1),
        reader.GetString(2),
        new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(3), DateTimeKind.Utc)));

    private Task<T> InTransactionAsync<T>(Func<Task<T>> work, CancellationToken ct) =>
        db.Database.CurrentTransaction is not null ? work() : db.ExecuteInUserTransactionAsync(work, ct);

    private Task InTransactionAsync(Func<Task<int>> work, CancellationToken ct) =>
        db.Database.CurrentTransaction is not null ? work() : db.ExecuteInUserTransactionAsync(work, ct);
}
