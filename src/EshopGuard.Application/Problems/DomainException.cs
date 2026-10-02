namespace EshopGuard.Application.Problems;

/// <summary>
/// An expected error of a service as a code, an HTTP status and parameters (AD 12); the API turns it into
/// <c>application/problem+json</c>. The message is the code only, so a log never carries a sentence with user data.
/// </summary>
public sealed class DomainException(string code, int status, IReadOnlyDictionary<string, object?>? parameters = null) : Exception(code)
{
    public string Code { get; } = code;

    public int Status { get; } = status;

    public IReadOnlyDictionary<string, object?> Parameters { get; } = parameters ?? new Dictionary<string, object?>();

    /// <summary>Errors by field of <see cref="ProblemCodes.ValidationFailed"/>.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>>? Errors { get; init; }

    public static DomainException Validation(ValidationResult result) =>
        new(ProblemCodes.ValidationFailed, 400) { Errors = result.Errors };
}
