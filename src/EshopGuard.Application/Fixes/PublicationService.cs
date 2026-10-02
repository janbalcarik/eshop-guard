using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using EshopGuard.Application.Audit;
using EshopGuard.Application.Contracts;
using EshopGuard.Application.Findings;
using EshopGuard.Application.Problems;
using EshopGuard.Application.Shops;
using EshopGuard.Data;
using EshopGuard.Data.Entities.Content;
using EshopGuard.Data.Entities.Fixes;
using EshopGuard.Data.Tenancy;
using EshopGuard.Jobs.Fixes;
using EshopGuard.Jobs.Queue;
using Microsoft.EntityFrameworkCore;

namespace EshopGuard.Application.Fixes;

/// <summary>
/// Publication of the accepted fixes through the connector and the composed text for „Kopírovať text“ (change 11, AD 8). This
/// change only queues: one publication per page and field (<c>status = queued</c>, an idempotency key from the text, so an
/// equal request returns the same one) and the job <c>publish.fix</c> for change 15, whose handler checks the conflict, writes,
/// keeps the original and sets the state. Fields the platform cannot write are <c>skipped</c> with <c>copy_only</c>; a field
/// with an accepted change that is no longer in the page is <c>skipped</c> with <c>not_located</c> (never written without it).
/// </summary>
public sealed class PublicationService(
    EshopGuardDb db,
    ShopReader reader,
    ExtractContextReader extracts,
    PublishAvailability availability,
    IJobQueue queue,
    SecurityAuditWriter audit,
    ITenantContext tenant)
{
    public const string CopyOnly = "copy_only";
    public const string NotLocated = "not_located";

    private static readonly FixField[] Fields = Enum.GetValues<FixField>();

    /// <summary>The whole field with the accepted changes; <c>409 page.no_accepted_changes</c> without any.</summary>
    public async Task<FixedTextDto> FixedTextAsync(Guid shopId, Guid pageId, string? field, CancellationToken ct) =>
        await db.ExecuteInTenantTransactionAsync(async () =>
        {
            await reader.ReadAsync(shopId, ct).ConfigureAwait(false);
            var page = await db.Pages.AsNoTracking().FirstOrDefaultAsync(p => p.ShopId == shopId && p.Id == pageId, ct).ConfigureAwait(false)
                ?? throw new DomainException(ProblemCodes.PageNotFound, 404);
            var parsed = ParseField(field);
            var proposals = await db.FixProposals.AsNoTracking().Where(p => p.ShopId == shopId && p.PageId == pageId && p.Field == parsed).ToListAsync(ct).ConfigureAwait(false);
            if (!proposals.Any(p => p.Status is FixProposalStatus.Accepted or FixProposalStatus.Published))
            {
                throw new DomainException(ProblemCodes.PageNoAcceptedChanges, 409, new Dictionary<string, object?> { ["field"] = FindingMapper.Text(parsed) });
            }

            var (versionId, text) = await TextAsync(page, ct).ConfigureAwait(false);
            var composed = FixedTextComposer.Compose(text, parsed, proposals);
            return new FixedTextDto(FindingMapper.Text(parsed), composed.Text, composed.Applied, composed.Pending, composed.Unplaced, versionId);
        }, ct).ConfigureAwait(false);

    /// <summary>Publications of the accepted changes of the chosen pages, proposals or group (<c>202</c>).</summary>
    public async Task<PublicationBatchDto> RequestAsync(
        Guid userId, Guid shopId, IReadOnlyList<Guid>? pageIds, IReadOnlyList<Guid>? proposalIds, Guid? groupId, CancellationToken ct) =>
        await db.ExecuteInTenantTransactionAsync(async () =>
        {
            var shop = await reader.ReadAsync(shopId, ct).ConfigureAwait(false);
            SampleOnlyGuard.Ensure(shop);
            var pages = (pageIds ?? []).Distinct().ToArray();
            var ids = (proposalIds ?? []).Distinct().ToArray();
            if (pages.Length == 0 && ids.Length == 0 && groupId is null)
            {
                throw DomainException.Validation(new ValidationResult().Add("pageIds", ProblemCodes.Fields.Required));
            }

            await EnsureExistAsync(shopId, pages, ids, groupId, ct).ConfigureAwait(false);
            var (connector, publisher) = await AvailableAsync(shop, ct).ConfigureAwait(false);

            var chosen = await db.FixProposals.AsNoTracking()
                .Where(p => p.ShopId == shopId && p.Status == FixProposalStatus.Accepted
                    && (pages.Contains(p.PageId) || ids.Contains(p.Id) || (groupId != null && p.GroupId == groupId)))
                .Select(p => new { p.PageId, p.Field })
                .Distinct().ToListAsync(ct).ConfigureAwait(false);
            if (chosen.Count == 0)
            {
                throw new DomainException(ProblemCodes.PublicationNothingToPublish, 409);
            }

            var templateGroups = await db.FixGroups.AsNoTracking().Where(g => g.ShopId == shopId && g.Kind == FixGroupKind.Template).Select(g => g.Id)
                .ToListAsync(ct).ConfigureAwait(false);
            var publications = new List<Publication>();
            var skipped = new List<PublicationSkippedDto>();
            foreach (var pagePairs in chosen.GroupBy(c => c.PageId).OrderBy(g => g.Key))
            {
                var page = await db.Pages.AsNoTracking().FirstAsync(p => p.ShopId == shopId && p.Id == pagePairs.Key, ct).ConfigureAwait(false);
                var (_, text) = await TextAsync(page, ct).ConfigureAwait(false);
                var all = await db.FixProposals.AsNoTracking().Where(p => p.ShopId == shopId && p.PageId == page.Id).ToListAsync(ct).ConfigureAwait(false);
                foreach (var field in pagePairs.Select(c => c.Field).OrderBy(f => f))
                {
                    var own = all.Where(p => p.Field == field).ToList();
                    var accepted = own.Where(p => p.Status == FixProposalStatus.Accepted).Select(p => p.Id).ToList();
                    var fieldText = FindingMapper.Text(field);
                    var template = own.Any(p => p.Status == FixProposalStatus.Accepted
                        && ((p.GroupId is { } g && templateGroups.Contains(g)) || (field == FixField.Block && p.BlockIndex is null)));
                    if (!publisher.CanPublish(shop.Platform, fieldText, new FixPublishPage(page.Id, page.ExternalId, page.PageType is { } t ? FindingMapper.Text(t) : null, template)))
                    {
                        skipped.Add(new PublicationSkippedDto(page.Id, fieldText, CopyOnly, accepted));
                        continue;
                    }

                    var composed = FixedTextComposer.Compose(text, field, own);
                    if (composed.Unplaced.Count > 0 || composed.Applied.Count == 0)
                    {
                        skipped.Add(new PublicationSkippedDto(page.Id, fieldText, NotLocated, composed.Unplaced.Count > 0 ? composed.Unplaced : accepted));
                        continue;
                    }

                    publications.Add(await PublicationAsync(userId, shopId, connector, page, fieldText, composed, ct).ConfigureAwait(false));
                }
            }

            await AuditAsync(AuditActions.PublicationRequested, userId, shopId, null, new JsonObject
            {
                ["pages"] = chosen.Select(c => c.PageId).Distinct().Count(),
                ["publications"] = publications.Count,
                ["copyOnly"] = skipped.Count(s => s.ReasonCode == CopyOnly),
                ["notLocated"] = skipped.Count(s => s.ReasonCode == NotLocated),
            }, ct).ConfigureAwait(false);
            return new PublicationBatchDto(publications.Select(p => Dto(p, detail: false)).ToList(), skipped);
        }, ct).ConfigureAwait(false);

    public async Task<CursorPage<PublicationDto>> ListAsync(Guid shopId, string? status, string? cursor, int? limit, CancellationToken ct) =>
        await db.ExecuteInTenantTransactionAsync(async () =>
        {
            await reader.ReadAsync(shopId, ct).ConfigureAwait(false);
            var size = Cursor.Limit(limit);
            var query = db.Publications.AsNoTracking().Where(p => p.ShopId == shopId);
            if (status is not null)
            {
                var wanted = Enum.GetValues<PublicationStatus>().Where(s => FindingMapper.Text(s) == status).ToArray();
                if (wanted.Length == 0)
                {
                    throw DomainException.Validation(new ValidationResult().Add("status", ProblemCodes.Fields.ValueNotAllowed));
                }

                query = query.Where(p => wanted.Contains(p.Status));
            }

            var total = await query.CountAsync(ct).ConfigureAwait(false);
            if (Cursor.Decode(cursor) is { } key)
            {
                // Newest first by the id (a version 7 id grows with time), as the notifications.
                var last = key is [{ ValueKind: System.Text.Json.JsonValueKind.String } id] && Guid.TryParse(id.GetString(), out var parsed) ? parsed : throw Cursor.Invalid();
                query = query.Where(p => p.Id.CompareTo(last) < 0);
            }

            var rows = await query.OrderByDescending(p => p.Id).Take(size + 1).ToListAsync(ct).ConfigureAwait(false);
            var items = rows.Take(size).ToList();
            var next = rows.Count > size ? Cursor.Encode(items[^1].Id) : null;
            return new CursorPage<PublicationDto>(items.Select(p => Dto(p, detail: false)).ToList(), next, total);
        }, ct).ConfigureAwait(false);

    public async Task<PublicationDto> DetailAsync(Guid shopId, Guid publicationId, CancellationToken ct) =>
        await db.ExecuteInTenantTransactionAsync(async () =>
        {
            await reader.ReadAsync(shopId, ct).ConfigureAwait(false);
            var publication = await db.Publications.AsNoTracking().FirstOrDefaultAsync(p => p.ShopId == shopId && p.Id == publicationId, ct).ConfigureAwait(false)
                ?? throw new DomainException(ProblemCodes.PublicationNotFound, 404);
            return Dto(publication, detail: true);
        }, ct).ConfigureAwait(false);

    /// <summary>Puts the original back (<c>publish.rollback</c>, change 15); only a <c>published</c> publication.</summary>
    public async Task<PublicationDto> RollbackAsync(Guid userId, Guid shopId, Guid publicationId, CancellationToken ct) =>
        await db.ExecuteInTenantTransactionAsync(async () =>
        {
            var shop = await reader.ReadAsync(shopId, ct).ConfigureAwait(false);
            SampleOnlyGuard.Ensure(shop);
            var publication = await db.Publications.AsNoTracking().FirstOrDefaultAsync(p => p.ShopId == shopId && p.Id == publicationId, ct).ConfigureAwait(false)
                ?? throw new DomainException(ProblemCodes.PublicationNotFound, 404);
            if (publication.Status != PublicationStatus.Published)
            {
                throw new DomainException(ProblemCodes.PublicationNotRollbackable, 409, new Dictionary<string, object?> { ["status"] = FindingMapper.Text(publication.Status) });
            }

            await AvailableAsync(shop, ct).ConfigureAwait(false);
            await queue.EnqueueAsync(PublishJobs.Rollback(publication.TenantId, shopId, publication.Id), DbSql.Transaction(db), ct).ConfigureAwait(false);
            await AuditAsync(AuditActions.PublicationRollbackRequested, userId, shopId, publication.Id, new JsonObject { ["field"] = publication.Field }, ct).ConfigureAwait(false);
            return Dto(publication, detail: true);
        }, ct).ConfigureAwait(false);

    /// <summary>The prerequisites of AD 4: <c>publication.not_available</c> with the reason, or <c>connector_unavailable</c> without a publisher.</summary>
    private async Task<(Data.Entities.Shops.Connector Connector, IFixPublisher Publisher)> AvailableAsync(Data.Entities.Shops.Shop shop, CancellationToken ct)
    {
        var (reason, connector) = await availability.BlockerAsync(shop, ct).ConfigureAwait(false);
        if (reason == PublishAvailability.PublisherMissing)
        {
            throw new DomainException(ProblemCodes.PublicationConnectorUnavailable, 409);
        }

        if (reason is not null || connector is null || availability.Publisher is not { } publisher)
        {
            throw new DomainException(ProblemCodes.PublicationNotAvailable, 409, new Dictionary<string, object?> { ["reason"] = reason ?? PublishAvailability.NoConnector });
        }

        return (connector, publisher);
    }

    private async Task EnsureExistAsync(Guid shopId, Guid[] pages, Guid[] proposals, Guid? groupId, CancellationToken ct)
    {
        if (pages.Length > 0 && await db.Pages.CountAsync(p => p.ShopId == shopId && pages.Contains(p.Id), ct).ConfigureAwait(false) != pages.Length)
        {
            throw new DomainException(ProblemCodes.PageNotFound, 404);
        }

        if (proposals.Length > 0 && await db.FixProposals.CountAsync(p => p.ShopId == shopId && proposals.Contains(p.Id), ct).ConfigureAwait(false) != proposals.Length)
        {
            throw new DomainException(ProblemCodes.ProposalNotFound, 404);
        }

        if (groupId is { } id && !await db.FixGroups.AnyAsync(g => g.ShopId == shopId && g.Id == id, ct).ConfigureAwait(false))
        {
            throw new DomainException(ProblemCodes.GroupNotFound, 404);
        }
    }

    /// <summary>The publication of the field, or the one an equal request made before (same page, field, original and new text).</summary>
    private async Task<Publication> PublicationAsync(
        Guid userId, Guid shopId, Data.Entities.Shops.Connector connector, Page page, string field, ComposedText composed, CancellationToken ct)
    {
        var oldHash = SHA256.HashData(Encoding.UTF8.GetBytes(composed.SourceText ?? ""));
        var newHash = SHA256.HashData(Encoding.UTF8.GetBytes(composed.Text));
        var key = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{shopId:D}|{page.Id:D}|{field}|{Convert.ToHexStringLower(oldHash)}|{Convert.ToHexStringLower(newHash)}")));
        if (await db.Publications.AsNoTracking().FirstOrDefaultAsync(p => p.IdempotencyKey == key, ct).ConfigureAwait(false) is { } existing)
        {
            return existing;
        }

        var publication = new Publication
        {
            ShopId = shopId,
            ConnectorId = connector.Id,
            PageId = page.Id,
            ExternalId = page.ExternalId,
            Field = field,
            Language = page.Language,
            OldValueHash = oldHash,
            NewValue = composed.Text,
            IdempotencyKey = key,
            Status = PublicationStatus.Queued,
            RequestedBy = userId,
            FixProposalIds = [.. composed.Applied],
        };
        db.Publications.Add(publication);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        await queue.EnqueueAsync(PublishJobs.Publish(publication.TenantId, shopId, publication.Id), DbSql.Transaction(db), ct).ConfigureAwait(false);
        return publication;
    }

    /// <summary>The current version of the page and its extraction (an empty text when it has none: every change is then not located).</summary>
    private async Task<(Guid VersionId, PageText Text)> TextAsync(Page page, CancellationToken ct)
    {
        var version = page.CurrentVersionId is { } id
            ? await db.PageVersions.AsNoTracking().Where(v => v.ShopId == page.ShopId && v.Id == id).Select(v => new { v.Id, v.ExtractBlobKey }).FirstOrDefaultAsync(ct).ConfigureAwait(false)
            : null;
        var text = version is null || tenant.TenantId is not { } tenantId ? null : await extracts.ReadAsync(tenantId, page.ShopId, version.ExtractBlobKey, ct).ConfigureAwait(false);
        return (version?.Id ?? Guid.Empty, text ?? new PageText(null, null, null, []));
    }

    private static FixField ParseField(string? field)
    {
        if (string.IsNullOrEmpty(field))
        {
            throw DomainException.Validation(new ValidationResult().Add("field", ProblemCodes.Fields.Required));
        }

        return Fields.FirstOrDefault(f => FindingMapper.Text(f) == field) is var parsed && FindingMapper.Text(parsed) == field
            ? parsed
            : throw DomainException.Validation(new ValidationResult().Add("field", ProblemCodes.Fields.ValueNotAllowed));
    }

    private static PublicationDto Dto(Publication p, bool detail) => new(
        p.Id, p.PageId, p.Field, p.Language, FindingMapper.Text(p.Status), p.Attempts, p.Error, p.RequestedBy, p.PublishedAt, p.RolledBackAt, p.FixProposalIds,
        p.CreatedAt, detail ? p.OldValue : null, detail ? p.NewValue : null);

    private Task AuditAsync(string action, Guid userId, Guid shopId, Guid? publicationId, JsonObject data, CancellationToken ct)
    {
        data["shopId"] = shopId.ToString("D");
        return audit.WriteAsync(new AuditEvent(action, tenant.TenantId, userId, "publication", publicationId?.ToString("D") ?? shopId.ToString("D"), data), ct);
    }
}
