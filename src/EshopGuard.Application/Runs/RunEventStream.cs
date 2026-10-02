using System.Collections.Concurrent;
using System.Threading.Channels;
using EshopGuard.Application.Problems;
using EshopGuard.Data.Connections;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace EshopGuard.Application.Runs;

/// <summary>Settings <c>Sse</c> of the live progress.</summary>
public sealed class SseOptions
{
    public const string SectionName = "Sse";

    /// <summary>Open streams of one user at a time on one instance of the API (K rozhodnutí of change 11, proposal 10).</summary>
    public int MaxConnectionsPerUser { get; set; } = 10;

    /// <summary>The comment <c>: ping</c> (and a check of the database for anything missed) after this many seconds of quiet.</summary>
    public int PingSeconds { get; set; } = 15;
}

/// <summary>A notification for a stream: the run (<see cref="RunEventId"/> 0 = its state or progress) or an entity of the e-shop.</summary>
public sealed record StreamSignal(long RunEventId, string? Entity, Guid EntityId);

/// <summary>An open stream of one user: its signals; disposing it frees the place of the user.</summary>
public sealed class StreamSubscription : IDisposable
{
    private readonly Action<StreamSubscription> release;
    private int disposed;

    internal StreamSubscription(Guid tenantId, Guid userId, Guid? runId, Guid? shopId, Action<StreamSubscription> release)
    {
        TenantId = tenantId;
        UserId = userId;
        RunId = runId;
        ShopId = shopId;
        this.release = release;
    }

    public Guid TenantId { get; }

    public Guid UserId { get; }

    public Guid? RunId { get; }

    public Guid? ShopId { get; }

    internal Channel<StreamSignal> Channel { get; } = System.Threading.Channels.Channel.CreateBounded<StreamSignal>(
        new BoundedChannelOptions(10_000) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });

    public ChannelReader<StreamSignal> Signals => Channel.Reader;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0)
        {
            Channel.Writer.TryComplete();
            release(this);
        }
    }
}

/// <summary>
/// The live progress of the API (change 11, AD 13): one connection per instance with <c>LISTEN eg_run, eg_shop</c> (role
/// <c>eshopguard_app</c>), opened with the first stream and opened again after a failure. A notification carries only ids
/// (<c>tenant:run:event</c>, <c>tenant:shop:entity:id</c>); it goes only to the streams of the same tenant and run or e-shop,
/// and every stream reads the rows itself under RLS of its request. At most <see cref="SseOptions.MaxConnectionsPerUser"/>
/// streams of one user (<c>429 sse.too_many_connections</c>).
/// </summary>
public sealed class RunEventStream(EshopGuardDataSource dataSource, IOptions<SseOptions> options, ILogger<RunEventStream> logger) : BackgroundService
{
    private readonly ConcurrentDictionary<StreamSubscription, byte> subscriptions = new();
    private readonly ConcurrentDictionary<Guid, int> perUser = new();
    private readonly TaskCompletionSource firstSubscription = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly object gate = new();
    private TaskCompletionSource listening = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>A stream of a run (<paramref name="runId"/>) or of an e-shop (<paramref name="shopId"/>).</summary>
    public StreamSubscription Subscribe(Guid tenantId, Guid userId, Guid? runId, Guid? shopId)
    {
        var max = options.Value.MaxConnectionsPerUser;
        var count = perUser.AddOrUpdate(userId, 1, (_, n) => n + 1);
        if (count > max)
        {
            perUser.AddOrUpdate(userId, 0, (_, n) => n - 1);
            throw new DomainException(ProblemCodes.SseTooManyConnections, 429, new Dictionary<string, object?> { ["max"] = max });
        }

        var subscription = new StreamSubscription(tenantId, userId, runId, shopId, Release);
        subscriptions[subscription] = 0;
        firstSubscription.TrySetResult();
        return subscription;
    }

    /// <summary>Waits (at most <paramref name="timeout"/>) until the connection listens, so nothing between the snapshot and the first notification is lost.</summary>
    public async Task<bool> WhenListeningAsync(TimeSpan timeout, CancellationToken ct)
    {
        Task task;
        lock (gate)
        {
            task = listening.Task;
        }

        try
        {
            await task.WaitAsync(timeout, ct).ConfigureAwait(false);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    /// <summary>Hands a notification to the streams of its tenant and run or e-shop; anything else is dropped.</summary>
    internal void Dispatch(string channel, string payload)
    {
        var parts = payload.Split(':');
        if (parts.Length < 3 || !Guid.TryParse(parts[0], out var tenantId) || !Guid.TryParse(parts[1], out var target))
        {
            return;
        }

        StreamSignal signal;
        if (channel == "eg_run" && long.TryParse(parts[2], out var eventId))
        {
            signal = new StreamSignal(eventId, null, target);
            foreach (var subscription in subscriptions.Keys.Where(s => s.TenantId == tenantId && s.RunId == target))
            {
                subscription.Channel.Writer.TryWrite(signal);
            }
        }
        else if (channel == "eg_shop" && parts.Length == 4 && Guid.TryParse(parts[3], out var id))
        {
            signal = new StreamSignal(0, parts[2], id);
            foreach (var subscription in subscriptions.Keys.Where(s => s.TenantId == tenantId && s.ShopId == target))
            {
                subscription.Channel.Writer.TryWrite(signal);
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await firstSubscription.Task.WaitAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        var delay = TimeSpan.FromSeconds(1);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var connection = await dataSource.Source.OpenConnectionAsync(stoppingToken).ConfigureAwait(false);
                connection.Notification += (_, e) => Dispatch(e.Channel, e.Payload);
                await using (var listen = new NpgsqlCommand("LISTEN eg_run; LISTEN eg_shop;", connection))
                {
                    await listen.ExecuteNonQueryAsync(stoppingToken).ConfigureAwait(false);
                }

                lock (gate)
                {
                    listening.TrySetResult();
                }

                delay = TimeSpan.FromSeconds(1);
                logger.LogInformation("sse.listening");
                while (true)
                {
                    await connection.WaitAsync(stoppingToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception e) when (e is NpgsqlException or InvalidOperationException or IOException or TimeoutException)
            {
                // The streams check the database on every ping, so a notification lost meanwhile is not lost for good.
                logger.LogWarning("sse.connection_lost {ErrorType}", e.GetType().Name);
                lock (gate)
                {
                    if (listening.Task.IsCompleted)
                    {
                        listening = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    }
                }

                await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
                delay = TimeSpan.FromSeconds(Math.Min(30, delay.TotalSeconds * 2));
            }
        }
    }

    private void Release(StreamSubscription subscription)
    {
        subscriptions.TryRemove(subscription, out _);
        perUser.AddOrUpdate(subscription.UserId, 0, (_, n) => Math.Max(0, n - 1));
    }
}
