namespace EshopGuard.Core.Jev;

/// <summary>
/// Client of the Jev API: one request evaluates one <c>state</c> against several typed questions.
/// This namespace does not depend on the rest of the library.
/// </summary>
public interface IJevClient
{
    /// <summary>
    /// Evaluates the state (a string, an object or an array) against the questions.
    /// </summary>
    /// <param name="state">Text or JSON-serializable object sent as <c>state</c>.</param>
    /// <param name="questions">Questions keyed by an id chosen by the caller; keys are sent unchanged.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<JevResult> EvaluateAsync(object state, IReadOnlyDictionary<string, JevQuestion> questions, CancellationToken ct);
}

/// <summary>
/// A typed question: <c>noul</c> (yes/no), <c>choice</c> or <c>score</c>.
/// </summary>
public sealed class JevQuestion
{
    /// <summary>Question type: <c>noul</c>, <c>choice</c> or <c>score</c>.</summary>
    public required string Type { get; init; }

    /// <summary>Question text (string, object or array).</summary>
    public required object Instructions { get; init; }

    /// <summary>
    /// Required for <c>choice</c> (map option → description, at most 255) and <c>score</c> (2–10 ordered levels);
    /// optional for <c>noul</c> (<c>{"true": …, "false": …}</c>).
    /// </summary>
    public object? Criteria { get; init; }
}

/// <summary>
/// Response of one request.
/// </summary>
public sealed class JevResult
{
    /// <summary>Model that answered (via OpenRouter a snapshot id).</summary>
    public string? Model { get; init; }

    /// <summary>Answers keyed by question id.</summary>
    public IReadOnlyDictionary<string, JevAnswer> Answers { get; init; } = new Dictionary<string, JevAnswer>();

    /// <summary>Token usage; only input tokens are billed.</summary>
    public JevUsage Usage { get; init; } = new();
}

/// <summary>
/// Answer to one question. Which fields are set depends on <see cref="Type"/>.
/// </summary>
public sealed class JevAnswer
{
    /// <summary>Answer type, the same as the question type.</summary>
    public string Type { get; init; } = "";

    /// <summary><c>noul</c>: probability of "yes" from 0 to 1.</summary>
    public double? Noul { get; init; }

    /// <summary><c>choice</c>: the most probable option.</summary>
    public string? Choice { get; init; }

    /// <summary><c>score</c>: probability-weighted level index, may lie between levels.</summary>
    public double? Score { get; init; }

    /// <summary><c>choice</c> and <c>score</c>: probabilities by option or level index ("0", "1", …).</summary>
    public IReadOnlyDictionary<string, double>? Probabilities { get; init; }

    /// <summary><c>score</c>: level descriptions by index ("0", "1", …).</summary>
    public IReadOnlyDictionary<string, string>? Legend { get; init; }

    /// <summary><c>choice</c> and <c>score</c>: confidence of the answer.</summary>
    public double? Confidence { get; init; }
}

/// <summary>
/// Token usage of one request.
/// </summary>
public sealed class JevUsage
{
    /// <summary>Billed input tokens.</summary>
    public long InputTokens { get; init; }

    /// <summary>Output tokens (free).</summary>
    public long OutputTokens { get; init; }
}
