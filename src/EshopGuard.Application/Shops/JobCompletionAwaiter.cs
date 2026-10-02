using EshopGuard.Data.Connections;
using Microsoft.Extensions.Options;

namespace EshopGuard.Application.Shops;

/// <summary>Waits for an interactive job of the worker (recognition of the platform, check of ownership).</summary>
public interface IJobCompletionAwaiter
{
    /// <summary>
    /// Reads the row of <c>ops.jobs</c> every 250 ms for at most <c>Api:InteractiveWaitSeconds</c>; true when the job ended
    /// (<c>succeeded</c>, <c>failed</c> or <c>canceled</c>), false when it still waits or runs.
    /// </summary>
    Task<bool> WaitAsync(long jobId, CancellationToken ct);
}

/// <inheritdoc />
public sealed class JobCompletionAwaiter(EshopGuardDataSource dataSource, IOptions<InteractiveOptions> options) : IJobCompletionAwaiter
{
    public static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(250);

    public async Task<bool> WaitAsync(long jobId, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(options.Value.InteractiveWaitSeconds);
        while (true)
        {
            if (await IsFinishedAsync(jobId, ct).ConfigureAwait(false))
            {
                return true;
            }

            if (DateTime.UtcNow + Interval > deadline)
            {
                return false;
            }

            await Task.Delay(Interval, ct).ConfigureAwait(false);
        }
    }

    private async Task<bool> IsFinishedAsync(long jobId, CancellationToken ct)
    {
        // ops.jobs has no RLS: the id comes from a row of the tenant the caller has already read.
        await using var command = dataSource.Source.CreateCommand("SELECT state FROM ops.jobs WHERE id = $1");
        command.Parameters.Add(new Npgsql.NpgsqlParameter { Value = jobId });
        var state = await command.ExecuteScalarAsync(ct).ConfigureAwait(false) as string;
        return state is null or "succeeded" or "failed" or "canceled";
    }
}
