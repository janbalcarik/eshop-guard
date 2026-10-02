using System.Text.Json;
using System.Text.Json.Nodes;
using EshopGuard.Application.Audit;
using EshopGuard.Application.Contracts;
using EshopGuard.Application.Findings;
using EshopGuard.Application.Fixes;
using EshopGuard.Application.Problems;
using EshopGuard.Data;
using EshopGuard.Data.Entities.Checks;
using EshopGuard.Data.Entities.Fixes;
using EshopGuard.Data.Entities.Shops;
using EshopGuard.Data.Tenancy;
using EshopGuard.Jobs.Evidence;
using EshopGuard.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EshopGuard.Application.Evidence;

/// <summary>What a new piece of evidence says (the metadata of <c>POST T/evidence</c>).</summary>
public sealed record EvidenceInput(
    string? ClaimText, string? SubjectKind, string? SubjectLabel, string? Kind, string? Title, DateOnly? ValidFrom, DateOnly? ValidUntil, string? RegistryRef);

/// <summary>The file of a new piece of evidence: its content, the name the user gave it and its length.</summary>
public sealed record EvidenceUpload(Stream Content, string? FileName, long Length);

/// <summary>A change of a piece of evidence (<c>PATCH</c>): only the given fields change.</summary>
public sealed record EvidencePatch(string? ClaimText, string? SubjectKind, string? SubjectLabel, string? Title, DateOnly? ValidFrom, DateOnly? ValidUntil);

/// <summary>Where the file of a piece of evidence is (for the signed link).</summary>
public sealed record EvidenceFile(BlobKey Key, string FileName, string ContentType);

/// <summary>
/// The evidence of the tenant (change 11, AD 10, design G). It holds for every e-shop of the tenant; its links say where it was
/// used. The list adds the open questions as rows <c>awaiting_answer</c> (one per text, as the answer will apply). The file is
/// checked by its content and stored under <c>tenants/{t}/evidence/{id}/</c>; it is downloaded only through a signed link.
/// Deleting is soft: the findings kept with it go back to <c>open</c> and the decisions remembered with it are superseded.
/// Changes go over <c>If-Match</c>; the audit carries codes and counts, never the claim or the file.
/// </summary>
public sealed class EvidenceService(
    EshopGuardDb db,
    IBlobStore blobs,
    FindingTransitions transitions,
    DecisionMemoryWriter memory,
    AnswerPropagation propagation,
    SecurityAuditWriter audit,
    IOptions<EvidenceOptions> options,
    TimeProvider time)
{
    private static readonly string[] Statuses = ["valid", "expiring", "expired", "awaiting_answer", "claim_removed"];
    private static readonly string[] SubjectKinds = ["brand", "product", "group"];
    private static readonly string[] UploadKinds = ["certificate", "license", "test_report", "statement"];

    public async Task<EvidenceListDto> ListAsync(string? status, string? q, Guid? shopId, CancellationToken ct)
    {
        if (status is not null && !Statuses.Contains(status))
        {
            throw DomainException.Validation(new ValidationResult().Add("status", ProblemCodes.Fields.ValueNotAllowed));
        }

        return await db.ExecuteInTenantTransactionAsync(async () =>
        {
            var query = db.EvidenceItems.AsNoTracking().Where(e => e.DeletedAt == null);
            if (!string.IsNullOrWhiteSpace(q))
            {
                var pattern = "%" + q.Trim().Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal) + "%";
                query = query.Where(e => EF.Functions.ILike(e.ClaimText, pattern) || EF.Functions.ILike(e.SubjectLabel ?? "", pattern) || EF.Functions.ILike(e.Title ?? "", pattern));
            }

            if (shopId is { } shop)
            {
                query = query.Where(e => db.EvidenceLinks.Any(l => l.EvidenceId == e.Id && l.ShopId == shop));
            }

            var items = await query.OrderByDescending(e => e.CreatedAt).ToListAsync(ct).ConfigureAwait(false);
            var dtos = await DtosAsync(items, detail: false, ct).ConfigureAwait(false);
            var awaiting = await AwaitingAsync(q, shopId, ct).ConfigureAwait(false);
            var covered = await db.EvidenceLinks.AsNoTracking()
                .Where(l => l.PageId != null && db.EvidenceItems.Any(e => e.Id == l.EvidenceId && e.DeletedAt == null && e.Source == EvidenceSource.Answer
                    && e.Status != EvidenceStatus.Expired))
                .Select(l => l.PageId).Distinct().CountAsync(ct).ConfigureAwait(false);
            var all = dtos.Concat(awaiting).ToList();
            var stats = new EvidenceStatsDto(all.Count(d => d.Status == "valid"), all.Count(d => d.Status == "expiring"), awaiting.Count, covered);
            return new EvidenceListDto(stats, all.Where(d => status is null || d.Status == status).OrderBy(d => Order(d.Status)).ToList());
        }, ct).ConfigureAwait(false);
    }

    public async Task<EvidenceDto> DetailAsync(Guid evidenceId, CancellationToken ct) =>
        await db.ExecuteInTenantTransactionAsync(async () =>
        {
            var item = await db.EvidenceItems.AsNoTracking().FirstOrDefaultAsync(e => e.Id == evidenceId && e.DeletedAt == null, ct).ConfigureAwait(false)
                ?? throw new DomainException(ProblemCodes.EvidenceNotFound, 404);
            return (await DtosAsync([item], detail: true, ct).ConfigureAwait(false))[0];
        }, ct).ConfigureAwait(false);

    public async Task<EvidenceDto> CreateAsync(Guid tenantId, Guid userId, EvidenceInput input, EvidenceUpload? upload, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        var validation = new ValidationResult();
        if (input.SubjectKind is null || !SubjectKinds.Contains(input.SubjectKind))
        {
            validation.Add("subjectKind", ProblemCodes.Fields.ValueNotAllowed);
        }

        if (input.Kind is null || !UploadKinds.Contains(input.Kind))
        {
            validation.Add("kind", ProblemCodes.Fields.ValueNotAllowed);
        }

        if (string.IsNullOrWhiteSpace(input.ClaimText))
        {
            throw new DomainException(ProblemCodes.EvidenceClaimRequired, 400);
        }

        if (!validation.IsValid)
        {
            throw DomainException.Validation(validation);
        }

        EnsureDates(input.ValidFrom, input.ValidUntil);
        var item = new EvidenceItem
        {
            ClaimText = input.ClaimText.Trim(),
            SubjectKind = SnakeEnum<EvidenceSubjectKind>(input.SubjectKind!),
            SubjectLabel = Trim(input.SubjectLabel),
            Kind = SnakeEnum<EvidenceKind>(input.Kind!),
            Title = Trim(input.Title),
            Source = EvidenceSource.Upload,
            RegistryRef = Trim(input.RegistryRef),
            ValidFrom = EvidenceStatusCalculator.Stored(input.ValidFrom),
            ValidUntil = EvidenceStatusCalculator.Stored(input.ValidUntil),
            CreatedBy = userId,
        };
        item.Status = StatusOf(item).Status;
        if (upload is not null)
        {
            var (key, name) = await StoreAsync(tenantId, item.Id, upload, ct).ConfigureAwait(false);
            item.FileBlobKey = key.Value;
            item.FileName = name;
        }

        return await db.ExecuteInTenantTransactionAsync(async () =>
        {
            db.EvidenceItems.Add(item);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            await audit.WriteAsync(new AuditEvent(AuditActions.EvidenceCreated, tenantId, userId, "evidence", item.Id.ToString("D"),
                new JsonObject { ["kind"] = input.Kind, ["hasFile"] = upload is not null }), ct).ConfigureAwait(false);
            return (await DtosAsync([item], detail: true, ct).ConfigureAwait(false))[0];
        }, ct).ConfigureAwait(false);
    }

    public async Task<EvidenceDto> UpdateAsync(Guid tenantId, Guid userId, Guid evidenceId, EvidencePatch patch, uint? version, CancellationToken ct) =>
        await db.ExecuteInTenantTransactionAsync(async () =>
        {
            ArgumentNullException.ThrowIfNull(patch);
            var item = await RequireAsync(evidenceId, ct).ConfigureAwait(false);
            db.Entry(item).Property(e => e.Version).OriginalValue = Concurrency.Require(version);
            if (patch.ClaimText is { } claim)
            {
                item.ClaimText = string.IsNullOrWhiteSpace(claim) ? throw new DomainException(ProblemCodes.EvidenceClaimRequired, 400) : claim.Trim();
            }

            if (patch.SubjectKind is { } subject)
            {
                item.SubjectKind = SubjectKinds.Contains(subject)
                    ? SnakeEnum<EvidenceSubjectKind>(subject)
                    : throw DomainException.Validation(new ValidationResult().Add("subjectKind", ProblemCodes.Fields.ValueNotAllowed));
            }

            item.SubjectLabel = patch.SubjectLabel is null ? item.SubjectLabel : Trim(patch.SubjectLabel);
            item.Title = patch.Title is null ? item.Title : Trim(patch.Title);
            var until = patch.ValidUntil ?? EvidenceStatusCalculator.Date(item.ValidUntil);
            var from = patch.ValidFrom ?? EvidenceStatusCalculator.Date(item.ValidFrom);
            EnsureDates(from, until);
            if (patch.ValidUntil is not null && patch.ValidUntil != EvidenceStatusCalculator.Date(item.ValidUntil))
            {
                // A new end of validity gets its own reminder.
                item.ReminderSentAt = null;
            }

            item.ValidFrom = EvidenceStatusCalculator.Stored(from);
            item.ValidUntil = EvidenceStatusCalculator.Stored(until);
            item.Status = StatusOf(item).Status;
            item.UpdatedAt = time.GetUtcNow();
            await SaveAsync(ct).ConfigureAwait(false);
            await audit.WriteAsync(new AuditEvent(AuditActions.EvidenceUpdated, tenantId, userId, "evidence", item.Id.ToString("D"), []), ct).ConfigureAwait(false);
            await db.Entry(item).ReloadAsync(ct).ConfigureAwait(false);
            return (await DtosAsync([item], detail: true, ct).ConfigureAwait(false))[0];
        }, ct).ConfigureAwait(false);

    public async Task DeleteAsync(Guid tenantId, Guid userId, Guid evidenceId, CancellationToken ct) =>
        await db.ExecuteInTenantTransactionAsync(async () =>
        {
            var item = await RequireAsync(evidenceId, ct).ConfigureAwait(false);
            item.DeletedAt = time.GetUtcNow();
            item.UpdatedAt = time.GetUtcNow();
            var reopened = await ReopenAsync(userId, evidenceId, "evidence_deleted", ct).ConfigureAwait(false);
            await memory.SupersedeEvidenceAsync(evidenceId, ct).ConfigureAwait(false);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            await audit.WriteAsync(new AuditEvent(AuditActions.EvidenceDeleted, tenantId, userId, "evidence", evidenceId.ToString("D"),
                new JsonObject { ["findingsReopened"] = reopened }), ct).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

    /// <summary>Links findings of any e-shop of the tenant: each becomes <c>kept_with_evidence</c> (by the state machine) and is remembered.</summary>
    public async Task<EvidenceDto> LinkAsync(Guid tenantId, Guid userId, Guid evidenceId, IReadOnlyList<Guid>? findingIds, CancellationToken ct) =>
        await db.ExecuteInTenantTransactionAsync(async () =>
        {
            var item = await RequireAsync(evidenceId, ct).ConfigureAwait(false);
            var ids = (findingIds ?? []).Distinct().ToArray();
            if (ids.Length == 0)
            {
                throw DomainException.Validation(new ValidationResult().Add("findingIds", ProblemCodes.Fields.Required));
            }

            var findings = await db.Findings.Where(f => ids.Contains(f.Id)).ToListAsync(ct).ConfigureAwait(false);
            if (findings.Count != ids.Length)
            {
                throw new DomainException(ProblemCodes.FindingNotFound, 404);
            }

            var shopIds = findings.Select(f => f.ShopId).Distinct().ToArray();
            foreach (var shop in await db.Shops.AsNoTracking().Where(s => shopIds.Contains(s.Id)).ToListAsync(ct).ConfigureAwait(false))
            {
                SampleOnlyGuard.Ensure(shop);
            }

            var pagesOf = await PagesAsync(findings, ct).ConfigureAwait(false);
            var existing = await db.EvidenceLinks.Where(l => l.EvidenceId == evidenceId && l.FindingId != null && ids.Contains(l.FindingId!.Value))
                .Select(l => new { l.FindingId, l.PageId }).ToListAsync(ct).ConfigureAwait(false);
            foreach (var finding in findings)
            {
                if (finding.Status != FindingStatus.KeptWithEvidence)
                {
                    await transitions.ApplyAsync(finding, FindingStatus.KeptWithEvidence, userId, ct, "evidence_linked").ConfigureAwait(false);
                }

                var pages = pagesOf[finding.Id];
                foreach (var page in pages.Count > 0 ? pages.Select(p => (Guid?)p) : [null])
                {
                    if (!existing.Any(e => e.FindingId == finding.Id && e.PageId == page))
                    {
                        db.EvidenceLinks.Add(new EvidenceLink { EvidenceId = evidenceId, ShopId = finding.ShopId, PageId = page, FindingId = finding.Id });
                    }
                }
            }

            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            foreach (var finding in findings)
            {
                await memory.RecordAsync(finding.ShopId, finding.SegmentHash, finding.Text, Decision.KeepWithEvidence, userId, ct, evidenceId: evidenceId).ConfigureAwait(false);
            }

            await audit.WriteAsync(new AuditEvent(AuditActions.EvidenceLinked, tenantId, userId, "evidence", evidenceId.ToString("D"),
                new JsonObject { ["findings"] = findings.Count }), ct).ConfigureAwait(false);
            return (await DtosAsync([item], detail: true, ct).ConfigureAwait(false))[0];
        }, ct).ConfigureAwait(false);

    /// <summary>Removes one use; a finding with no other evidence goes back to <c>open</c> and its remembered decision is superseded.</summary>
    public async Task UnlinkAsync(Guid tenantId, Guid userId, Guid evidenceId, Guid linkId, CancellationToken ct) =>
        await db.ExecuteInTenantTransactionAsync(async () =>
        {
            await RequireAsync(evidenceId, ct).ConfigureAwait(false);
            var link = await db.EvidenceLinks.FirstOrDefaultAsync(l => l.Id == linkId && l.EvidenceId == evidenceId, ct).ConfigureAwait(false)
                ?? throw new DomainException(ProblemCodes.EvidenceNotFound, 404);
            db.EvidenceLinks.Remove(link);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            var reopened = 0;
            if (link.FindingId is { } findingId
                && !await db.EvidenceLinks.AnyAsync(l => l.EvidenceId == evidenceId && l.FindingId == findingId, ct).ConfigureAwait(false))
            {
                var finding = await db.Findings.FirstAsync(f => f.Id == findingId, ct).ConfigureAwait(false);
                await memory.SupersedeAsync(finding.ShopId, finding.SegmentHash, ct, evidenceId: evidenceId).ConfigureAwait(false);

                // Kept with evidence only while some piece of evidence still holds it.
                var held = await db.EvidenceLinks.AnyAsync(l => l.FindingId == findingId
                    && db.EvidenceItems.Any(e => e.Id == l.EvidenceId && e.DeletedAt == null), ct).ConfigureAwait(false);
                if (!held && finding.Status == FindingStatus.KeptWithEvidence)
                {
                    await transitions.ApplyAsync(finding, FindingStatus.Open, userId, ct, "evidence_unlinked").ConfigureAwait(false);
                    reopened = 1;
                }
            }

            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            await audit.WriteAsync(new AuditEvent(AuditActions.EvidenceUnlinked, tenantId, userId, "evidence", evidenceId.ToString("D"),
                new JsonObject { ["findingsReopened"] = reopened }), ct).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

    /// <summary>The stored file, or <c>404 evidence.no_file</c>.</summary>
    public async Task<EvidenceFile> FileAsync(Guid evidenceId, CancellationToken ct) =>
        await db.ExecuteInTenantTransactionAsync(async () =>
        {
            var item = await db.EvidenceItems.AsNoTracking().FirstOrDefaultAsync(e => e.Id == evidenceId && e.DeletedAt == null, ct).ConfigureAwait(false)
                ?? throw new DomainException(ProblemCodes.EvidenceNotFound, 404);
            if (Key(item.TenantId, item.FileBlobKey) is not { } key || item.FileName is null || EvidenceFileValidator.ByExtension(item.FileName) is not { } type)
            {
                throw new DomainException(ProblemCodes.EvidenceNoFile, 404);
            }

            return new EvidenceFile(key, item.FileName, type.ContentType);
        }, ct).ConfigureAwait(false);

    /// <summary>The findings kept with the evidence go back to <c>open</c> unless another piece of evidence holds them; returns how many.</summary>
    private async Task<int> ReopenAsync(Guid userId, Guid evidenceId, string reason, CancellationToken ct)
    {
        var linked = db.EvidenceLinks.Where(l => l.EvidenceId == evidenceId && l.FindingId != null).Select(l => l.FindingId!.Value);
        var heldElsewhere = db.EvidenceLinks.Where(l => l.EvidenceId != evidenceId && l.FindingId != null
            && db.EvidenceItems.Any(e => e.Id == l.EvidenceId && e.DeletedAt == null)).Select(l => l.FindingId!.Value);
        var findings = await db.Findings.Where(f => linked.Contains(f.Id) && !heldElsewhere.Contains(f.Id) && f.Status == FindingStatus.KeptWithEvidence)
            .ToListAsync(ct).ConfigureAwait(false);
        foreach (var finding in findings)
        {
            await transitions.ApplyAsync(finding, FindingStatus.Open, userId, ct, reason).ConfigureAwait(false);
        }

        return findings.Count;
    }

    private async Task<(BlobKey Key, string Name)> StoreAsync(Guid tenantId, Guid evidenceId, EvidenceUpload upload, CancellationToken ct)
    {
        var max = options.Value.MaxFileBytes;
        if (upload.Length > max)
        {
            throw new DomainException(ProblemCodes.EvidenceFileTooLarge, 400, new Dictionary<string, object?> { ["maxBytes"] = max });
        }

        // The whole file is read once (at most MaxFileBytes): the type is checked on the bytes that are stored.
        using var buffer = new MemoryStream();
        await upload.Content.CopyToAsync(buffer, ct).ConfigureAwait(false);
        if (buffer.Length > max)
        {
            throw new DomainException(ProblemCodes.EvidenceFileTooLarge, 400, new Dictionary<string, object?> { ["maxBytes"] = max });
        }

        var type = EvidenceFileValidator.Detect(buffer.GetBuffer().AsSpan(0, (int)Math.Min(buffer.Length, EvidenceFileValidator.HeaderLength)))
            ?? throw new DomainException(ProblemCodes.EvidenceFileTypeNotAllowed, 400);
        var name = EvidenceFileValidator.SafeName(upload.FileName, type);
        var key = BlobKey.ForTenant(tenantId, "evidence", evidenceId.ToString("N"), name);
        buffer.Position = 0;
        await blobs.PutAsync(key, buffer, type.ContentType, ct).ConfigureAwait(false);
        return (key, name);
    }

    private async Task<EvidenceItem> RequireAsync(Guid evidenceId, CancellationToken ct) =>
        await db.EvidenceItems.FirstOrDefaultAsync(e => e.Id == evidenceId && e.DeletedAt == null, ct).ConfigureAwait(false)
            ?? throw new DomainException(ProblemCodes.EvidenceNotFound, 404);

    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new DomainException(ProblemCodes.ConcurrencyConflict, 409);
        }
    }

    private (EvidenceStatus Status, int? DaysToExpiry) StatusOf(EvidenceItem item) => EvidenceStatusCalculator.Calculate(
        item.Status, EvidenceStatusCalculator.Date(item.ValidUntil), EvidenceStatusCalculator.Today(time.GetUtcNow(), options.Value.TimeZone), options.Value.ExpiringDays);

    private async Task<List<EvidenceDto>> DtosAsync(IReadOnlyList<EvidenceItem> items, bool detail, CancellationToken ct)
    {
        var ids = items.Select(i => i.Id).ToArray();
        var links = await db.EvidenceLinks.AsNoTracking().Where(l => ids.Contains(l.EvidenceId)).ToListAsync(ct).ConfigureAwait(false);
        var users = items.Select(i => i.CreatedBy).OfType<Guid>().Distinct().ToArray();
        var names = users.Length == 0 ? [] : await db.Users.AsNoTracking().Where(u => users.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct).ConfigureAwait(false);
        var questions = await db.Questions.AsNoTracking().Where(q => q.EvidenceId != null && ids.Contains(q.EvidenceId.Value)).Select(q => new { q.EvidenceId, q.Id })
            .ToListAsync(ct).ConfigureAwait(false);
        Dictionary<Guid, string?> titles = [];
        if (detail)
        {
            var pageIds = links.Select(l => l.PageId).OfType<Guid>().Distinct().ToArray();
            titles = await db.Pages.AsNoTracking().Where(p => pageIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Title, ct).ConfigureAwait(false);
        }

        return items.Select(item =>
        {
            var own = links.Where(l => l.EvidenceId == item.Id).ToList();
            var (status, days) = StatusOf(item);
            return new EvidenceDto(
                item.Id, questions.FirstOrDefault(q => q.EvidenceId == item.Id)?.Id, item.ClaimText, FindingMapper.Text(item.SubjectKind), item.SubjectLabel,
                FindingMapper.Text(item.Kind), item.Title, item.FileName, item.FileBlobKey is not null, FindingMapper.Text(item.Source),
                EvidenceStatusCalculator.Date(item.ValidFrom), EvidenceStatusCalculator.Date(item.ValidUntil), FindingMapper.Text(status), days,
                Links(own), item.CreatedBy is { } by ? new FindingActorDto(by, names.GetValueOrDefault(by)) : null, item.CreatedAt, item.Version,
                detail ? own.Select(l => new EvidenceLinkDto(l.Id, l.ShopId, l.PageId, l.PageId is { } p ? titles.GetValueOrDefault(p) : null, l.FindingId)).ToList() : null);
        }).ToList();
    }

    /// <summary>Open questions about a finding, one row per text (code and fingerprint), as „Čaká na odpoveď“.</summary>
    private async Task<List<EvidenceDto>> AwaitingAsync(string? q, Guid? shopId, CancellationToken ct)
    {
        var open = await db.Questions.AsNoTracking().Where(x => x.Status == QuestionStatus.Open && x.Scope == QuestionScope.Finding && x.FindingId != null
                && (shopId == null || x.ShopId == shopId))
            .Join(db.Findings.AsNoTracking(), x => x.FindingId, f => f.Id, (x, f) => new { Question = x, f.SegmentHash, f.Text })
            .Join(db.Shops.AsNoTracking().Where(s => s.Status != ShopStatus.Draft && s.Status != ShopStatus.Sample), x => x.Question.ShopId, s => s.Id, (x, s) => x)
            .OrderBy(x => x.Question.Id).ToListAsync(ct).ConfigureAwait(false);
        var rows = new List<EvidenceDto>();
        foreach (var group in open.GroupBy(x => (x.Question.Code, Key: x.SegmentHash?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? x.Question.Id.ToString("N"))))
        {
            var first = group.First();
            var claim = first.Text ?? first.Question.Code;
            if (!string.IsNullOrWhiteSpace(q) && !claim.Contains(q.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var applies = await propagation.CountAsync(group.Select(x => x.Question).ToList(), ct).ConfigureAwait(false);
            var shops = group.Select(x => x.Question.ShopId).Distinct().Count();
            rows.Add(new EvidenceDto(
                first.Question.Id, first.Question.Id, claim, "product", Label(first.Question.Params), "answer", null, null, false, "answer", null, null,
                "awaiting_answer", null, new EvidenceLinksDto(applies.Findings, applies.Pages, shops), null, first.Question.CreatedAt, 0));
        }

        return rows;
    }

    private async Task<Dictionary<Guid, List<Guid>>> PagesAsync(IReadOnlyList<Finding> findings, CancellationToken ct)
    {
        var ids = findings.Select(f => f.Id).ToArray();
        var occurrences = await db.FindingOccurrences.AsNoTracking().Where(o => ids.Contains(o.FindingId)).Select(o => new { o.FindingId, o.PageId })
            .ToListAsync(ct).ConfigureAwait(false);
        return findings.ToDictionary(
            f => f.Id,
            f => occurrences.Where(o => o.FindingId == f.Id).Select(o => o.PageId).Concat(f.PageId is { } own ? [own] : []).Distinct().ToList());
    }

    /// <summary>The stored key when it lies under the tenant; otherwise null (never a file of another tenant).</summary>
    private static BlobKey? Key(Guid tenantId, string? value)
    {
        var segments = value?.Split('/') ?? [];
        if (segments.Length < 3 || segments[0] != "tenants" || segments[1] != tenantId.ToString("D"))
        {
            return null;
        }

        try
        {
            return BlobKey.ForTenant(tenantId, segments[2..]);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static EvidenceLinksDto Links(IReadOnlyList<EvidenceLink> links) => new(
        links.Select(l => l.FindingId).OfType<Guid>().Distinct().Count(),
        links.Select(l => l.PageId).OfType<Guid>().Distinct().Count(),
        links.Select(l => l.ShopId).Distinct().Count());

    private static void EnsureDates(DateOnly? from, DateOnly? until)
    {
        if (from is { } f && until is { } u && u < f)
        {
            throw new DomainException(ProblemCodes.EvidenceValidUntilBeforeFrom, 400);
        }
    }

    private static int Order(string status) => status switch
    {
        "expiring" => 0,
        "expired" => 1,
        "awaiting_answer" => 2,
        "claim_removed" => 3,
        _ => 4,
    };

    private static T SnakeEnum<T>(string text)
        where T : struct, Enum => Enum.Parse<T>(text.Replace("_", "", StringComparison.Ordinal), ignoreCase: true);

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? Label(JsonDocument? parameters) =>
        parameters?.RootElement is { ValueKind: JsonValueKind.Object } root
            ? root.EnumerateObject().Select(p => p.Value.ValueKind switch
            {
                JsonValueKind.String => p.Value.GetString(),
                JsonValueKind.Number => p.Value.GetRawText(),
                _ => null,
            }).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))
            : null;
}
