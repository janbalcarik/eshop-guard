using EshopGuard.Data.Connections;
using EshopGuard.Data.Entities.Ops;
using EshopGuard.Jobs.Processing;
using EshopGuard.Jobs.Queue;
using EshopGuard.Jobs.Scheduling;
using Microsoft.Extensions.Logging;
using NpgsqlTypes;

namespace EshopGuard.Jobs.Maintenance;

/// <summary>
/// <c>auth.cleanup</c> (daily, change 9 AD 5): deletes tokens of links that expired more than 30 days ago and the buckets of
/// the sign-in limits (<c>auth:%</c>) that are full again and untouched for 2 hours. Global tables, no tenant.
/// </summary>
public sealed partial class AuthCleanupHandler(EshopGuardDataSource dataSource, TimeProvider time, ILogger<AuthCleanupHandler> logger) : IJobHandler
{
    public const string JobKind = "auth.cleanup";
    public static readonly TimeSpan TokenAge = TimeSpan.FromDays(30);
    public static readonly TimeSpan BucketAge = TimeSpan.FromHours(2);

    public string Kind => JobKind;

    public JobResourceClass ResourceClass => JobResourceClass.System;

    public async Task<JobResult> ExecuteAsync(JobExecutionContext context, CancellationToken ct)
    {
        var now = time.GetUtcNow().UtcDateTime;
        await using var connection = await dataSource.Source.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var tokens = connection.CreateCommand();
        tokens.CommandText = "DELETE FROM iam.user_tokens WHERE expires_at < $1";
        tokens.Parameters.Add(new() { Value = now - TokenAge, NpgsqlDbType = NpgsqlDbType.TimestampTz });
        var deletedTokens = await tokens.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        await using var buckets = connection.CreateCommand();
        buckets.CommandText = """
            DELETE FROM ops.rate_limit_buckets b
            WHERE b.key LIKE 'auth:%' AND b.updated_at < $1
              AND b.tokens + extract(epoch FROM $2 - b.updated_at) * b.refill_per_sec >= b.capacity
            """;
        buckets.Parameters.Add(new() { Value = now - BucketAge, NpgsqlDbType = NpgsqlDbType.TimestampTz });
        buckets.Parameters.Add(new() { Value = now, NpgsqlDbType = NpgsqlDbType.TimestampTz });
        var deletedBuckets = await buckets.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        LogCleaned(logger, deletedTokens, deletedBuckets);
        return JobResult.Done;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "auth.cleaned {Tokens} {Buckets}")]
    private static partial void LogCleaned(ILogger logger, int tokens, int buckets);
}

/// <summary>Daily <c>auth.cleanup</c>.</summary>
public sealed class AuthCleanupTask(IJobQueue queue) : DailySystemJobTask(queue)
{
    protected override string Kind => AuthCleanupHandler.JobKind;
}
