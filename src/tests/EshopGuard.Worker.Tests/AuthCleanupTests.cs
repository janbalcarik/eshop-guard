using System.Text.Json;
using EshopGuard.Data.Entities.Ops;
using EshopGuard.Jobs.Maintenance;
using EshopGuard.Jobs.Queue;
using EshopGuard.Tests.Shared;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace EshopGuard.Worker.Tests;

/// <summary>The daily <c>auth.cleanup</c> (change 9, task 4.6): old expired tokens and full idle buckets of the sign-in go.</summary>
[Trait("Category", "Db")]
public sealed class AuthCleanupTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Cleanup_DeletesTokensExpiredOver30DaysAgo_AndFullIdleAuthBuckets_Only()
    {
        await TestDatabase.EnsureMigratedAsync(Ct);
        var id = Guid.NewGuid().ToString("N");
        var oldToken = Guid.CreateVersion7();
        var recentToken = Guid.CreateVersion7();
        await AdminAsync(
            "INSERT INTO iam.user_tokens (id, email, purpose, token_hash, expires_at, created_at, updated_at) VALUES " +
            "($1, $3, 'magic_link', decode(md5($1::text), 'hex'), now() - interval '31 days', now() - interval '31 days', now()), " +
            "($2, $3, 'magic_link', decode(md5($2::text), 'hex'), now() - interval '1 day', now() - interval '1 day', now())",
            oldToken, recentToken, $"cleanup.{id}@bylinkovo-test.sk");
        await AdminAsync(
            "INSERT INTO ops.rate_limit_buckets (key, capacity, tokens, refill_per_sec, created_at, updated_at) VALUES " +
            "($1, 5, 5, 0.001, now() - interval '3 hours', now() - interval '3 hours'), " +
            "($2, 5, 0, 0, now() - interval '3 hours', now() - interval '3 hours'), " +
            "($3, 5, 5, 0.001, now(), now()), " +
            "($4, 5, 5, 0.001, now() - interval '3 hours', now() - interval '3 hours')",
            $"auth:test:{id}:full-idle", $"auth:test:{id}:empty", $"auth:test:{id}:full-recent", $"other:test:{id}");

        var logs = new InMemoryLoggerProvider();
        using var host = WorkerTestHost.Build(logs, new Dictionary<string, string?>
        {
            ["ConnectionStrings:Worker"] = TestConfiguration.ConnectionString("Worker"),
            ["Worker:Id"] = "auth-cleanup-" + id,
            ["Worker:Slots:System"] = "1",
            ["Worker:MinPollMilliseconds"] = "100",
            ["Worker:MaxPollSeconds"] = "0.5",
        });
        await host.StartAsync(Ct);
        try
        {
            await using (var connection = new NpgsqlConnection(TestConfiguration.ConnectionString("Worker")))
            {
                await connection.OpenAsync(Ct);
                await using var transaction = await connection.BeginTransactionAsync(Ct);
                await host.Services.GetRequiredService<IJobQueue>().EnqueueAsync(new JobRequest(
                    AuthCleanupHandler.JobKind, JobResourceClass.System, JobPriority.P4, JsonDocument.Parse("{}"), DedupeKey: "auth.cleanup:test:" + id), transaction, Ct);
                await transaction.CommitAsync(Ct);
            }

            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
            while (await CountAsync("SELECT count(*) FROM ops.rate_limit_buckets WHERE key = $1", $"auth:test:{id}:full-idle") > 0)
            {
                Assert.True(DateTime.UtcNow < deadline, "auth.cleanup did not run within 30 s.");
                await Task.Delay(100, Ct);
            }

            Assert.Equal(0, await CountAsync("SELECT count(*) FROM iam.user_tokens WHERE id = $1", oldToken));
            Assert.Equal(1, await CountAsync("SELECT count(*) FROM iam.user_tokens WHERE id = $1", recentToken));
            Assert.Equal(3, await CountAsync("SELECT count(*) FROM ops.rate_limit_buckets WHERE key LIKE '%' || $1 || '%'", id));
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
            await AdminAsync("DELETE FROM ops.rate_limit_buckets WHERE key LIKE '%' || $1 || '%'", id);
        }
    }

    private static async Task AdminAsync(string sql, params object[] parameters)
    {
        await using var connection = new NpgsqlConnection(TestConfiguration.ConnectionString("Admin"));
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var parameter in parameters)
        {
            command.Parameters.Add(new NpgsqlParameter { Value = parameter });
        }

        await command.ExecuteNonQueryAsync(Ct);
    }

    private static async Task<long> CountAsync(string sql, object parameter)
    {
        await using var connection = new NpgsqlConnection(TestConfiguration.ConnectionString("Admin"));
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection) { Parameters = { new NpgsqlParameter { Value = parameter } } };
        return (long)(await command.ExecuteScalarAsync(Ct))!;
    }
}
