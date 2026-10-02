using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using EshopGuard.Application.Audit;
using EshopGuard.Application.Contracts;
using EshopGuard.Application.Problems;
using EshopGuard.Application.Protocols;
using EshopGuard.Data;
using EshopGuard.Data.Tenancy;
using Microsoft.Extensions.Options;

namespace EshopGuard.Application.Findings;

/// <summary>Settings of the lists of findings (<c>Findings</c>).</summary>
public sealed class FindingsOptions
{
    public const string SectionName = "Findings";

    /// <summary>Most rows of one export (AD 15).</summary>
    public int ExportMaxRows { get; set; } = 50_000;
}

/// <summary>
/// The export of findings (change 11, AD 15): the same filters as the list, UTF-8 with BOM, separator <c>;</c>, headers and
/// the names of the rules in the language of the user, verdicts by country, provisions in the language of the law. The text
/// of a finding is a copy from the customer's own web and stays in the file for the tenant only. At most
/// <c>Findings:ExportMaxRows</c> rows; when there are more, <see cref="CsvExport.Truncated"/> says so (the API sends a header).
/// The export is written to the audit (<c>findings.exported</c>, count only).
/// </summary>
public sealed class FindingsCsvExporter(
    EshopGuardDb db, ShopWorkLoader loader, RuleTextCatalog catalog, SecurityAuditWriter audit, IOptions<FindingsOptions> options)
{
    public async Task<CsvExport> ExportAsync(Guid userId, Guid shopId, FindingFilter filter, string locale, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var statuses = filter.Validate();
        var texts = DocumentTexts.For(locale) ?? throw new DomainException(ProblemCodes.LocaleNotEnabled, 400, new Dictionary<string, object?> { ["locale"] = locale });
        return await db.ExecuteInTenantTransactionAsync(async () =>
        {
            var work = await loader.LoadAsync(shopId, ct).ConfigureAwait(false);
            var rows = FindingQueryService.Rows(work, filter, statuses).Select(r => r.Item).ToList();
            var max = Math.Max(1, options.Value.ExportMaxRows);
            var truncated = rows.Count > max;
            rows = rows.Take(max).ToList();
            var titles = await catalog.TitlesAsync(locale, rows.Select(r => r.RuleSetId).Distinct().ToList(), ct).ConfigureAwait(false);

            var csv = new StringBuilder();
            var columns = texts.Csv.Columns;
            Line(csv, new[] { "text", "page", "url", "language", "group", "verdicts", "provisions", "status", "rule" }.Select(c => columns.GetValueOrDefault(c, c)));
            foreach (var row in rows)
            {
                Line(csv,
                [
                    row.Text ?? "",
                    row.Page?.Title ?? (row.Page is null ? texts.WholeSite : ""),
                    row.Page?.Url ?? "",
                    row.Page?.Language ?? "",
                    texts.Group(row.Strictest?.Checkability ?? "verify"),
                    string.Join(", ", row.Verdicts.Select(v => $"{v.Jurisdiction.ToUpperInvariant()} · {texts.Group(v.Checkability)} ({texts.Severity(v.Severity)})")),
                    string.Join(" | ", row.Verdicts.SelectMany(v => Refs(v.LegalRefs)).Distinct(StringComparer.Ordinal)),
                    texts.Status(row.Status),
                    titles.GetValueOrDefault((row.RuleSetId, row.RuleId), row.RuleId),
                ]);
            }

            await audit.WriteAsync(new AuditEvent(AuditActions.FindingsExported, work.Shop.TenantId, userId, "shop", shopId.ToString("D"),
                new JsonObject { ["rows"] = rows.Count, ["truncated"] = truncated, ["locale"] = locale }), ct).ConfigureAwait(false);
            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
            return new CsvExport(bytes, $"nalezy-{work.Shop.Domain}.csv", rows.Count, truncated);
        }, ct).ConfigureAwait(false);
    }

    private static IEnumerable<string> Refs(JsonElement? refs) =>
        refs is { ValueKind: JsonValueKind.Array } array
            ? array.EnumerateArray().Select(r => r.ValueKind == JsonValueKind.Object && r.TryGetProperty("ref", out var text) && text.ValueKind == JsonValueKind.String ? text.GetString() : null).OfType<string>()
            : [];

    private static void Line(StringBuilder csv, IEnumerable<string> values)
    {
        csv.Append(string.Join(';', values.Select(Quote))).Append("\r\n");
    }

    private static string Quote(string value) =>
        value.IndexOfAny([';', '"', '\r', '\n']) >= 0 ? "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"" : value;
}

/// <summary>The file of an export: bytes, file name, rows and whether the limit cut it.</summary>
public sealed record CsvExport(byte[] Content, string FileName, int Rows, bool Truncated);
