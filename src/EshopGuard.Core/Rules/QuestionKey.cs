namespace EshopGuard.Core.Rules;

/// <summary>
/// Key of an answer in <see cref="Models.Segment.Probabilities"/>: <c>{rule set}:{question id}</c>, e.g. <c>legal_sk:legal_adr</c>.
/// Rule sets of different jurisdictions may ask a question with the same id and a different wording; their answers stay
/// apart. Jev gets only the question id.
/// </summary>
public static class QuestionKey
{
    /// <summary>Separator of the rule set and the question id.</summary>
    public const char Separator = ':';

    /// <summary>Key of question <paramref name="questionId"/> of rule set <paramref name="ruleSet"/>.</summary>
    public static string Of(string ruleSet, string questionId) => ruleSet + Separator + questionId;

    /// <summary>Key of question <paramref name="questionId"/> of <paramref name="set"/>.</summary>
    public static string Of(RuleSet set, string questionId)
    {
        ArgumentNullException.ThrowIfNull(set);
        return Of(set.Name, questionId);
    }

    /// <summary>The question id of a key.</summary>
    public static string QuestionId(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        var index = key.IndexOf(Separator, StringComparison.Ordinal);
        return index < 0 ? key : key[(index + 1)..];
    }

    /// <summary>The rule set of a key, or an empty string.</summary>
    public static string RuleSet(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        var index = key.IndexOf(Separator, StringComparison.Ordinal);
        return index < 0 ? "" : key[..index];
    }
}
