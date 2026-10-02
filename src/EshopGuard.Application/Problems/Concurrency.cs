namespace EshopGuard.Application.Problems;

/// <summary>The version of a row the client read (<c>If-Match</c>); a change without it is <c>400 validation.failed</c>.</summary>
public static class Concurrency
{
    public const string IfMatch = "If-Match";

    public static uint Require(uint? version) =>
        version ?? throw DomainException.Validation(new ValidationResult().Add(IfMatch, ProblemCodes.Fields.Required));
}
