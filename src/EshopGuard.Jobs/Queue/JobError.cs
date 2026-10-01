namespace EshopGuard.Jobs.Queue;

/// <summary>
/// Error of a job for <c>last_error</c>: <c>"{Code}: {ExceptionType}: {Detail}"</c>, cut to <see cref="MaxLength"/> characters.
/// <paramref name="Detail"/> must not contain page texts or keys (handlers throw exceptions with codes).
/// </summary>
public sealed record JobError(string Code, string? ExceptionType = null, string? Detail = null)
{
    /// <summary>Longest stored error.</summary>
    public const int MaxLength = 500;

    /// <summary>The text stored in <c>last_error</c>.</summary>
    public string Format()
    {
        var text = string.Join(": ", new[] { Code, ExceptionType, Detail }.Where(p => !string.IsNullOrEmpty(p)));
        return text.Length <= MaxLength ? text : text[..MaxLength];
    }

    /// <inheritdoc />
    public override string ToString() => Format();
}
