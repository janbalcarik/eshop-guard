using System.Text.Json;
using System.Text.Json.Nodes;
using EshopGuard.Application.Audit;
using EshopGuard.Application.Findings;
using EshopGuard.Application.Options;
using EshopGuard.Data;
using EshopGuard.Data.Entities.Checks;
using EshopGuard.Data.Entities.Fixes;
using EshopGuard.Data.Entities.Shops;
using EshopGuard.Jobs.Fixes;
using EshopGuard.Jobs.Protocols;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EshopGuard.Application.Protocols;

/// <summary>The content of a protocol, its numbers for <c>protocols.summary</c> and the rule sets it names.</summary>
public sealed record ProtocolBuild(ProtocolDocument Document, JsonObject Summary, Guid[] RuleSetIds);

/// <summary>
/// Builds the content of a protocol from the database (change 11, AD 11; design <c>Protocol.dc.html</c>), in the tenant
/// transaction of the request, as it is on the day of issue:
/// <list type="bullet">
/// <item>the head: e-shop, period, the initial check (date, pages, pages not checked by reason), the rule sets and countries of
///       the runs of the period (with the last run before it);</item>
/// <item>the summary of the findings now (initial, fixed and published, kept with evidence, kept by decision, approved and
///       waiting for publication, waiting for a decision);</item>
/// <item>the decisions of the period: the current decisions of the memory (one per sentence; the bulk ones with their pages)
///       and the decisions about findings without a sentence of their own (from <c>finding.status_changed</c>); the point of
///       the law from the verdicts, in the language of the law;</item>
/// <item>the evidence of the operator linked to the e-shop, and the statement.</item>
/// </list>
/// Days are those of <c>Localization:TimeZone</c>. Nothing is left out silently: a reason of pages not checked without a text
/// is named by its code.
/// </summary>
public sealed class ProtocolDocumentBuilder(EshopGuardDb db, RuleTextCatalog ruleTexts, IOptions<LocalizationOptions> options)
{
    private static readonly FindingStatus[] Decided =
        [FindingStatus.Kept, FindingStatus.Dismissed, FindingStatus.KeptWithEvidence, FindingStatus.Approved, FindingStatus.Published, FindingStatus.Resolved];

    public async Task<ProtocolBuild> BuildAsync(
        Shop shop, string number, ProtocolTexts texts, DateOnly from, DateOnly to, DateTimeOffset issuedAt, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(shop);
        ArgumentNullException.ThrowIfNull(texts);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZone);
        DateOnly Day(DateTimeOffset at) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, zone).DateTime);
        DateTimeOffset Start(DateOnly day)
        {
            var local = day.ToDateTime(TimeOnly.MinValue);
            return new DateTimeOffset(local, zone.GetUtcOffset(local)).ToUniversalTime();
        }

        var start = Start(from);
        var end = Start(to.AddDays(1));

        var runs = await db.Runs.AsNoTracking()
            .Where(r => r.ShopId == shop.Id && (r.Status == RunStatus.Finished || r.Status == RunStatus.Partial) && r.FinishedAt != null && r.FinishedAt < end)
            .OrderBy(r => r.FinishedAt).ToListAsync(ct).ConfigureAwait(false);
        var initial = runs.FirstOrDefault(r => r.Kind == RunKind.FullAnalysis) ?? runs.FirstOrDefault(r => r.Kind == RunKind.FreeSample);
        var considered = runs.Where(r => r.FinishedAt >= start).ToList();
        if (runs.LastOrDefault(r => r.FinishedAt < start) is { } before)
        {
            considered.Insert(0, before);
        }

        var ruleSetIds = considered.SelectMany(r => r.RuleSetIds).Distinct().ToArray();
        var ruleSets = await db.RuleSets.AsNoTracking().Where(r => ruleSetIds.Contains(r.Id)).Select(r => new { r.Module, r.Version }).ToListAsync(ct).ConfigureAwait(false);
        var countries = considered.SelectMany(r => r.Jurisdictions).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

        var facts = new List<ProtocolFact>
        {
            new("shop", texts.Text("facts.shop"), shop.Domain),
            new("period", texts.Text("facts.period"), ProtocolTexts.Period(from, to)),
            new("initial_check", texts.Text("facts.initial_check"), initial is null
                ? texts.Text("facts.initial_check_none")
                : texts.Format("facts.initial_check_value", ("date", ProtocolTexts.Date(Day(initial.FinishedAt!.Value))), ("pages", texts.Count(PagesChecked(initial), "pages")))),
        };
        if (initial is not null && Unchecked(initial, texts) is { Length: > 0 } notChecked)
        {
            facts.Add(new ProtocolFact("unchecked", texts.Text("facts.unchecked"), notChecked));
        }

        facts.Add(new ProtocolFact("rules", texts.Text("facts.rules"),
            ruleSets.Count == 0 ? texts.Text("decisions.no_reference") : string.Join(", ", ruleSets.OrderBy(r => r.Module, StringComparer.Ordinal).ThenBy(r => r.Version, StringComparer.Ordinal).Select(r => r.Version))));
        facts.Add(new ProtocolFact("countries", texts.Text("facts.countries"),
            countries.Count == 0 ? texts.Text("decisions.no_reference") : string.Join(", ", countries.Select(c => texts.TryText("countries." + c) ?? c.ToUpperInvariant()))));

        var (summary, summaryJson) = await SummaryAsync(shop.Id, initial?.Id, texts, ct).ConfigureAwait(false);
        var rows = new List<(DateTimeOffset At, ProtocolRow Row)>();
        rows.AddRange(await MemoryRowsAsync(shop.Id, start, end, texts, Day, ct).ConfigureAwait(false));
        rows.AddRange(await SiteRowsAsync(shop, start, end, texts, Day, ruleSetIds, ct).ConfigureAwait(false));
        var evidence = await EvidenceAsync(shop.Id, end, texts, Day, ct).ConfigureAwait(false);
        summaryJson["decisions"] = rows.Count;
        summaryJson["evidence"] = evidence.Count;

        var document = new ProtocolDocument(
            number, texts.Locale, texts.Text("title"), texts.Text("heading"), texts.Format("number", ("number", number)),
            texts.Format("issued", ("date", ProtocolTexts.Date(Day(issuedAt)))), facts, summary,
            new ProtocolTable(texts.Text("decisions.title"), texts.List("decisions.headers"), rows.OrderBy(r => r.At).Select(r => r.Row).ToList(),
                rows.Count == 0 ? texts.Text("decisions.empty") : null),
            new ProtocolSection(texts.Text("evidence.title"), evidence, evidence.Count == 0 ? texts.Text("evidence.empty") : null),
            texts.Text("disclaimer"), number + ".pdf");
        return new ProtocolBuild(document, summaryJson, ruleSetIds);
    }

    private static int PagesChecked(Run run) =>
        run.Stats?.RootElement is { ValueKind: JsonValueKind.Object } stats && stats.TryGetProperty("pages_checked", out var pages) && pages.TryGetInt32(out var count) ? count : 0;

    /// <summary>Every reason of pages not checked with its count; a reason without a text is named by its code.</summary>
    private static string Unchecked(Run run, ProtocolTexts texts)
    {
        if (run.Stats?.RootElement is not { ValueKind: JsonValueKind.Object } stats || !stats.TryGetProperty("unchecked", out var reasons)
            || reasons.ValueKind != JsonValueKind.Object)
        {
            return "";
        }

        var parts = reasons.EnumerateObject().Where(r => r.Value.TryGetInt32(out var n) && n > 0).OrderBy(r => r.Name, StringComparer.Ordinal)
            .Select(r => (texts.TryText("unchecked." + r.Name) ?? texts.Format("unchecked.other", ("code", r.Name))) + ": " + ProtocolTexts.Number(r.Value.GetInt32()));
        return string.Join("; ", parts);
    }

    private async Task<(IReadOnlyList<ProtocolSummaryItem> Items, JsonObject Json)> SummaryAsync(Guid shopId, Guid? initialRunId, ProtocolTexts texts, CancellationToken ct)
    {
        var counts = await db.Findings.AsNoTracking().Where(f => f.ShopId == shopId).GroupBy(f => f.Status).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count, ct).ConfigureAwait(false);
        int Of(params FindingStatus[] statuses) => statuses.Sum(s => counts.GetValueOrDefault(s));
        var initial = initialRunId is { } runId ? await db.Findings.CountAsync(f => f.ShopId == shopId && f.FirstRunId == runId, ct).ConfigureAwait(false) : 0;
        var values = new (string Code, int Value)[]
        {
            ("initial", initial),
            ("fixed", Of(FindingStatus.Published, FindingStatus.Resolved)),
            ("kept_with_evidence", Of(FindingStatus.KeptWithEvidence)),
            ("kept", Of(FindingStatus.Kept, FindingStatus.Dismissed)),
            ("approved", Of(FindingStatus.Approved)),
            ("waiting", Of(FindingStatus.Open, FindingStatus.NeedsAnswer, FindingStatus.Proposed)),
        };
        var json = new JsonObject();
        foreach (var (code, value) in values)
        {
            json[code] = value;
        }

        return (values.Select(v => new ProtocolSummaryItem(v.Code, v.Value, texts.Text("summary." + v.Code))).ToList(), json);
    }

    /// <summary>The current decisions of the memory made in the period: one row per sentence.</summary>
    private async Task<List<(DateTimeOffset, ProtocolRow)>> MemoryRowsAsync(
        Guid shopId, DateTimeOffset start, DateTimeOffset end, ProtocolTexts texts, Func<DateTimeOffset, DateOnly> day, CancellationToken ct)
    {
        var memory = await db.DecisionMemory.AsNoTracking()
            .Where(m => m.ShopId == shopId && m.SupersededAt == null && m.CreatedAt >= start && m.CreatedAt < end)
            .OrderBy(m => m.CreatedAt).ToListAsync(ct).ConfigureAwait(false);
        var rows = new List<(DateTimeOffset, ProtocolRow)>();
        if (memory.Count == 0)
        {
            return rows;
        }

        var hashes = memory.Select(m => m.SegmentHash).Distinct().ToArray();
        var findings = await db.Findings.AsNoTracking().Where(f => f.ShopId == shopId && f.SegmentHash != null && hashes.Contains(f.SegmentHash.Value))
            .Select(f => new { f.Id, Hash = f.SegmentHash!.Value, f.PageId, f.LegalRefs }).ToListAsync(ct).ConfigureAwait(false);
        var findingIds = findings.Select(f => f.Id).ToArray();
        var occurrences = await db.FindingOccurrences.AsNoTracking().Where(o => findingIds.Contains(o.FindingId)).Select(o => new { o.FindingId, o.PageId })
            .ToListAsync(ct).ConfigureAwait(false);
        var groups = await db.FixGroups.AsNoTracking().Where(g => g.ShopId == shopId && g.SegmentHash != null && hashes.Contains(g.SegmentHash.Value))
            .Select(g => new { g.Id, Hash = g.SegmentHash!.Value }).ToListAsync(ct).ConfigureAwait(false);
        var groupIds = groups.Select(g => g.Id).ToArray();
        var groupProposals = await db.FixProposals.AsNoTracking().Where(p => p.ShopId == shopId && p.GroupId != null && groupIds.Contains(p.GroupId.Value))
            .Select(p => new { p.Id, GroupId = p.GroupId!.Value }).ToListAsync(ct).ConfigureAwait(false);
        var published = await db.Publications.AsNoTracking().Where(p => p.ShopId == shopId && p.Status == PublicationStatus.Published && p.PublishedAt != null)
            .Select(p => new { p.FixProposalIds, p.PublishedAt }).ToListAsync(ct).ConfigureAwait(false);
        var pagesOf = new Dictionary<long, HashSet<Guid>>();
        foreach (var finding in findings)
        {
            var set = pagesOf.TryGetValue(finding.Hash, out var existing) ? existing : pagesOf[finding.Hash] = [];
            set.UnionWith(occurrences.Where(o => o.FindingId == finding.Id).Select(o => o.PageId));
            if (finding.PageId is { } own)
            {
                set.Add(own);
            }
        }

        var titles = await TitlesAsync(shopId, pagesOf.Values.Where(p => p.Count == 1).SelectMany(p => p), ct).ConfigureAwait(false);
        var names = await NamesAsync(memory.Select(m => m.CreatedBy), ct).ConfigureAwait(false);
        var evidence = await EvidenceNamesAsync(memory.Select(m => m.EvidenceId), texts, ct).ConfigureAwait(false);
        foreach (var entry in memory)
        {
            var pages = pagesOf.GetValueOrDefault(entry.SegmentHash) ?? [];
            var bulk = entry.SourceProposalId is null && entry.Decision is Decision.Replace or Decision.Remove && groups.Any(g => g.Hash == entry.SegmentHash);
            var pagesText = pages.Count switch
            {
                0 => texts.Text("decisions.whole_site"),
                1 when !bulk => titles.GetValueOrDefault(pages.First()) ?? texts.Count(1, "pages"),
                _ => bulk ? texts.Format("decisions.bulk", ("pages", texts.Count(pages.Count, "pages"))) : texts.Count(pages.Count, "pages"),
            };
            var proposals = entry.SourceProposalId is { } source
                ? [source]
                : groupProposals.Where(p => groups.Any(g => g.Id == p.GroupId && g.Hash == entry.SegmentHash)).Select(p => p.Id).ToHashSet();
            var publishedAt = published.Where(p => p.FixProposalIds.Any(proposals.Contains)).Select(p => p.PublishedAt).Max();
            var fix = entry.Decision switch
            {
                Decision.Replace => texts.Format("decisions.replaced", ("text", entry.ReplacementText ?? "")),
                Decision.Remove => texts.Text("decisions.removed"),
                Decision.KeepWithEvidence => texts.Format("decisions.kept_with_evidence", ("evidence", evidence.GetValueOrDefault(entry.EvidenceId ?? Guid.Empty) ?? "")),
                _ => names.GetValueOrDefault(entry.CreatedBy ?? Guid.Empty) is { } name ? texts.Format("decisions.kept_by", ("name", name)) : texts.Text("decisions.kept"),
            };
            if (publishedAt is { } at && entry.Decision is Decision.Replace or Decision.Remove)
            {
                fix += texts.Format("decisions.published", ("date", ProtocolTexts.Date(day(at))));
            }

            var references = References(findings.Where(f => f.Hash == entry.SegmentHash).Select(f => f.LegalRefs), texts);
            rows.Add((entry.CreatedAt, new ProtocolRow(ProtocolTexts.Date(day(entry.CreatedAt)), pagesText, "„" + entry.NormalizedText + "“", fix, references)));
        }

        return rows;
    }

    /// <summary>
    /// Decisions of the period about findings without a sentence of their own (the whole site, a page): the last change of
    /// their state in the audit, when it is a decision.
    /// </summary>
    private async Task<List<(DateTimeOffset, ProtocolRow)>> SiteRowsAsync(
        Shop shop, DateTimeOffset start, DateTimeOffset end, ProtocolTexts texts, Func<DateTimeOffset, DateOnly> day, Guid[] ruleSetIds, CancellationToken ct)
    {
        var findings = await db.Findings.AsNoTracking().Where(f => f.ShopId == shop.Id && f.SegmentHash == null && Decided.Contains(f.Status))
            .Select(f => new { f.Id, f.RuleSetId, f.RuleId, f.PageId, f.Text, f.Status, f.LegalRefs }).ToListAsync(ct).ConfigureAwait(false);
        var rows = new List<(DateTimeOffset, ProtocolRow)>();
        if (findings.Count == 0)
        {
            return rows;
        }

        var ids = findings.Select(f => f.Id.ToString("D")).ToArray();
        var changes = await db.AuditLog.AsNoTracking()
            .Where(a => a.TenantId == shop.TenantId && a.Action == AuditActions.FindingStatusChanged && a.EntityType == "finding" && a.EntityId != null
                && ids.Contains(a.EntityId) && a.At >= start && a.At < end)
            .Select(a => new { a.EntityId, a.At, a.ActorUserId }).ToListAsync(ct).ConfigureAwait(false);
        var last = changes.GroupBy(c => c.EntityId!).ToDictionary(g => Guid.Parse(g.Key), g => g.OrderBy(c => c.At).Last());
        if (last.Count == 0)
        {
            return rows;
        }

        var titles = await TitlesAsync(shop.Id, findings.Where(f => f.PageId != null).Select(f => f.PageId!.Value), ct).ConfigureAwait(false);
        var names = await NamesAsync(last.Values.Select(c => c.ActorUserId), ct).ConfigureAwait(false);
        var ruleTitles = await ruleTexts.TitlesAsync(texts.Locale, [.. findings.Select(f => f.RuleSetId).Distinct().Union(ruleSetIds)], ct).ConfigureAwait(false);
        var decidedIds = last.Keys.ToArray();
        var proposals = await db.FixProposals.AsNoTracking()
            .Where(p => p.ShopId == shop.Id && (p.Status == FixProposalStatus.Accepted || p.Status == FixProposalStatus.Published))
            .Where(p => p.FindingIds.Any(id => decidedIds.Contains(id))).ToListAsync(ct).ConfigureAwait(false);
        var links = await db.EvidenceLinks.AsNoTracking().Where(l => l.ShopId == shop.Id && l.FindingId != null && decidedIds.Contains(l.FindingId.Value))
            .Select(l => new { FindingId = l.FindingId!.Value, l.EvidenceId }).ToListAsync(ct).ConfigureAwait(false);
        var evidence = await EvidenceNamesAsync(links.Select(l => (Guid?)l.EvidenceId), texts, ct).ConfigureAwait(false);
        var published = await db.Publications.AsNoTracking().Where(p => p.ShopId == shop.Id && p.Status == PublicationStatus.Published && p.PublishedAt != null)
            .Select(p => new { p.FixProposalIds, p.PublishedAt }).ToListAsync(ct).ConfigureAwait(false);
        foreach (var finding in findings.Where(f => last.ContainsKey(f.Id)))
        {
            var change = last[finding.Id];
            var proposal = proposals.Where(p => p.FindingIds.Contains(finding.Id)).OrderByDescending(p => p.DecidedAt).FirstOrDefault();
            var fix = finding.Status switch
            {
                FindingStatus.KeptWithEvidence => texts.Format("decisions.kept_with_evidence",
                    ("evidence", string.Join(", ", links.Where(l => l.FindingId == finding.Id).Select(l => evidence.GetValueOrDefault(l.EvidenceId)).OfType<string>().Distinct()))),
                FindingStatus.Kept or FindingStatus.Dismissed => names.GetValueOrDefault(change.ActorUserId ?? Guid.Empty) is { } name
                    ? texts.Format("decisions.kept_by", ("name", name))
                    : texts.Text("decisions.kept"),
                _ when proposal is not null && ProposalText.Text(proposal).Trim() is { Length: > 0 } text => texts.Format("decisions.replaced", ("text", text)),
                _ => texts.Text("decisions.removed"),
            };
            if (proposal is not null && finding.Status is FindingStatus.Approved or FindingStatus.Published or FindingStatus.Resolved
                && published.Where(p => p.FixProposalIds.Contains(proposal.Id)).Select(p => p.PublishedAt).Max() is { } at)
            {
                fix += texts.Format("decisions.published", ("date", ProtocolTexts.Date(day(at))));
            }

            var before = finding.Text is { Length: > 0 } own ? "„" + own + "“" : ruleTitles.GetValueOrDefault((finding.RuleSetId, finding.RuleId)) ?? finding.RuleId;
            var pages = finding.PageId is { } page ? titles.GetValueOrDefault(page) ?? texts.Count(1, "pages") : texts.Text("decisions.whole_site");
            rows.Add((change.At, new ProtocolRow(ProtocolTexts.Date(day(change.At)), pages, before, fix, References([finding.LegalRefs], texts))));
        }

        return rows;
    }

    /// <summary>The evidence of the operator linked to the e-shop and recorded before the end of the period.</summary>
    private async Task<List<string>> EvidenceAsync(Guid shopId, DateTimeOffset end, ProtocolTexts texts, Func<DateTimeOffset, DateOnly> day, CancellationToken ct)
    {
        var links = await db.EvidenceLinks.AsNoTracking().Where(l => l.ShopId == shopId).Select(l => new { l.EvidenceId, l.PageId }).ToListAsync(ct).ConfigureAwait(false);
        var ids = links.Select(l => l.EvidenceId).Distinct().ToArray();
        var items = await db.EvidenceItems.AsNoTracking()
            .Where(e => ids.Contains(e.Id) && e.CreatedAt < end && e.Status != EvidenceStatus.AwaitingAnswer && e.Status != EvidenceStatus.ClaimRemoved)
            .OrderBy(e => e.CreatedAt).ThenBy(e => e.Id).ToListAsync(ct).ConfigureAwait(false);
        return items.Select(item =>
        {
            var subject = item.SubjectLabel is { Length: > 0 } label ? texts.Format("evidence.subject." + FindingMapper.Text(item.SubjectKind), ("label", label)) : "";
            var validity = item.ValidUntil is { } until
                ? texts.Format(item.Status == EvidenceStatus.Expired ? "evidence.expired" : "evidence.valid_until", ("date", ProtocolTexts.Date(day(until))))
                : "";
            var products = links.Where(l => l.EvidenceId == item.Id && l.PageId != null).Select(l => l.PageId).Distinct().Count();
            return texts.Format("evidence.line", ("claim", item.ClaimText), ("subject", subject), ("kind", Kind(item, texts)), ("validity", validity),
                ("date", ProtocolTexts.Date(day(item.CreatedAt))), ("products", texts.Count(products, "products")));
        }).ToList();
    }

    private static string Kind(EvidenceItem item, ProtocolTexts texts)
    {
        var kind = texts.Text("evidence.kind." + FindingMapper.Text(item.Kind));
        return item.Title is { Length: > 0 } title ? kind + " " + title : kind;
    }

    /// <summary>„certifikát Vegan“: the kind and the title of every evidence.</summary>
    private async Task<Dictionary<Guid, string>> EvidenceNamesAsync(IEnumerable<Guid?> ids, ProtocolTexts texts, CancellationToken ct)
    {
        var wanted = ids.OfType<Guid>().Distinct().ToArray();
        return wanted.Length == 0
            ? []
            : (await db.EvidenceItems.AsNoTracking().IgnoreQueryFilters([EshopGuardDb.SoftDeleteFilter]).Where(e => wanted.Contains(e.Id)).ToListAsync(ct).ConfigureAwait(false))
                .ToDictionary(e => e.Id, e => Kind(e, texts));
    }

    private async Task<Dictionary<Guid, string>> TitlesAsync(Guid shopId, IEnumerable<Guid> pageIds, CancellationToken ct)
    {
        var ids = pageIds.Distinct().ToArray();
        return ids.Length == 0
            ? []
            : await db.Pages.AsNoTracking().Where(p => p.ShopId == shopId && ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Title ?? p.Path ?? p.Url, ct).ConfigureAwait(false);
    }

    private async Task<Dictionary<Guid, string>> NamesAsync(IEnumerable<Guid?> userIds, CancellationToken ct)
    {
        var ids = userIds.OfType<Guid>().Distinct().ToArray();
        return ids.Length == 0
            ? []
            : await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id) && u.DisplayName != null).ToDictionaryAsync(u => u.Id, u => u.DisplayName!, ct).ConfigureAwait(false);
    }

    /// <summary>The points of the law of the verdicts (<c>legal_refs[].ref</c>), as the law words them; „–“ without any.</summary>
    private static string References(IEnumerable<JsonDocument?> legalRefs, ProtocolTexts texts)
    {
        var refs = legalRefs.Where(r => r?.RootElement.ValueKind == JsonValueKind.Array)
            .SelectMany(r => r!.RootElement.EnumerateArray())
            .Select(r => r.ValueKind == JsonValueKind.Object && r.TryGetProperty("ref", out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null)
            .OfType<string>().Distinct(StringComparer.Ordinal).ToList();
        return refs.Count == 0 ? texts.Text("decisions.no_reference") : string.Join("; ", refs);
    }
}
