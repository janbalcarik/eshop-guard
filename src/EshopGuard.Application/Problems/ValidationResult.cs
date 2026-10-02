namespace EshopGuard.Application.Problems;

/// <summary>Codes of invalid fields (field name in camelCase → codes); empty when valid.</summary>
public sealed class ValidationResult
{
    private readonly Dictionary<string, List<string>> _errors = new(StringComparer.Ordinal);

    public bool IsValid => _errors.Count == 0;

    public IReadOnlyDictionary<string, IReadOnlyList<string>> Errors => _errors.ToDictionary(e => e.Key, e => (IReadOnlyList<string>)e.Value, StringComparer.Ordinal);

    public ValidationResult Add(string field, string code)
    {
        if (!_errors.TryGetValue(field, out var codes))
        {
            _errors[field] = codes = [];
        }

        if (!codes.Contains(code))
        {
            codes.Add(code);
        }

        return this;
    }

    /// <summary>Throws <see cref="DomainException"/> with <see cref="ProblemCodes.ValidationFailed"/> when a field is invalid.</summary>
    public void ThrowIfInvalid()
    {
        if (!IsValid)
        {
            throw DomainException.Validation(this);
        }
    }
}
