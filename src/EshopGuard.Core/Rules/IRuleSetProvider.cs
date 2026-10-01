namespace EshopGuard.Core.Rules;

/// <summary>
/// Source of rule sets and label lists. The default reads YAML files; a product may read them from a database.
/// </summary>
public interface IRuleSetProvider
{
    /// <summary>Loads and validates all rule sets and label lists.</summary>
    /// <exception cref="RuleValidationException">A file is missing, not valid YAML or not a valid rule set.</exception>
    Task<RuleCatalog> LoadAsync(CancellationToken ct = default);
}

/// <summary>
/// Rule sets could not be loaded. <see cref="Errors"/> lists every problem with its file and rule.
/// </summary>
public sealed class RuleValidationException(IReadOnlyList<string> errors)
    : Exception("Pravidla nejsou platná:" + Environment.NewLine + string.Join(Environment.NewLine, errors.Select(e => "- " + e)))
{
    /// <summary>Every problem found, in Czech.</summary>
    public IReadOnlyList<string> Errors { get; } = errors;
}
