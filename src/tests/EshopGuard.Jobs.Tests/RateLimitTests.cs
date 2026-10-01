using EshopGuard.Jobs.Queue;
using EshopGuard.Jobs.Workers;

namespace EshopGuard.Jobs.Tests;

public sealed class RateLimitTests(JobsTestDatabase db) : JobsTestBase(db)
{
    private Task CreateBucketAsync(string key, double capacity, double refill, double? share) => Db.ExecuteAsync("Owner",
        "INSERT INTO ops.rate_limit_buckets (key, capacity, tokens, refill_per_sec, reserved) VALUES ($1, $2, $2, $3, $4::jsonb)",
        key, capacity, refill, share is { } s ? $"{{\"p0_p1_share\": {s.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}" : null);

    [Fact]
    public async Task ReservedShare_IsLeftForP0AndP1()
    {
        await CreateBucketAsync("test", 100, 10, 0.2);

        Assert.IsType<RateLimitReservation.Granted>(await Direct.Store.TryReserveAsync("test", 80, JobPriority.P2, Ct));
        var denied = Assert.IsType<RateLimitReservation.Denied>(await Direct.Store.TryReserveAsync("test", 1, JobPriority.P2, Ct));
        Assert.True(denied.RetryAfter > TimeSpan.Zero);
        Assert.IsType<RateLimitReservation.Granted>(await Direct.Store.TryReserveAsync("test", 20, JobPriority.P0, Ct));
    }

    [Fact]
    public async Task ConcurrentReservations_GrantExactlyTheCapacity()
    {
        await CreateBucketAsync("test", 100, 0, null);

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        {
            var granted = 0;
            for (var i = 0; i < 5; i++)
            {
                if (await Direct.Store.TryReserveAsync("test", 10, JobPriority.P2) is RateLimitReservation.Granted)
                {
                    granted++;
                }
            }

            return granted;
        }));

        Assert.Equal(10, results.Sum());
        Assert.Equal(0d, await Db.ScalarAsync<double>("SELECT tokens FROM ops.rate_limit_buckets WHERE key = 'test'"));
    }

    [Fact]
    public async Task MissingBucket_Throws()
    {
        var ex = await Assert.ThrowsAsync<RateLimitBucketMissingException>(() => Direct.Store.TryReserveAsync("missing.bucket", 5, JobPriority.P2, Ct));

        Assert.Equal(JobErrorCodes.RateLimitBucketMissing, ex.Code);
        await Assert.ThrowsAsync<RateLimitBucketMissingException>(() => Direct.Store.ReturnTokensAsync("missing.bucket", 5, Ct));
    }

    [Fact]
    public async Task BatchThatCouldNeverBeGranted_Throws()
    {
        await CreateBucketAsync("test", 100, 10, 0.2);

        var p2 = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Direct.Store.TryReserveAsync("test", 90, JobPriority.P2, Ct));
        var p0 = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Direct.Store.TryReserveAsync("test", 101, JobPriority.P0, Ct));

        Assert.StartsWith(JobErrorCodes.RateLimitBatchTooLarge, p2.Message, StringComparison.Ordinal);
        Assert.StartsWith(JobErrorCodes.RateLimitBatchTooLarge, p0.Message, StringComparison.Ordinal);
        Assert.IsType<RateLimitReservation.Granted>(await Direct.Store.TryReserveAsync("test", 80, JobPriority.P2, Ct));
    }

    [Fact]
    public async Task ReturnedTokens_NeverExceedTheCapacity()
    {
        await CreateBucketAsync("test", 100, 0, null);
        await Direct.Store.TryReserveAsync("test", 30, JobPriority.P2, Ct);

        await Direct.Store.ReturnTokensAsync("test", 10, Ct);
        Assert.Equal(80d, await Db.ScalarAsync<double>("SELECT tokens FROM ops.rate_limit_buckets WHERE key = 'test'"));
        await Direct.Store.ReturnTokensAsync("test", 50, Ct);
        Assert.Equal(100d, await Db.ScalarAsync<double>("SELECT tokens FROM ops.rate_limit_buckets WHERE key = 'test'"));
    }

    [Fact]
    public async Task Tokens_RefillWithTime()
    {
        await CreateBucketAsync("test", 100, 10, null);
        await Db.ExecuteAsync("Owner", "UPDATE ops.rate_limit_buckets SET tokens = 0, updated_at = clock_timestamp() WHERE key = 'test'");

        await Task.Delay(1000, Ct);
        var granted = Assert.IsType<RateLimitReservation.Granted>(await Direct.Store.TryReserveAsync("test", 1, JobPriority.P0, Ct));

        Assert.InRange(granted.Remaining + 1, 9, 11);
    }

    [Fact]
    public async Task Migration_SeedsTheJevAndOpenAiBuckets()
    {
        var rows = await Db.RowsAsync("SELECT key, capacity, refill_per_sec, reserved ->> 'p0_p1_share' FROM ops.rate_limit_buckets ORDER BY key");

        Assert.Equal(3, rows.Count);
        Assert.Equal(["jev", 1200d, 20d, "0.2"], rows[0]);
        Assert.Equal("openai", rows[1][0]);
        Assert.Equal(10_000d, Math.Round((double)rows[1][2]! * 60));
        Assert.Equal("openai:tokens", rows[2][0]);
        Assert.Equal(4_000_000d, Math.Round((double)rows[2][2]! * 60));
    }
}
