using EshopGuard.Core.Storage;

namespace EshopGuard.Core.Rules;

/// <summary>
/// What is stored about one version of a rule set (change 8, <c>checks.rule_sets</c>): its definition without texts, its
/// texts by language and two fingerprints. <see cref="QuestionSetHash"/> changes only with the questions (and is the same as in
/// the cache keys of Jev), <see cref="SourceHash"/> with anything, texts included; fixing a typo in a text never changes the
/// version or the cache.
/// </summary>
public sealed class RuleSetDescriptor
{
    /// <summary>Module.</summary>
    public required string Module { get; init; }

    /// <summary>Version of the question set.</summary>
    public required string Version { get; init; }

    /// <summary>Name of the set (file name without extension).</summary>
    public required string Name { get; init; }

    /// <summary>File of the rules.</summary>
    public required string File { get; init; }

    /// <summary>Jurisdictions of the set.</summary>
    public IReadOnlyList<string> Jurisdictions { get; init; } = [];

    /// <summary>Language of the questions sent to Jev the hash is for (<c>en</c> or <c>cs</c>).</summary>
    public required string QuestionLanguage { get; init; }

    /// <summary>Fingerprint of the questions as in the cache keys of Jev (model, version, language, questions).</summary>
    public required string QuestionSetHash { get; init; }

    /// <summary>Fingerprint of the questions alone (<c>rules/question-set-hashes.json</c>).</summary>
    public required string QuestionsHash { get; init; }

    /// <summary>Canonical JSON of the set without texts: questions, rules, logic, references, effective dates.</summary>
    public required string Definition { get; init; }

    /// <summary>Canonical JSON of the texts of the set, by language.</summary>
    public IReadOnlyDictionary<string, string> Texts { get; init; } = new Dictionary<string, string>();

    /// <summary>Fingerprint of the definition and all texts.</summary>
    public required string SourceHash { get; init; }

    /// <summary>A disabled set is stored but never run.</summary>
    public bool Enabled { get; init; }

    /// <summary>Languages with usable texts of the set (original or reviewed translation).</summary>
    public IReadOnlyList<string> CompleteLocales { get; init; } = [];

    /// <summary>Descriptors of every set of the catalog for the configured model and question language.</summary>
    public static List<RuleSetDescriptor> Describe(RuleCatalog catalog, string model, string questionLanguage)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return catalog.RuleSets.Select(set =>
        {
            var definition = JevCacheKeys.Canonical(new
            {
                set.Version, set.Module, set.AppliesTo, set.Jurisdictions, set.Enabled, set.PresenceThreshold, set.Questions, set.Rules,
            });
            var texts = catalog.Texts.Locales
                .Where(l => l.Value.Sets.ContainsKey(set.Name))
                .OrderBy(l => l.Key, StringComparer.Ordinal)
                .ToDictionary(l => l.Key, l => JevCacheKeys.Canonical(l.Value.Sets[set.Name]), StringComparer.Ordinal);
            var questions = SegmentEvaluator.BuildQuestions(set, questionLanguage);
            return new RuleSetDescriptor
            {
                Module = set.Module,
                Version = set.Version,
                Name = set.Name,
                File = set.SourceFile,
                Jurisdictions = set.Jurisdictions,
                QuestionLanguage = questionLanguage,
                QuestionSetHash = JevCacheKeys.Create(JevCacheKind.Detail, model, set.Version, questionLanguage, questions, "").QuestionSetHash,
                QuestionsHash = QuestionSetHashes.Of(set),
                Definition = definition,
                Texts = texts,
                SourceHash = TextTools.Sha256(definition + "\u001F" + string.Join("\u001F", texts.Select(t => t.Key + "\u001E" + t.Value))),
                Enabled = set.Enabled,
                CompleteLocales = catalog.Texts.UsableSets.Where(u => u.Value.Contains(set.Name)).Select(u => u.Key).Order(StringComparer.Ordinal).ToList(),
            };
        }).ToList();
    }
}
