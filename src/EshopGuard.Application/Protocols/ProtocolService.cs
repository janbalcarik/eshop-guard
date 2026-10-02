using System.Text.Json;
using System.Text.Json.Nodes;
using EshopGuard.Application.Audit;
using EshopGuard.Application.Contracts;
using EshopGuard.Application.Findings;
using EshopGuard.Application.Localization;
using EshopGuard.Application.Options;
using EshopGuard.Application.Problems;
using EshopGuard.Application.Shops;
using EshopGuard.Data;
using EshopGuard.Data.Entities.Checks;
using EshopGuard.Data.Entities.Fixes;
using EshopGuard.Data.Tenancy;
using EshopGuard.Jobs.Protocols;
using EshopGuard.Jobs.Queue;
using EshopGuard.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EshopGuard.Application.Protocols;

/// <summary>
/// Protocols of the checks of an e-shop (change 11, AD 11). A request checks the period (<c>from ≤ to ≤ today</c> in
/// <c>Localization:TimeZone</c>) and the language (the one asked for, otherwise the language of the home market), needs a
/// finished check (<c>full_analysis</c> or <c>free_sample</c>), gives the next number, builds the content as it is today
/// (stored as JSON next to the future PDF), inserts the protocol <c>rendering</c> and queues <c>protocol.render</c>; all in one
/// transaction. The PDF is given by a signed link only when it is <c>ready</c>.
/// </summary>
public sealed class ProtocolService(
    EshopGuardDb db,
    ShopReader reader,
    ProtocolNumberAllocator numbers,
    ProtocolDocumentBuilder builder,
    IRefCatalog refCatalog,
    IBlobStore blobs,
    IJobQueue queue,
    SecurityAuditWriter audit,
    IOptions<LocalizationOptions> options,
    TimeProvider time)
{
    public async Task<IReadOnlyList<ProtocolDto>> ListAsync(Guid shopId, CancellationToken ct) =>
        await db.ExecuteInTenantTransactionAsync(async () =>
        {
            await reader.ReadAsync(shopId, ct).ConfigureAwait(false);
            var rows = await db.Protocols.AsNoTracking().Where(p => p.ShopId == shopId).OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id)
                .ToListAsync(ct).ConfigureAwait(false);
            return (IReadOnlyList<ProtocolDto>)rows.Select(Dto).ToList();
        }, ct).ConfigureAwait(false);

    public async Task<ProtocolDto> DetailAsync(Guid shopId, Guid protocolId, CancellationToken ct) =>
        await db.ExecuteInTenantTransactionAsync(async () => Dto(await RequireAsync(shopId, protocolId, ct).ConfigureAwait(false)), ct).ConfigureAwait(false);

    public async Task<ProtocolDto> RequestAsync(Guid userId, Guid shopId, DateOnly? periodFrom, DateOnly? periodTo, string? locale, CancellationToken ct) =>
        await db.ExecuteInTenantTransactionAsync(async () =>
        {
            var shop = await reader.ReadAsync(shopId, ct).ConfigureAwait(false);
            var errors = new ValidationResult();
            if (periodFrom is null)
            {
                errors.Add("periodFrom", ProblemCodes.Fields.Required);
            }

            if (periodTo is null)
            {
                errors.Add("periodTo", ProblemCodes.Fields.Required);
            }

            if (!errors.IsValid)
            {
                throw DomainException.Validation(errors);
            }

            var now = time.GetUtcNow();
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZone)).DateTime);
            if (periodFrom > periodTo || periodTo > today)
            {
                throw new DomainException(ProblemCodes.ProtocolPeriodInvalid, 400, new Dictionary<string, object?> { ["today"] = today.ToString("O") });
            }

            var texts = await TextsAsync(shop.HomeCountry, locale, ct).ConfigureAwait(false);
            var finished = await db.Runs.AsNoTracking().AnyAsync(r => r.ShopId == shopId && (r.Kind == RunKind.FullAnalysis || r.Kind == RunKind.FreeSample)
                && (r.Status == RunStatus.Finished || r.Status == RunStatus.Partial), ct).ConfigureAwait(false);
            if (!finished)
            {
                throw new DomainException(ProblemCodes.ProtocolNoCompletedRun, 409);
            }

            var number = await numbers.NextAsync(shop.TenantId, now, ct).ConfigureAwait(false);
            var build = await builder.BuildAsync(shop, number, texts, periodFrom!.Value, periodTo!.Value, now, ct).ConfigureAwait(false);
            await using (var content = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(build.Document, ProtocolJobs.Json)))
            {
                await blobs.PutAsync(ProtocolJobs.DocumentKey(shop.TenantId, shopId, number), content, "application/json", ct).ConfigureAwait(false);
            }

            var protocol = new Protocol
            {
                ShopId = shopId,
                Number = number,
                PeriodFrom = periodFrom.Value,
                PeriodTo = periodTo.Value,
                Locale = texts.Locale,
                GeneratedBy = userId,
                Summary = JsonDocument.Parse(build.Summary.ToJsonString()),
                RuleSetIds = build.RuleSetIds,
                Status = ProtocolStatus.Rendering,
            };
            db.Protocols.Add(protocol);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            await queue.EnqueueAsync(ProtocolJobs.Render(protocol.TenantId, shopId, protocol.Id), DbSql.Transaction(db), ct).ConfigureAwait(false);
            await audit.WriteAsync(new AuditEvent(AuditActions.ProtocolRequested, protocol.TenantId, userId, "protocol", protocol.Id.ToString("D"), new JsonObject
            {
                ["shopId"] = shopId.ToString("D"),
                ["number"] = number,
                ["locale"] = texts.Locale,
                ["periodFrom"] = periodFrom.Value.ToString("O"),
                ["periodTo"] = periodTo.Value.ToString("O"),
            }), ct).ConfigureAwait(false);
            return Dto(protocol);
        }, ct).ConfigureAwait(false);

    /// <summary>The stored PDF of a <c>ready</c> protocol; <c>409 protocol.not_ready</c> while rendering, <c>protocol.failed</c> after a failure.</summary>
    public async Task<(BlobKey Key, string FileName)> PdfAsync(Guid shopId, Guid protocolId, CancellationToken ct) =>
        await db.ExecuteInTenantTransactionAsync(async () =>
        {
            var protocol = await RequireAsync(shopId, protocolId, ct).ConfigureAwait(false);
            return protocol.Status switch
            {
                ProtocolStatus.Ready when ProtocolJobs.PdfKey(protocol.TenantId, protocol.ShopId, protocol.Number) is var key && protocol.PdfBlobKey == key.Value
                    => (key, protocol.Number + ".pdf"),
                ProtocolStatus.Failed => throw new DomainException(ProblemCodes.ProtocolFailed, 409, new Dictionary<string, object?> { ["errorCode"] = protocol.ErrorCode }),
                _ => throw new DomainException(ProblemCodes.ProtocolNotReady, 409),
            };
        }, ct).ConfigureAwait(false);

    /// <summary>The language asked for (enabled and with texts), otherwise the language of the home market (K rozhodnutí 5).</summary>
    private async Task<ProtocolTexts> TextsAsync(string homeCountry, string? locale, CancellationToken ct)
    {
        var enabled = (await refCatalog.GetLocalesAsync(ct).ConfigureAwait(false)).Where(l => l.Enabled).Select(l => l.Code).ToHashSet(StringComparer.Ordinal);
        if (locale is null)
        {
            var markets = await refCatalog.GetMarketsAsync(ct).ConfigureAwait(false);
            locale = markets.FirstOrDefault(m => string.Equals(m.CountryCode, homeCountry, StringComparison.OrdinalIgnoreCase))?.DefaultLocale ?? options.Value.DefaultMarket;
        }

        return enabled.Contains(locale) && ProtocolTexts.For(locale) is { } texts
            ? texts
            : throw new DomainException(ProblemCodes.LocaleNotEnabled, 400, new Dictionary<string, object?> { ["locale"] = locale });
    }

    private async Task<Protocol> RequireAsync(Guid shopId, Guid protocolId, CancellationToken ct)
    {
        await reader.ReadAsync(shopId, ct).ConfigureAwait(false);
        return await db.Protocols.AsNoTracking().FirstOrDefaultAsync(p => p.ShopId == shopId && p.Id == protocolId, ct).ConfigureAwait(false)
            ?? throw new DomainException(ProblemCodes.ProtocolNotFound, 404);
    }

    private static ProtocolDto Dto(Protocol p) => new(
        p.Id, p.Number, p.PeriodFrom, p.PeriodTo, p.Locale, FindingMapper.Text(p.Status), p.ErrorCode, p.GeneratedBy, p.CreatedAt, p.Summary?.RootElement.Clone());
}
