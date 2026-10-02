namespace EshopGuard.Core.Fix;

/// <summary>
/// A language model that rewrites the problematic passages of one page. The default is OpenAI; a host may register
/// another implementation before <c>AddEshopGuard</c>.
/// </summary>
public interface IRewriteClient
{
    /// <summary>Sends the shared part and the page part and returns the JSON answer with token usage.</summary>
    Task<RewriteResponse> RewriteAsync(RewriteRequest request, CancellationToken ct);
}

/// <summary>
/// One request: the shared part (instructions, examples, legal texts; the same for all pages) first, the page last.
/// </summary>
public sealed class RewriteRequest
{
    /// <summary>Instructions, examples and legal texts; byte for byte the same for every page.</summary>
    public required string SharedPart { get; init; }

    /// <summary>The page with numbered blocks and its findings.</summary>
    public required string PagePart { get; init; }

    /// <summary>Version of the prompt, used as the prompt cache key.</summary>
    public required string PromptVersion { get; init; }

    /// <summary>JSON schema of the answer.</summary>
    public required object Schema { get; init; }

    /// <summary>Blocks of the page as sent (for clients that do not call a model, such as the mock).</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Blocks { get; init; } = [];

    /// <summary>Findings of the page as sent.</summary>
    public IReadOnlyList<RewriteFinding> Findings { get; init; } = [];

    /// <summary>Reasoning effort of this request (<c>low</c>, <c>medium</c>, <c>high</c>); null uses <c>rewrite.reasoning_effort</c>.</summary>
    public string? ReasoningEffort { get; init; }
}

/// <summary>
/// The answer of the model: JSON by the schema and token usage.
/// </summary>
public sealed class RewriteResponse
{
    /// <summary>JSON answer.</summary>
    public required string Json { get; init; }

    /// <summary>Model that answered.</summary>
    public string? Model { get; init; }

    /// <summary>Input tokens.</summary>
    public long InputTokens { get; init; }

    /// <summary>Of <see cref="InputTokens"/>, tokens read from the prompt cache.</summary>
    public long CachedTokens { get; init; }

    /// <summary>Output tokens, reasoning included.</summary>
    public long OutputTokens { get; init; }

    /// <summary>Of <see cref="OutputTokens"/>, reasoning tokens.</summary>
    public long ReasoningTokens { get; init; }
}

/// <summary>
/// Error of the rewrite API. Fatal errors (key, quota) stop the whole run; others fail one page.
/// </summary>
public sealed class RewriteApiException : Exception
{
    /// <summary>Creates the exception.</summary>
    public RewriteApiException(string message, int statusCode, bool isFatal, Exception? inner = null)
        : base(message, inner)
    {
        StatusCode = statusCode;
        IsFatal = isFatal;
    }

    /// <summary>HTTP status, 0 when there was no response.</summary>
    public int StatusCode { get; }

    /// <summary>True when retrying other pages makes no sense (missing key, no quota).</summary>
    public bool IsFatal { get; }
}
