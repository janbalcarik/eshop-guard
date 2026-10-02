using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EshopGuard.Application.Contracts;
using EshopGuard.Application.Localization;
using EshopGuard.Application.Problems;
using EshopGuard.Application.Shops;
using EshopGuard.Core.Rules.Texts;
using EshopGuard.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace EshopGuard.Application.Findings;

/// <summary>
/// The texts of the rules for the frontend (change 11, AD 2): title, explanation (general and by jurisdiction), recommendation
/// and the wording of the questions of every rule of the requested rule sets, in the language of the user. The texts are the
/// files <c>rules/texts/&lt;locale&gt;/</c> of the same rules the worker checks with (<c>RuleTexts</c> of change 6): a translation is
/// used only after a person reviewed it, otherwise the original texts of the set, and <c>locale</c> of each set says which
/// language came back. A rule set without any usable texts is <c>409 catalog.locale_incomplete</c>, never a partial
/// catalog. Legal references are not here: they stay in the verdicts, in the language of the law. Entries are held in
/// <see cref="IMemoryCache"/> by rule set and language.
/// </summary>
public sealed class RuleTextCatalog(EshopGuardDb db, ShopCatalog shops, IRefCatalog refCatalog, IMemoryCache cache)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(1);

    public async Task<(RuleTextCatalogDto Catalog, string ETag)> GetAsync(string? locale, IReadOnlyList<Guid>? ruleSetIds, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(locale))
        {
            throw DomainException.Validation(new ValidationResult().Add("locale", ProblemCodes.Fields.Required));
        }

        var locales = await refCatalog.GetLocalesAsync(ct).ConfigureAwait(false);
        if (!locales.Any(l => l.Enabled && l.Code == locale))
        {
            throw new DomainException(ProblemCodes.LocaleNotEnabled, 400, new Dictionary<string, object?> { ["locale"] = locale });
        }

        var rows = await RuleSetRowsAsync(ruleSetIds, ct).ConfigureAwait(false);
        var rules = await shops.RulesAsync(ct).ConfigureAwait(false);
        var sets = new List<RuleSetTextsDto>();
        foreach (var row in rows)
        {
            var entry = await cache.GetOrCreateAsync(("rule-texts", row.Id, locale), e =>
            {
                e.AbsoluteExpirationRelativeToNow = Lifetime;
                return Task.FromResult(Build(row, locale, rules.Texts));
            }).ConfigureAwait(false);
            sets.Add(entry ?? throw new DomainException(ProblemCodes.CatalogLocaleIncomplete, 409, new Dictionary<string, object?> { ["ruleSetId"] = row.Id }));
        }

        var catalog = new RuleTextCatalogDto(locale, sets);
        var etag = "\"" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(catalog))))[..32] + "\"";
        return (catalog, etag);
    }

    /// <summary>Titles of the rules of the rule sets in a language (the rule id where a set has no texts), for documents.</summary>
    public async Task<Dictionary<(Guid RuleSetId, string RuleId), string>> TitlesAsync(string locale, IReadOnlyList<Guid> ruleSetIds, CancellationToken ct)
    {
        var titles = new Dictionary<(Guid, string), string>();
        if (ruleSetIds.Count == 0)
        {
            return titles;
        }

        var rows = await RuleSetRowsAsync(ruleSetIds, ct).ConfigureAwait(false);
        var rules = await shops.RulesAsync(ct).ConfigureAwait(false);
        foreach (var row in rows)
        {
            if (Build(row, locale, rules.Texts) is { } set)
            {
                foreach (var (ruleId, text) in set.Rules)
                {
                    titles[(row.Id, ruleId)] = text.Title;
                }
            }
        }

        return titles;
    }

    /// <summary>The texts of one rule set in a language, or null when the set has no usable texts at all.</summary>
    internal static RuleSetTextsDto? Build(RuleSetRow row, string locale, RuleTexts texts)
    {
        Dictionary<string, RuleText> rules;
        string used;
        if (row.Name == "builtin")
        {
            // The built-in rules of the engine (legal_pages_missing) have their texts in _engine.yaml.
            var engine = Usable(texts, locale) ?? Usable(texts, texts.OriginalLocales.Values.FirstOrDefault() ?? "");
            if (engine is null)
            {
                return null;
            }

            rules = engine.Value.Engine.Rules;
            used = engine.Value.Locale;
        }
        else
        {
            if (texts.SetTexts(row.Name, locale) is not { } set)
            {
                return null;
            }

            rules = set.File.Rules;
            used = set.Locale;
        }

        return new RuleSetTextsDto(
            row.Id, row.Module, row.Version, used,
            rules.OrderBy(r => r.Key, StringComparer.Ordinal).ToDictionary(r => r.Key, r => new RuleTextDto(
                r.Value.Title,
                new RuleExplanationDto(r.Value.Explanation, r.Value.ExplanationByJurisdiction ?? []),
                r.Value.Recommendation,
                [],
                r.Value.UserQuestions ?? []), StringComparer.Ordinal));
    }

    private static (EngineTextFile Engine, string Locale)? Usable(RuleTexts texts, string locale) =>
        texts.Locales.TryGetValue(locale, out var folder) && folder.Engine is { } engine && engine.Review.IsUsable ? (engine, locale) : null;

    private async Task<List<RuleSetRow>> RuleSetRowsAsync(IReadOnlyList<Guid>? ids, CancellationToken ct)
    {
        var query = db.RuleSets.AsNoTracking();
        if (ids is { Count: > 0 })
        {
            query = query.Where(r => ids.Contains(r.Id));
        }
        else
        {
            query = query.Where(r => r.Enabled);
        }

        var rows = await query.Select(r => new { r.Id, r.Module, r.Version, r.Definition }).ToListAsync(ct).ConfigureAwait(false);
        return rows
            .Select(r => new RuleSetRow(r.Id, r.Module, r.Version, Name(r.Definition)))
            .Where(r => r.Name.Length > 0)
            .OrderBy(r => r.Module, StringComparer.Ordinal).ThenBy(r => r.Version, StringComparer.Ordinal)
            .ToList();
    }

    private static string Name(JsonDocument? definition) =>
        definition?.RootElement is { ValueKind: JsonValueKind.Object } root && root.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String
            ? name.GetString()!
            : "";
}

/// <summary>A row of <c>checks.rule_sets</c>: the name is the file of the rules (<c>definition.name</c>), e.g. <c>legal_sk</c>.</summary>
public sealed record RuleSetRow(Guid Id, string Module, string Version, string Name);
