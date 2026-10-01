using EshopGuard.Data.Connections;
using EshopGuard.Jobs.Queue;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace EshopGuard.Jobs.Processing;

/// <summary>
/// <c>LISTEN eshopguard_jobs</c> on a dedicated connection (<c>ConnectionStrings:WorkerListen</c>, otherwise
/// <c>ConnectionStrings:Worker</c>; never through PgBouncer in transaction mode) and wakes the loop of the notified class.
/// Only latency depends on it: without notifications the loops poll. A broken connection is reopened with a growing pause.
/// </summary>
public sealed class JobNotificationListener(IConfiguration configuration, JobNotificationHub hub, ILogger<JobNotificationListener> logger) : BackgroundService
{
    /// <summary>Optional connection string for the listening connection.</summary>
    public const string ListenConnectionName = "WorkerListen";

    private static readonly TimeSpan MaxReconnectDelay = TimeSpan.FromSeconds(30);
    private const int KeepAliveMilliseconds = 30_000;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var delay = TimeSpan.FromSeconds(1);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var connection = new NpgsqlConnection(ConnectionString());
                await connection.OpenAsync(stoppingToken).ConfigureAwait(false);
                connection.Notification += (_, e) => hub.Publish(e.Payload);
                await using (var listen = new NpgsqlCommand("LISTEN " + JobNames.NotificationChannel, connection))
                {
                    await listen.ExecuteNonQueryAsync(stoppingToken).ConfigureAwait(false);
                }

                logger.LogInformation("jobs.listening");
                delay = TimeSpan.FromSeconds(1);
                hub.PublishAll();
                while (!stoppingToken.IsCancellationRequested)
                {
                    if (!await connection.WaitAsync(KeepAliveMilliseconds, stoppingToken).ConfigureAwait(false))
                    {
                        // Nothing for a while: check the connection is alive (a silently dropped connection never notifies).
                        await using var ping = new NpgsqlCommand("SELECT 1", connection);
                        await ping.ExecuteScalarAsync(stoppingToken).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning("jobs.listen_failed {ExceptionType} {RetryInSeconds}", ex.GetType().Name, delay.TotalSeconds);
                try
                {
                    await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                delay = delay * 2 < MaxReconnectDelay ? delay * 2 : MaxReconnectDelay;
            }
        }
    }

    private string ConnectionString()
    {
        var configured = configuration.GetConnectionString(ListenConnectionName);
        var builder = new NpgsqlConnectionStringBuilder(string.IsNullOrWhiteSpace(configured)
            ? EshopGuardDataSource.BuildConnectionString(configuration, DatabaseRole.Worker)
            : configured)
        {
            Pooling = false,
            ApplicationName = "eshopguard-worker-listen",
            IncludeErrorDetail = false,
        };
        return builder.ConnectionString;
    }
}
