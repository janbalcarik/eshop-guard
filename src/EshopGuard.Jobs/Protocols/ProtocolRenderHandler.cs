using System.Text.Json;
using System.Text.Json.Nodes;
using EshopGuard.Data;
using EshopGuard.Data.Entities.Fixes;
using EshopGuard.Data.Entities.Ops;
using EshopGuard.Data.Tenancy;
using EshopGuard.Jobs.Notifications;
using EshopGuard.Jobs.Processing;
using EshopGuard.Jobs.Queue;
using EshopGuard.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EshopGuard.Jobs.Protocols;

/// <summary>The job <c>protocol.render</c> (P1, class <c>cpu</c>, one per protocol) and the keys of its files.</summary>
public static class ProtocolJobs
{
    public const string RenderKind = "protocol.render";

    public const int MaxAttempts = 3;

    public static JobRequest Render(Guid tenantId, Guid shopId, Guid protocolId) => new(
        RenderKind, JobResourceClass.Cpu, JobPriority.P1, JsonDocument.Parse($$"""{"protocol_id":"{{protocolId:D}}"}"""),
        TenantId: tenantId, ShopId: shopId, DedupeKey: $"protocol:{protocolId:N}", MaxAttempts: MaxAttempts);

    /// <summary><c>tenants/{tenantId}/shops/{shopId}/protocols/{number}.pdf</c>.</summary>
    public static BlobKey PdfKey(Guid tenantId, Guid shopId, string number) => BlobKey.ForShop(tenantId, shopId, "protocols", number + ".pdf");

    /// <summary>The content of the protocol as it was on the day of issue (JSON next to the PDF).</summary>
    public static BlobKey DocumentKey(Guid tenantId, Guid shopId, string number) => BlobKey.ForShop(tenantId, shopId, "protocols", number + ".json");

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
}

/// <summary>
/// The job <c>protocol.render</c> (change 11, AD 11): the content stored when the protocol was requested goes through
/// <see cref="IPdfRenderer"/> into <c>tenants/{tenantId}/shops/{shopId}/protocols/{number}.pdf</c>; the protocol becomes
/// <c>ready</c> and the editors get <c>protocol_ready</c>. Without a renderer (K rozhodnutí 6), without the content or when the
/// rendering fails the protocol becomes <c>failed</c> with the code and they get <c>protocol_failed</c>. Logs carry ids and codes.
/// </summary>
public sealed class ProtocolRenderHandler(IBlobStore blobs, NotificationDispatcher notifications, TimeProvider time, ILogger<ProtocolRenderHandler> logger) : IJobHandler
{
    public const string RendererUnavailable = "pdf_renderer_unavailable";
    public const string RenderFailed = "pdf_render_failed";
    public const string DocumentMissing = "document_missing";

    public string Kind => ProtocolJobs.RenderKind;

    public JobResourceClass ResourceClass => JobResourceClass.Cpu;

    public async Task<JobResult> ExecuteAsync(JobExecutionContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        var id = Guid.Parse(context.Job.Payload.RootElement.GetProperty("protocol_id").GetString()!);
        var db = context.Services.GetRequiredService<EshopGuardDb>();
        var protocol = await db.ExecuteInTenantTransactionAsync(() => db.Protocols.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct), ct).ConfigureAwait(false);
        if (protocol is null || protocol.Status != ProtocolStatus.Rendering)
        {
            logger.LogInformation("protocol.render {ProtocolId} skipped", id);
            return JobResult.Done;
        }

        var (key, error) = await RenderAsync(context.Services.GetService<IPdfRenderer>(), protocol, ct).ConfigureAwait(false);
        var now = time.GetUtcNow();
        await context.CompleteAsync(async tx =>
        {
            var row = await tx.Db.Protocols.FirstAsync(p => p.Id == id, ct).ConfigureAwait(false);
            row.Status = error is null ? ProtocolStatus.Ready : ProtocolStatus.Failed;
            row.PdfBlobKey = key?.Value;
            row.ErrorCode = error;
            row.UpdatedAt = now;
            await tx.Db.SaveChangesAsync(ct).ConfigureAwait(false);
            var parameters = new JsonObject { ["protocol_id"] = id.ToString("D"), ["number"] = protocol.Number };
            if (error is not null)
            {
                parameters["error_code"] = error;
            }

            await notifications.NotifyAsync(tx.Transaction, new NotificationRequest(protocol.TenantId, protocol.ShopId,
                error is null ? NotificationKinds.ProtocolReady : NotificationKinds.ProtocolFailed, parameters, NotificationRoutes.Protocol,
                new JsonObject { ["shopId"] = protocol.ShopId.ToString("D"), ["protocolId"] = id.ToString("D") }), ct).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);
        logger.LogInformation("protocol.rendered {ProtocolId} {Status} {ErrorCode}", id, error is null ? "ready" : "failed", error);
        return JobResult.Done;
    }

    private async Task<(BlobKey? Key, string? Error)> RenderAsync(IPdfRenderer? renderer, Protocol protocol, CancellationToken ct)
    {
        if (renderer is null)
        {
            return (null, RendererUnavailable);
        }

        ProtocolDocument? document;
        await using (var stream = await blobs.OpenReadAsync(ProtocolJobs.DocumentKey(protocol.TenantId, protocol.ShopId, protocol.Number), ct).ConfigureAwait(false))
        {
            document = stream is null ? null : await JsonSerializer.DeserializeAsync<ProtocolDocument>(stream, ProtocolJobs.Json, ct).ConfigureAwait(false);
        }

        if (document is null)
        {
            return (null, DocumentMissing);
        }

        byte[] pdf;
        try
        {
            pdf = await renderer.RenderAsync(document, ct).ConfigureAwait(false);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogWarning("protocol.render {ProtocolId} failed {ErrorType}", protocol.Id, e.GetType().Name);
            return (null, RenderFailed);
        }

        var key = ProtocolJobs.PdfKey(protocol.TenantId, protocol.ShopId, protocol.Number);
        await using var content = new MemoryStream(pdf);
        await blobs.PutAsync(key, content, "application/pdf", ct).ConfigureAwait(false);
        return (key, null);
    }
}

/// <summary>Registration of the job of the protocols (worker); the renderer comes with K rozhodnutí 6.</summary>
public static class ProtocolJobsServiceCollectionExtensions
{
    public static IServiceCollection AddProtocolJobs(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddNotifications();
        services.AddJobHandler<ProtocolRenderHandler>();
        return services;
    }
}
