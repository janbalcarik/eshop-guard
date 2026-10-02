using EshopGuard.Api.Http;
using EshopGuard.Api.Problems;
using EshopGuard.Api.Tenancy;
using EshopGuard.Application.Contracts;
using EshopGuard.Application.Problems;
using EshopGuard.Application.Runs;
using EshopGuard.Application.Tenants;
using Microsoft.Extensions.Options;

namespace EshopGuard.Api.Endpoints;

/// <summary>
/// Runs and the live progress (change 11, AD 13): the list of an e-shop, the detail, canceling (admin) and the streams of
/// server-sent events of a run (<c>snapshot</c>, <c>run_event</c>, <c>progress</c>, <c>status</c>, <c>end</c>) and of an e-shop
/// (the states of proposals, groups, publications, questions and runs). A stream checks the database on every ping, so a lost
/// notification is only late.
/// </summary>
public static class RunEndpoints
{
    /// <summary>Events of a run sent at once (the rest follow on the next signal).</summary>
    private const int EventBatch = 500;

    public static RouteGroupBuilder MapRunEndpoints(this RouteGroupBuilder tenant)
    {
        tenant.MapGet("/shops/{shopId:guid}/runs", async (Guid shopId, string? kind, string? cursor, int? limit, RunQueryService service, CancellationToken ct) =>
                TypedResults.Ok(await service.ListAsync(shopId, kind, cursor, limit, ct)))
            .WithTags("runs")
            .RequireTenantRole(TenantRole.Viewer)
            .ProducesProblemCodes(ProblemCodes.ShopNotFound, ProblemCodes.ValidationFailed);

        tenant.MapGet("/runs/{runId:guid}", async (Guid runId, RunQueryService service, CancellationToken ct) => TypedResults.Ok(await service.GetAsync(runId, ct)))
            .WithTags("runs")
            .RequireTenantRole(TenantRole.Viewer)
            .ProducesProblemCodes(ProblemCodes.RunNotFound);

        tenant.MapPost("/runs/{runId:guid}/cancel", async (Guid runId, HttpContext context, RunCancelService service, CancellationToken ct) =>
                TypedResults.Accepted((string?)null, await service.CancelAsync(context.User.RequireUserId(), runId, ct)))
            .WithTags("runs")
            .RequireTenantRole(TenantRole.Admin)
            .ProducesProblemCodes(ProblemCodes.RunNotFound, ProblemCodes.RunNotCancelable);

        tenant.MapGet("/runs/{runId:guid}/events", RunStreamAsync)
            .WithTags("runs")
            .RequireTenantRole(TenantRole.Viewer)
            .Produces(StatusCodes.Status200OK, contentType: "text/event-stream")
            .ProducesProblemCodes(ProblemCodes.RunNotFound, ProblemCodes.SseTooManyConnections);

        tenant.MapGet("/shops/{shopId:guid}/events", ShopStreamAsync)
            .WithTags("runs")
            .RequireTenantRole(TenantRole.Viewer)
            .Produces(StatusCodes.Status200OK, contentType: "text/event-stream")
            .ProducesProblemCodes(ProblemCodes.ShopNotFound, ProblemCodes.SseTooManyConnections);
        return tenant;
    }

    private static async Task<IResult> RunStreamAsync(
        Guid tenantId, Guid runId, HttpContext context, RunQueryService runs, RunEventStream stream, IOptions<SseOptions> options, CancellationToken ct)
    {
        var run = await runs.RequireAsync(runId, ct);
        using var subscription = stream.Subscribe(tenantId, context.User.RequireUserId(), runId, null);
        await stream.WhenListeningAsync(TimeSpan.FromSeconds(5), ct);
        var lastId = long.TryParse(context.Request.Headers["Last-Event-ID"].ToString(), out var parsed) ? parsed : 0;
        var writer = await SseWriter.StartAsync(context, ct);
        try
        {
            await writer.EventAsync("snapshot", await runs.GetAsync(runId, ct), null, ct);
            lastId = await EventsAsync(writer, runs, runId, lastId, ct);
            run = await runs.RequireAsync(runId, ct);
            var progress = RunQueryService.Progress(run);
            var status = RunQueryService.Status(run);
            var ping = TimeSpan.FromSeconds(options.Value.PingSeconds);
            while (!RunQueryService.IsFinal(run.Status))
            {
                if (!await WaitAsync(subscription, ping, ct))
                {
                    await writer.CommentAsync("ping", ct);
                }

                lastId = await EventsAsync(writer, runs, runId, lastId, ct);
                run = await runs.RequireAsync(runId, ct);
                var nowProgress = RunQueryService.Progress(run);
                if (!SameProgress(progress, nowProgress))
                {
                    await writer.EventAsync("progress", nowProgress, null, ct);
                    progress = nowProgress;
                }

                var nowStatus = RunQueryService.Status(run);
                if (nowStatus != status)
                {
                    await writer.EventAsync("status", nowStatus, null, ct);
                    status = nowStatus;
                }
            }

            await writer.EventAsync("end", RunQueryService.Status(run), null, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // The client went away.
        }

        return Results.Empty;
    }

    private static async Task<IResult> ShopStreamAsync(
        Guid tenantId, Guid shopId, HttpContext context, ShopChangeReader changes, RunEventStream stream, IOptions<SseOptions> options, CancellationToken ct)
    {
        await changes.RequireShopAsync(shopId, ct);
        using var subscription = stream.Subscribe(tenantId, context.User.RequireUserId(), null, shopId);
        await stream.WhenListeningAsync(TimeSpan.FromSeconds(5), ct);
        var writer = await SseWriter.StartAsync(context, ct);
        try
        {
            await writer.EventAsync("ready", new { shopId }, null, ct);
            var ping = TimeSpan.FromSeconds(options.Value.PingSeconds);
            while (!ct.IsCancellationRequested)
            {
                if (!await WaitAsync(subscription, ping, ct))
                {
                    await writer.CommentAsync("ping", ct);
                    continue;
                }

                var seen = new HashSet<(string?, Guid)>();
                while (subscription.Signals.TryRead(out var signal))
                {
                    if (signal.Entity is { } entity && seen.Add((entity, signal.EntityId))
                        && await changes.ReadAsync(shopId, entity, signal.EntityId, ct) is { } change)
                    {
                        await writer.EventAsync(entity, change, null, ct);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // The client went away.
        }

        return Results.Empty;
    }

    /// <summary>True when a signal came within <paramref name="timeout"/> (the signals are then read by the caller or dropped).</summary>
    private static async Task<bool> WaitAsync(StreamSubscription subscription, TimeSpan timeout, CancellationToken ct)
    {
        using var quiet = CancellationTokenSource.CreateLinkedTokenSource(ct);
        quiet.CancelAfter(timeout);
        try
        {
            var got = await subscription.Signals.WaitToReadAsync(quiet.Token);
            if (subscription.RunId is not null)
            {
                // A run stream reads its state and events from the database whatever the signal said.
                while (subscription.Signals.TryRead(out _))
                {
                }
            }

            return got;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return false;
        }
    }

    private static async Task<long> EventsAsync(SseWriter writer, RunQueryService runs, Guid runId, long lastId, CancellationToken ct)
    {
        IReadOnlyList<RunEventDto> batch;
        do
        {
            batch = await runs.EventsAsync(runId, lastId, EventBatch, ct);
            foreach (var item in batch)
            {
                await writer.EventAsync("run_event", item, item.Id, ct);
                lastId = item.Id;
            }
        }
        while (batch.Count == EventBatch);

        return lastId;
    }

    private static bool SameProgress(RunProgressDto a, RunProgressDto b) =>
        a.PagesPlanned == b.PagesPlanned && a.PagesFetched == b.PagesFetched && a.PagesProcessed == b.PagesProcessed
        && a.Steps.Count == b.Steps.Count && a.Steps.All(s => b.Steps.TryGetValue(s.Key, out var other) && other == s.Value);
}
