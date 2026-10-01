using System.Text.RegularExpressions;

namespace EshopGuard.Core.Rules.Texts;

/// <summary>
/// Checks the texts against the rule sets: errors that stop the load (a text left in an enabled rule set, missing original
/// texts, unknown rules, codes or jurisdictions, an incomplete language that must be complete) and problems that only keep a
/// translation from being used (missing keys, different placeholders, not reviewed, written for another version of the set).
/// </summary>
internal static partial class RuleTextValidator
{
    /// <summary>Ids of the built-in rules whose texts are in <c>_engine.yaml</c>.</summary>
    public static readonly IReadOnlyList<string> BuiltInRules = [RuleEngine.MissingLegalPagesRuleId];

    /// <summary>
    /// Validates <paramref name="texts"/> and returns them with the languages that can be used, the problems and the language of
    /// the original texts of every set.
    /// </summary>
    public static RuleTexts Validate(
        RuleTexts texts, IReadOnlyList<RuleSet> ruleSets, LabelConfiguration labels, JurisdictionRegistry jurisdictions,
        IReadOnlyList<string> requiredLocales, List<string> errors)
    {
        var problems = texts.Locales.Keys.ToDictionary(l => l, _ => new List<string>(), StringComparer.Ordinal);
        var sets = ruleSets.ToDictionary(s => s.Name, StringComparer.Ordinal);

        // The original texts of every set: exactly one file marked original.
        var originals = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (locale, localeTexts) in texts.Locales)
        {
            foreach (var (name, file) in localeTexts.Sets)
            {
                if (!sets.TryGetValue(name, out var set))
                {
                    errors.Add($"{file.File}: sada pravidel „{name}“ neexistuje (v rules/ není {name}.yaml).");
                    continue;
                }

                CheckKnownKeys(file, set, jurisdictions, errors);
                if (file.Review.Original)
                {
                    if (originals.TryGetValue(name, out var other))
                    {
                        errors.Add($"{file.File}: původní texty sady {name} jsou už v jazyce {other}; original: true smí mít jen jeden soubor.");
                    }
                    else
                    {
                        originals[name] = locale;
                    }
                }
            }
        }

        foreach (var set in ruleSets)
        {
            if (set.Enabled)
            {
                foreach (var rule in set.Rules.Where(r => r.HasInlineTexts))
                {
                    errors.Add($"{set.SourceFile}: pravidlo „{rule.Id}“ má title, explanation, explanation_by_jurisdiction nebo recommendation v souboru pravidel; texty zapnuté sady patří do rules/texts/<jazyk>/{set.Name}.yaml.");
                }
            }

            if (!originals.TryGetValue(set.Name, out var originalLocale))
            {
                if (set.Enabled)
                {
                    errors.Add($"{set.SourceFile}: chybí soubor textů rules/texts/<jazyk>/{set.Name}.yaml s původními texty (review.original: true).");
                }
                else
                {
                    CheckInlineTexts(set, errors);
                }

                continue;
            }

            var original = texts.Locales[originalLocale].Sets[set.Name];
            if (original.SourceVersion != set.Version)
            {
                errors.Add($"{original.File}: source_version „{original.SourceVersion}“ neodpovídá verzi sady {set.Version}; zkontrolujte texty a source_version upravte.");
            }

            foreach (var missing in Missing(original, set))
            {
                errors.Add($"{original.File}: {missing}");
            }

            set.ExplanationVariants = original.Rules.ToDictionary(
                r => r.Key,
                r => (IReadOnlySet<string>)(r.Value.ExplanationByJurisdiction?.Where(e => !string.IsNullOrWhiteSpace(e.Value)).Select(e => e.Key).ToHashSet(StringComparer.Ordinal)
                    ?? []),
                StringComparer.Ordinal);
        }

        // Translations of the sets.
        var usable = texts.Locales.Keys.ToDictionary(l => l, _ => new HashSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);
        foreach (var set in ruleSets)
        {
            if (!originals.TryGetValue(set.Name, out var originalLocale))
            {
                continue;
            }

            var original = texts.Locales[originalLocale].Sets[set.Name];
            usable[originalLocale].Add(set.Name);
            foreach (var (locale, localeTexts) in texts.Locales.Where(l => l.Key != originalLocale))
            {
                if (!localeTexts.Sets.TryGetValue(set.Name, out var file))
                {
                    if (set.Enabled)
                    {
                        problems[locale].Add($"{locale}/{set.Name}.yaml: chybí (texty se zobrazí v jazyce {originalLocale}).");
                    }

                    continue;
                }

                var found = TranslationProblems(file, original, set).ToList();
                if (found.Count == 0)
                {
                    usable[locale].Add(set.Name);
                }

                problems[locale].AddRange(found.Select(p => $"{file.File}: {p}"));
            }
        }

        // Texts of the tool and of the labels.
        var engineOriginal = texts.Locales.Values.Select(l => l.Engine).FirstOrDefault(e => e is { Review.Original: true });
        var labelsOriginal = texts.Locales.Values.Select(l => l.Labels).FirstOrDefault(e => e is { Review.Original: true });
        var toolLocales = new List<string>();
        foreach (var (locale, localeTexts) in texts.Locales)
        {
            var found = new List<string>();
            if (localeTexts.Engine is not { } engine)
            {
                found.Add($"{locale}/{YamlRuleTextProvider.EngineFile}: chybí.");
            }
            else
            {
                foreach (var unknown in engine.Codes.Keys.Where(c => !EngineCodes.All.Contains(c)))
                {
                    errors.Add($"{engine.File}: neznámý kód „{unknown}“.");
                }

                foreach (var unknown in engine.Rules.Keys.Where(r => !BuiltInRules.Contains(r)))
                {
                    errors.Add($"{engine.File}: neznámé vestavěné pravidlo „{unknown}“.");
                }

                foreach (var unknown in engine.Jurisdictions.Keys.Where(j => !jurisdictions.Contains(j)))
                {
                    errors.Add($"{engine.File}: jurisdikce „{unknown}“ není v config/jurisdictions.yaml.");
                }

                found.AddRange(EngineProblems(engine, engineOriginal, jurisdictions).Select(p => $"{engine.File}: {p}"));
            }

            if (localeTexts.Labels is not { } labelTexts)
            {
                found.Add($"{locale}/{YamlRuleTextProvider.LabelsFile}: chybí.");
            }
            else
            {
                var ids = labels.Notes.Select(n => n.Id).ToHashSet(StringComparer.Ordinal);
                foreach (var unknown in labelTexts.Notes.Keys.Where(k => !ids.Contains(k)))
                {
                    errors.Add($"{labelTexts.File}: poznámka „{unknown}“ není v souboru se značkami (label_notes).");
                }

                found.AddRange(LabelProblems(labelTexts, labelsOriginal, ids).Select(p => $"{labelTexts.File}: {p}"));
            }

            if (found.Count == 0)
            {
                toolLocales.Add(locale);
            }

            problems[locale].AddRange(found);
        }

        foreach (var required in requiredLocales.Where(r => !toolLocales.Contains(r)))
        {
            var detail = problems.TryGetValue(required, out var list) && list.Count > 0
                ? string.Join(" ", list.Where(p => p.Contains("_engine", StringComparison.Ordinal) || p.Contains("_labels", StringComparison.Ordinal)))
                : $"složka rules/texts/{required} chybí.";
            errors.Add($"Texty nástroje pro povinný jazyk {required} (rules.required_locales) nejsou úplné: {detail}");
        }

        var enabled = ruleSets.Where(s => s.Enabled).Select(s => s.Name).ToList();
        var complete = toolLocales.Where(l => enabled.All(usable[l].Contains)).Order(StringComparer.Ordinal).ToList();
        return new RuleTexts
        {
            Locales = texts.Locales,
            OriginalLocales = originals,
            CompleteLocales = complete,
            ToolLocales = toolLocales.Order(StringComparer.Ordinal).ToList(),
            Problems = problems.ToDictionary(p => p.Key, p => (IReadOnlyList<string>)p.Value, StringComparer.Ordinal),
            UsableSets = usable.ToDictionary(u => u.Key, u => (IReadOnlySet<string>)u.Value, StringComparer.Ordinal),
        };
    }

    /// <summary>Names of the placeholders <c>{name}</c> and <c>{name:format}</c> of a text, in order without repeats.</summary>
    public static IReadOnlyList<string> Placeholders(string text) =>
        Placeholder().Matches(text).Select(m => m.Groups[1].Value).Distinct().Order(StringComparer.Ordinal).ToList();

    [GeneratedRegex(@"\{([a-z_][a-z0-9_]*)(?::[^{}]*)?\}")]
    private static partial Regex Placeholder();

    private static void CheckKnownKeys(RuleSetTextFile file, RuleSet set, JurisdictionRegistry jurisdictions, List<string> errors)
    {
        var rules = set.Rules.ToDictionary(r => r.Id, StringComparer.Ordinal);
        foreach (var (id, text) in file.Rules)
        {
            if (!rules.TryGetValue(id, out var rule))
            {
                errors.Add($"{file.File}: pravidlo „{id}“ není v sadě {set.Name}.");
                continue;
            }

            foreach (var jurisdiction in (text.ExplanationByJurisdiction ?? []).Keys.Where(j => !jurisdictions.Contains(j)))
            {
                errors.Add($"{file.File}: pravidlo „{id}“, explanation_by_jurisdiction: jurisdikce „{jurisdiction}“ není v config/jurisdictions.yaml.");
            }

            foreach (var question in (text.UserQuestions ?? []).Keys.Where(q => !rule.UserQuestions.Contains(q)))
            {
                errors.Add($"{file.File}: pravidlo „{id}“: otázka pro uživatele „{question}“ není v user_questions pravidla.");
            }
        }
    }

    /// <summary>A disabled set without a texts file keeps title, explanation and recommendation inline.</summary>
    private static void CheckInlineTexts(RuleSet set, List<string> errors)
    {
        foreach (var rule in set.Rules.Where(r => string.IsNullOrWhiteSpace(r.Title) || string.IsNullOrWhiteSpace(r.Explanation) || string.IsNullOrWhiteSpace(r.Recommendation)))
        {
            errors.Add($"{set.SourceFile}: pravidlo „{rule.Id}“ vypnuté sady nemá title, explanation a recommendation ani soubor textů.");
        }
    }

    /// <summary>What is missing in a texts file of the set: every rule needs title, explanation, recommendation and its questions for the user.</summary>
    private static IEnumerable<string> Missing(RuleSetTextFile file, RuleSet set)
    {
        foreach (var rule in set.Rules)
        {
            if (!file.Rules.TryGetValue(rule.Id, out var text))
            {
                yield return $"chybí texty pravidla „{rule.Id}“.";
                continue;
            }

            foreach (var (key, value) in new[] { ("title", text.Title), ("explanation", text.Explanation), ("recommendation", text.Recommendation) })
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    yield return $"pravidlo „{rule.Id}“: chybí {key}.";
                }
            }

            foreach (var question in rule.UserQuestions.Where(q => text.UserQuestions?.GetValueOrDefault(q) is not { Length: > 0 }))
            {
                yield return $"pravidlo „{rule.Id}“: chybí text otázky pro uživatele „{question}“.";
            }
        }
    }

    private static IEnumerable<string> TranslationProblems(RuleSetTextFile file, RuleSetTextFile original, RuleSet set)
    {
        if (!file.Review.IsUsable)
        {
            yield return file.Review.MachineDraft ? "návrh překladu čeká na kontrolu člověkem (machine_draft)." : "čeká na kontrolu (chybí review.reviewed_by nebo reviewed_at).";
        }

        if (file.SourceVersion != set.Version)
        {
            yield return $"překlad je k verzi {file.SourceVersion}, sada má verzi {set.Version}.";
        }

        foreach (var missing in Missing(file, set))
        {
            yield return missing;
        }

        foreach (var (id, text) in file.Rules)
        {
            if (!original.Rules.TryGetValue(id, out var source))
            {
                continue;
            }

            var sourceKeys = (source.ExplanationByJurisdiction ?? []).Keys.Order(StringComparer.Ordinal);
            var keys = (text.ExplanationByJurisdiction ?? []).Where(e => !string.IsNullOrWhiteSpace(e.Value)).Select(e => e.Key).Order(StringComparer.Ordinal);
            if (!sourceKeys.SequenceEqual(keys))
            {
                yield return $"pravidlo „{id}“: explanation_by_jurisdiction má mít jurisdikce [{string.Join(", ", sourceKeys)}], má [{string.Join(", ", keys)}].";
            }

            foreach (var (key, translated, sourceText) in new[]
            {
                ("title", text.Title, source.Title), ("explanation", text.Explanation, source.Explanation), ("recommendation", text.Recommendation, source.Recommendation),
            })
            {
                if (PlaceholderDifference(translated, sourceText) is { } difference)
                {
                    yield return $"pravidlo „{id}“, {key}: {difference}";
                }
            }
        }
    }

    private static IEnumerable<string> EngineProblems(EngineTextFile engine, EngineTextFile? original, JurisdictionRegistry jurisdictions)
    {
        if (!engine.Review.IsUsable)
        {
            yield return engine.Review.MachineDraft ? "návrh překladu čeká na kontrolu člověkem (machine_draft)." : "čeká na kontrolu (chybí review.reviewed_by nebo reviewed_at).";
        }

        foreach (var code in EngineCodes.All.Where(c => engine.Codes.GetValueOrDefault(c) is not { Length: > 0 }))
        {
            yield return $"chybí text kódu „{code}“.";
        }

        foreach (var rule in BuiltInRules)
        {
            if (!engine.Rules.TryGetValue(rule, out var text) || new[] { text.Title, text.Explanation, text.Recommendation }.Any(string.IsNullOrWhiteSpace))
            {
                yield return $"chybí title, explanation nebo recommendation vestavěného pravidla „{rule}“.";
            }
        }

        foreach (var code in jurisdictions.Codes.Where(j => engine.Jurisdictions.GetValueOrDefault(j) is not { Length: > 0 }))
        {
            yield return $"chybí název jurisdikce „{code}“.";
        }

        if (original is null || ReferenceEquals(original, engine))
        {
            yield break;
        }

        foreach (var (code, text) in engine.Codes)
        {
            if (original.Codes.TryGetValue(code, out var source) && PlaceholderDifference(text, source) is { } difference)
            {
                yield return $"kód „{code}“: {difference}";
            }
        }
    }

    private static IEnumerable<string> LabelProblems(LabelTextFile labels, LabelTextFile? original, IReadOnlySet<string> ids)
    {
        if (!labels.Review.IsUsable)
        {
            yield return labels.Review.MachineDraft ? "návrh překladu čeká na kontrolu člověkem (machine_draft)." : "čeká na kontrolu (chybí review.reviewed_by nebo reviewed_at).";
        }

        foreach (var id in ids.Order(StringComparer.Ordinal).Where(id => labels.Notes.GetValueOrDefault(id) is not { Length: > 0 }))
        {
            yield return $"chybí text poznámky „{id}“.";
        }

        if (original is not null && !ReferenceEquals(original, labels))
        {
            foreach (var (id, text) in labels.Notes)
            {
                if (original.Notes.TryGetValue(id, out var source) && PlaceholderDifference(text, source) is { } difference)
                {
                    yield return $"poznámka „{id}“: {difference}";
                }
            }
        }
    }

    private static string? PlaceholderDifference(string text, string source)
    {
        var expected = Placeholders(source);
        var actual = Placeholders(text);
        if (expected.SequenceEqual(actual))
        {
            return null;
        }

        var missing = expected.Except(actual).Select(p => $"{{{p}}}").ToList();
        var extra = actual.Except(expected).Select(p => $"{{{p}}}").ToList();
        return "zástupné symboly se liší od původního textu"
            + (missing.Count > 0 ? $"; chybí {string.Join(", ", missing)}" : "")
            + (extra.Count > 0 ? $"; navíc {string.Join(", ", extra)}" : "") + ".";
    }
}
