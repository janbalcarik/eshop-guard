using AngleSharp.Html.Parser;
using EshopGuard.Core.Crawl;
using EshopGuard.Core.Options;
using EshopGuard.Data;
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
/// The job <c>shop.verify_ownership</c> (change 10, AD 9): <c>meta</c> reads the home page through the fetcher (robots.txt,
/// protection against SSRF) and looks only into its <c>&lt;head&gt;</c>; <c>dns</c> asks the resolver of the worker for TXT
/// <c>_eshopguard.{domain}</c>. The result is <c>verified</c> or <c>failed</c> with a code; a verified check sets
/// <c>shops.ownership_verified_at</c> and the method and writes the audit, all with the completion of the job.
/// </summary>
public sealed partial class ShopVerifyOwnershipHandler(
    IPageFetcher fetcher,
    IDnsTxtResolver dns,
    IOptions<EshopGuardOptions> library,
    IOptions<ShopJobsOptions> options,
    TimeProvider time,
    ILogger<ShopVerifyOwnershipHandler> logger) : IJobHandler
{
    public string Kind => ShopJobs.VerifyOwnershipKind;

    public JobResourceClass ResourceClass => JobResourceClass.Fetch;

    public async Task<JobResult> ExecuteAsync(JobExecutionContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        var payload = context.Job.Payload.RootElement;
        var shopId = Guid.Parse(payload.GetProperty("shop_id").GetString()!);
        var verificationId = Guid.Parse(payload.GetProperty("verification_id").GetString()!);
        var db = context.Services.GetRequiredService<EshopGuardDb>();
        var found = await db.ExecuteInTenantTransactionAsync(async () =>
        {
            var shop = await db.Shops.AsNoTracking().Where(s => s.Id == shopId).Select(s => new { s.Domain, s.BaseUrl }).FirstOrDefaultAsync(ct).ConfigureAwait(false);
            var verification = await db.ShopVerifications.AsNoTracking().FirstOrDefaultAsync(v => v.Id == verificationId && v.ShopId == shopId, ct).ConfigureAwait(false);
            return (shop?.Domain, shop?.BaseUrl, verification);
        }, ct).ConfigureAwait(false);
        if (found.Domain is null || found.verification is null)
        {
            return JobResult.Done;
        }

        var verification = found.verification;
        var failure = verification.Method switch
        {
            VerificationMethod.Meta => await CheckMetaAsync(new Uri(found.BaseUrl!), verification.Token, ct).ConfigureAwait(false),
            VerificationMethod.Dns => await CheckDnsAsync(found.Domain, verification.Token, ct).ConfigureAwait(false),
            _ => OwnershipTokens.FetchFailed,
        };
        var now = time.GetUtcNow();
        var status = failure is null ? "verified" : "failed";
        var method = verification.Method == VerificationMethod.Meta ? "meta" : "dns";
        await context.CompleteAsync(async tx =>
        {
            await tx.Db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE shop.shop_verifications SET status = {status}, failure_code = {failure}, checked_at = {now}, updated_at = {now} WHERE id = {verificationId}",
                ct).ConfigureAwait(false);
            if (failure is null)
            {
                await tx.Db.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                    UPDATE shop.shops SET ownership_verified_at = coalesce(ownership_verified_at, {now}), verification_method = coalesce(verification_method, {method}),
                        updated_at = {now}
                    WHERE id = {shopId}
                    """, ct).ConfigureAwait(false);
                await tx.Db.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                    INSERT INTO ops.audit_log (at, tenant_id, actor_user_id, actor_kind, action, entity_type, entity_id, data, created_at)
                    VALUES ({now}, {context.Job.TenantId}, NULL, 'system', 'ownership.verified', 'shop', {shopId.ToString("D")},
                        jsonb_build_object('method', {method}::text, 'verificationId', {verificationId.ToString("D")}::text), {now})
                    """, ct).ConfigureAwait(false);
            }
        }, ct).ConfigureAwait(false);
        LogChecked(logger, shopId, context.Job.TenantId, method, status, failure);
        return JobResult.Done;
    }

    /// <summary>The meta tag of the home page: only its <c>&lt;head&gt;</c>, never the text of the page.</summary>
    private async Task<string?> CheckMetaAsync(Uri baseUrl, string token, CancellationToken ct)
    {
        HomePageResult page;
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct))
        {
            timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.TimeoutSeconds));
            try
            {
                page = await HomePageReader.ReadAsync(fetcher, baseUrl, library.Value.Crawl.UserAgent, options.Value.MaxBytes, captureHeaders: false, timeout.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return OwnershipTokens.FetchFailed;
            }
        }

        if (page.Response?.Body is not { } body)
        {
            return OwnershipTokens.FetchFailed;
        }

        var document = new HtmlParser().ParseDocument(HtmlDecoding.Decode(body, page.Response.Charset));
        var values = document.Head?.QuerySelectorAll("meta[name]")
            .Where(m => string.Equals(m.GetAttribute("name")?.Trim(), OwnershipTokens.MetaName, StringComparison.OrdinalIgnoreCase))
            .Select(m => m.GetAttribute("content")?.Trim() ?? "").ToList() ?? [];
        return values.Count == 0 ? OwnershipTokens.MetaNotFound
            : values.Contains(token, StringComparer.Ordinal) ? null
            : OwnershipTokens.TokenMismatch;
    }

    private async Task<string?> CheckDnsAsync(string domain, string token, CancellationToken ct)
    {
        IReadOnlyList<string> records;
        try
        {
            records = await dns.TxtAsync(OwnershipTokens.DnsName(domain), ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            return OwnershipTokens.DnsRecordNotFound;
        }

        var ours = records.Where(r => r.Trim().StartsWith(OwnershipTokens.DnsPrefix, StringComparison.Ordinal))
            .Select(r => r.Trim()[OwnershipTokens.DnsPrefix.Length..]).ToList();
        return ours.Count == 0 ? OwnershipTokens.DnsRecordNotFound
            : ours.Contains(token, StringComparer.Ordinal) ? null
            : OwnershipTokens.TokenMismatch;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "shop.ownership_checked {ShopId} {TenantId} {Method} {Status} {FailureCode}")]
    private static partial void LogChecked(ILogger logger, Guid shopId, Guid? tenantId, string method, string status, string? failureCode);
}
