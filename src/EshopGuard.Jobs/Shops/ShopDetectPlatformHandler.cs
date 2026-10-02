using EshopGuard.Core.Crawl;
using EshopGuard.Core.Options;
using EshopGuard.Core.Platforms;
using EshopGuard.Data;
using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Ops;
using EshopGuard.Data.Entities.Shops;
using EshopGuard.Data.Tenancy;
using EshopGuard.Jobs.Processing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EshopGuard.Jobs.Shops;

/// <summary>
/// The job <c>shop.detect_platform</c> (change 10, AD 2): robots.txt and the home page of <c>base_url</c> through the fetcher
/// with the protection against SSRF (at most <c>Shops:Detection:MaxBytes</c> and <c>TimeoutSeconds</c>), the technical
/// signatures (<see cref="PlatformDetector"/>), and the result in <c>shops.platform</c>, <c>shops.base_url</c> (the final
/// address on the same site) and <c>shops.detection</c>. A platform set by the user is kept. A redirect to another domain
/// is only reported. A site that fails is a result (<c>failed</c> with a code), not a retry: the user waits.
/// </summary>
public sealed partial class ShopDetectPlatformHandler(
    IPageFetcher fetcher,
    IOptions<EshopGuardOptions> library,
    IOptions<ShopJobsOptions> options,
    PlatformSignatureSource signatures,
    TimeProvider time,
    ILogger<ShopDetectPlatformHandler> logger) : IJobHandler
{
    public string Kind => ShopJobs.DetectPlatformKind;

    public JobResourceClass ResourceClass => JobResourceClass.Fetch;

    public async Task<JobResult> ExecuteAsync(JobExecutionContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        var shopId = Guid.Parse(context.Job.Payload.RootElement.GetProperty("shop_id").GetString()!);
        var db = context.Services.GetRequiredService<EshopGuardDb>();
        var shop = await db.ExecuteInTenantTransactionAsync(
            () => db.Shops.AsNoTracking().Where(s => s.Id == shopId).Select(s => new { s.BaseUrl, s.Detection }).FirstOrDefaultAsync(ct), ct).ConfigureAwait(false);
        if (shop is null)
        {
            // Deleted meanwhile: nothing to recognize.
            return JobResult.Done;
        }

        var previous = ShopDetectionState.Read(shop.Detection);
        var settings = options.Value;
        HomePageResult page;
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct))
        {
            timeout.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds));
            try
            {
                page = await HomePageReader.ReadAsync(fetcher, new Uri(shop.BaseUrl), library.Value.Crawl.UserAgent, settings.MaxBytes, captureHeaders: true, timeout.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                page = new HomePageResult(new Uri(shop.BaseUrl), null, HomePageCodes.Timeout, null);
            }
        }

        var now = time.GetUtcNow();
        var state = new ShopDetectionState
        {
            Status = page.IsSuccess || page.RedirectedToHost is not null ? ShopDetectionState.Done : ShopDetectionState.Failed,
            Platform = PlatformDetection.UnknownPlatform,
            Confidence = Code(PlatformConfidence.Unknown),
            FinalUrl = page.FinalUrl.GetLeftPart(UriPartial.Path),
            RedirectedTo = page.RedirectedToHost,
            FailureCode = page.FailureCode,
            RequestedAt = previous?.RequestedAt,
            DetectedAt = now,
            PlatformSource = previous?.PlatformSource ?? ShopDetectionState.SourceDetected,
        };
        if (page.Response is { } response)
        {
            var result = PlatformDetector.Detect(await signatures.GetAsync(ct).ConfigureAwait(false),
                new PlatformPage(page.FinalUrl, response.Headers, response.SetCookies, response.Body!, response.Charset));
            state = state with { Platform = result.Platform, Confidence = Code(result.Confidence), Signals = result.Signals };
        }

        var platform = Enum.TryParse<ShopPlatform>(state.Platform, ignoreCase: true, out var parsed) ? parsed : ShopPlatform.Unknown;
        var keepPlatform = state.PlatformSource == ShopDetectionState.SourceUser || !page.IsSuccess;
        var baseUrl = page.IsSuccess ? state.FinalUrl : null;
        await context.CompleteAsync(tx => tx.Db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE shop.shops SET detection = {state.ToJson()}::jsonb,
                platform = CASE WHEN {keepPlatform} THEN platform ELSE {SnakeCaseEnumConverter<ShopPlatform>.ToText(platform)} END,
                base_url = coalesce({baseUrl}, base_url), updated_at = {now}
            WHERE id = {shopId}
            """, ct), ct).ConfigureAwait(false);
        LogDetected(logger, shopId, context.Job.TenantId, state.Status, state.Platform!, state.Confidence!, state.FailureCode);
        return JobResult.Done;
    }

    private static string Code(PlatformConfidence confidence) => confidence.ToString().ToLowerInvariant();

    [LoggerMessage(Level = LogLevel.Information, Message = "shop.detected {ShopId} {TenantId} {Status} {Platform} {Confidence} {FailureCode}")]
    private static partial void LogDetected(ILogger logger, Guid shopId, Guid? tenantId, string status, string platform, string confidence, string? failureCode);
}

/// <summary>The signatures of <c>config/platforms.yaml</c>, read once on first use (the path from <c>EshopGuard:Rules:PlatformsFile</c>).</summary>
public sealed class PlatformSignatureSource(IOptions<EshopGuardOptions> options)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private PlatformSignatures? _signatures;

    public async Task<PlatformSignatures> GetAsync(CancellationToken ct)
    {
        if (_signatures is { } loaded)
        {
            return loaded;
        }

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return _signatures ??= await PlatformSignatures.LoadAsync(options.Value.Rules.PlatformsFile, ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }
}
