using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EshopGuard.Core.Models;
using EshopGuard.Core.Pipeline;
using EshopGuard.Core.Rules;
using Npgsql;
using NpgsqlTypes;
using CoreRuleSet = EshopGuard.Core.Rules.RuleSet;

namespace EshopGuard.Jobs.Runs;

/// <summary>Identity of a finding in an e-shop: the rule and, for a finding in a text, the fingerprint of the sentence (K rozhodnutí 3).</summary>
public sealed record FindingKey(string RuleId, string Scope, long? Fingerprint);

/// <summary>A finding of the run with its row in <c>checks.findings</c>.</summary>
public sealed record StoredFinding(Guid Id, FindingKey Key);

/// <summary>Order of the findings of the free sample: the strictest verdict (<c>VerdictOrder</c>), then score, occurrences and rule.</summary>
public static class SampleFindingOrder
{
    public static int Compare(Finding a, Finding b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        var verdict = VerdictOrder.Compare(a.Strictest, b.Strictest);
        if (verdict != 0)
        {
            return verdict;
        }

        var score = b.Score.CompareTo(a.Score);
        if (score != 0)
        {
            return score;
        }

        var occurrences = b.Occurrences.CompareTo(a.Occurrences);
        return occurrences != 0 ? occurrences : string.CompareOrdinal(a.RuleId, b.RuleId);
    }

    /// <summary>The findings from the most serious.</summary>
    public static List<Finding> Sort(IEnumerable<Finding> findings)
    {
        var list = findings.ToList();
        list.Sort(Compare);
        return list;
    }
}

/// <summary>
/// Writes the findings of a run: one row per rule and sentence of the e-shop (the same sentence on 38 pages is one finding
/// with 38 occurrences), verdicts of all jurisdictions, legal references and parameters of the strictest verdict. A finding
/// seen again keeps its state and its first run; only what the run saw changes. Occurrences are added, never doubled.
/// </summary>
internal static class FindingWriter
{
    /// <summary>The identity of a finding of the library.</summary>
    public static FindingKey Key(Finding finding) =>
        new(finding.RuleId, finding.Scope, finding.Scope == "site" ? null : finding.TextFingerprint ?? Fingerprint(finding.SegmentHash ?? finding.Text ?? ""));

    /// <summary>Findings of the library merged by their identity in the e-shop (the strictest first, pages together).</summary>
    public static List<(FindingKey Key, Finding Finding, IReadOnlyList<string> Urls)> Merge(IReadOnlyList<Finding> findings) =>
        findings.GroupBy(Key)
            .Select(g =>
            {
                var ordered = SampleFindingOrder.Sort(g);
                return (g.Key, ordered[0], (IReadOnlyList<string>)ordered.SelectMany(f => f.Urls).Distinct(StringComparer.Ordinal).ToList());
            })
            .ToList();

    public static async Task<List<StoredFinding>> WriteAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, RunAmbientScope scope, IReadOnlyList<Finding> findings, IReadOnlyList<CoreRuleSet> ruleSets,
        IReadOnlyDictionary<string, Guid> pageIds, CancellationToken ct)
    {
        var ruleSetIds = await RuleSetRowsAsync(connection, transaction, ruleSets, ct).ConfigureAwait(false);
        var stored = new List<StoredFinding>();
        foreach (var (key, finding, urls) in Merge(findings))
        {
            var strictest = finding.Strictest;
            var ruleSet = ruleSets.FirstOrDefault(s => s.Name == strictest.RuleSet);
            var ruleSetId = ruleSetIds[(ruleSet?.Module ?? finding.Module, ruleSet?.Version ?? strictest.RuleSetVersion)];
            var pageId = urls.Select(u => pageIds.TryGetValue(u, out var id) ? id : (Guid?)null).FirstOrDefault(id => id is not null);
            var conflict = key.Scope == "site" ? "(shop_id, rule_id) WHERE scope = 'site'" : "(shop_id, rule_id, segment_hash) WHERE scope = 'segment'";
            await using var command = new NpgsqlCommand(
                $"""
                INSERT INTO checks.findings (id, tenant_id, shop_id, rule_id, rule_set_id, module, checkability, severity, band, scope, segment_hash, page_id,
                    text, score, verdicts, legal_refs, params, status, occurrences, first_run_id, last_seen_run_id, created_at, updated_at)
                VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, 'open', $18, $19, $19, now(), now())
                ON CONFLICT {conflict} DO UPDATE SET rule_set_id = excluded.rule_set_id, module = excluded.module, checkability = excluded.checkability,
                    severity = excluded.severity, band = excluded.band, page_id = coalesce(excluded.page_id, checks.findings.page_id), text = excluded.text,
                    score = excluded.score, verdicts = excluded.verdicts, legal_refs = excluded.legal_refs, params = excluded.params,
                    occurrences = excluded.occurrences, last_seen_run_id = excluded.last_seen_run_id, updated_at = now()
                RETURNING id
                """, connection, transaction)
            {
                Parameters =
                {
                    new NpgsqlParameter { Value = Guid.CreateVersion7() },
                    new NpgsqlParameter { Value = scope.TenantId },
                    new NpgsqlParameter { Value = scope.ShopId },
                    new NpgsqlParameter { Value = finding.RuleId },
                    new NpgsqlParameter { Value = ruleSetId },
                    new NpgsqlParameter { Value = finding.Module },
                    new NpgsqlParameter { Value = Checkability(strictest.Checkability) },
                    new NpgsqlParameter { Value = strictest.Severity },
                    new NpgsqlParameter { Value = strictest.Band == FindingBand.High ? "high" : "review" },
                    new NpgsqlParameter { Value = key.Scope == "site" ? "site" : "segment" },
                    new NpgsqlParameter { Value = (object?)key.Fingerprint ?? DBNull.Value, NpgsqlDbType = NpgsqlDbType.Bigint },
                    new NpgsqlParameter { Value = (object?)pageId ?? DBNull.Value, NpgsqlDbType = NpgsqlDbType.Uuid },
                    new NpgsqlParameter { Value = (object?)finding.Text ?? DBNull.Value, NpgsqlDbType = NpgsqlDbType.Text },
                    new NpgsqlParameter { Value = (float)strictest.Score },
                    new NpgsqlParameter { Value = JsonSerializer.Serialize(finding.Verdicts, PipelineJson.Options), NpgsqlDbType = NpgsqlDbType.Jsonb },
                    new NpgsqlParameter { Value = JsonSerializer.Serialize(strictest.LegalRefs, PipelineJson.Options), NpgsqlDbType = NpgsqlDbType.Jsonb },
                    new NpgsqlParameter { Value = JsonSerializer.Serialize(finding.Params, PipelineJson.Options), NpgsqlDbType = NpgsqlDbType.Jsonb },
                    new NpgsqlParameter { Value = urls.Count },
                    new NpgsqlParameter { Value = scope.RunId },
                },
            };
            var id = (Guid)(await command.ExecuteScalarAsync(ct).ConfigureAwait(false))!;
            stored.Add(new StoredFinding(id, key));
            foreach (var page in urls.Select(u => pageIds.TryGetValue(u, out var p) ? p : (Guid?)null).OfType<Guid>().Distinct())
            {
                await using var occurrence = new NpgsqlCommand(
                    """
                    INSERT INTO checks.finding_occurrences (tenant_id, finding_id, page_id, shop_id, created_at)
                    VALUES ($1, $2, $3, $4, now()) ON CONFLICT (finding_id, page_id) DO NOTHING
                    """, connection, transaction)
                {
                    Parameters =
                    {
                        new NpgsqlParameter { Value = scope.TenantId },
                        new NpgsqlParameter { Value = id },
                        new NpgsqlParameter { Value = page },
                        new NpgsqlParameter { Value = scope.ShopId },
                    },
                };
                await occurrence.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
        }

        return stored;
    }

    /// <summary>The groups of the database: text, assess, verify (a verdict that cannot be checked by the text goes to verify).</summary>
    public static string Checkability(string checkability) => checkability is "text" or "assess" ? checkability : "verify";

    /// <summary>
    /// Rows of <c>checks.rule_sets</c> of the rule sets used (by module and version, created when missing; the built-in rule
    /// of a missing legal page has its own row).
    /// </summary>
    private static async Task<Dictionary<(string Module, string Version), Guid>> RuleSetRowsAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, IReadOnlyList<CoreRuleSet> ruleSets, CancellationToken ct)
    {
        var rows = ruleSets.Select(s => new RuleSetRow(s.Module, s.Version, s.Jurisdictions.ToArray(),
                JsonSerializer.Serialize(new { name = s.Name, file = Path.GetFileName(s.SourceFile), applies_to = s.AppliesTo }, PipelineJson.Options),
                JsonSerializer.Serialize(s.Questions, PipelineJson.Options), JsonSerializer.Serialize(s, PipelineJson.Options)))
            .Append(new RuleSetRow("legal", "builtin", [], "{\"name\":\"builtin\"}", "{}", "builtin"))
            .ToList();
        var ids = new Dictionary<(string, string), Guid>();
        foreach (var row in rows)
        {
            await using var command = new NpgsqlCommand(
                """
                WITH inserted AS (
                    INSERT INTO checks.rule_sets (id, module, version, jurisdictions, question_language, question_set_hash, definition, texts, source_hash, enabled, published_at, created_at, updated_at)
                    VALUES ($1, $2, $3, $4, 'en', $5, $6, '{}'::jsonb, $7, true, now(), now(), now())
                    ON CONFLICT (module, version) DO NOTHING
                    RETURNING id)
                SELECT id FROM inserted
                UNION ALL
                SELECT id FROM checks.rule_sets WHERE module = $2 AND version = $3
                LIMIT 1
                """, connection, transaction)
            {
                Parameters =
                {
                    new NpgsqlParameter { Value = Guid.CreateVersion7() },
                    new NpgsqlParameter { Value = row.Module },
                    new NpgsqlParameter { Value = row.Version },
                    new NpgsqlParameter { Value = row.Jurisdictions },
                    new NpgsqlParameter { Value = SHA256.HashData(Encoding.UTF8.GetBytes(row.Questions)) },
                    new NpgsqlParameter { Value = row.Definition, NpgsqlDbType = NpgsqlDbType.Jsonb },
                    new NpgsqlParameter { Value = SHA256.HashData(Encoding.UTF8.GetBytes(row.Source)) },
                },
            };
            ids[(row.Module, row.Version)] = (Guid)(await command.ExecuteScalarAsync(ct).ConfigureAwait(false))!;
        }

        return ids;
    }

    private sealed record RuleSetRow(string Module, string Version, string[] Jurisdictions, string Definition, string Questions, string Source);

    private static long Fingerprint(string text) => BitConverter.ToInt64(SHA256.HashData(Encoding.UTF8.GetBytes(text)), 0);
}
