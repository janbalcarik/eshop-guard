namespace EshopGuard.Core.Fix;

/// <summary>
/// Settings of the rewrite of problematic passages by an OpenAI model (<c>eshopguard rewrite</c>).
/// </summary>
public sealed class RewriteOptions
{
    /// <summary>OpenAI API key sent as a bearer token. Never logged.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Endpoint of the Responses API.</summary>
    public Uri BaseUrl { get; set; } = new("https://api.openai.com/v1/responses");

    /// <summary>
    /// Model. gpt-6.1-sol was chosen in the pilot of 30. 9. 2026 on 24 pages of vegis.sk: it specifies claims with
    /// facts from the page and invents none; gpt-6-luna costs a tenth but deletes more and specifies less.
    /// </summary>
    public string Model { get; set; } = "gpt-6.1-sol";

    /// <summary>Reasoning effort of the model: <c>low</c>, <c>medium</c> or <c>high</c>.</summary>
    public string ReasoningEffort { get; set; } = "medium";

    /// <summary>YAML file with the instructions, examples and legal texts sent to the model.</summary>
    public string PromptFile { get; set; } = "config/rewrite.yaml";

    /// <summary>Parallel requests; the first page is always sent alone so that the shared prompt is cached.</summary>
    public int Concurrency { get; set; } = 4;

    /// <summary>HTTP timeout of one request in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 300;

    /// <summary>Maximum number of attempts for retryable errors (429, 5xx, timeouts).</summary>
    public int MaxRetries { get; set; } = 5;

    /// <summary>Upper limit of output tokens (reasoning included) of one page.</summary>
    public int MaxOutputTokens { get; set; } = 16_000;

    /// <summary>USD per million input tokens (gpt-6.1-sol, developers.openai.com/api/docs/pricing, 30. 9. 2026).</summary>
    public decimal InputUsdPerMillion { get; set; } = 2.00m;

    /// <summary>USD per million input tokens read from the OpenAI prompt cache.</summary>
    public decimal CachedInputUsdPerMillion { get; set; } = 0.10m;

    /// <summary>USD per million output tokens, reasoning included.</summary>
    public decimal OutputUsdPerMillion { get; set; } = 10.00m;

    /// <summary>Output tokens per page assumed by the estimate (the pilot measured about 470).</summary>
    public int EstimatedOutputTokensPerPage { get; set; } = 800;

    /// <summary>Above this estimated price in USD the host must confirm the run.</summary>
    public decimal MaxUsdWithoutConfirm { get; set; } = 1.00m;

    /// <summary>Use the deterministic mock client instead of the API (tests, dry runs).</summary>
    public bool UseMock { get; set; }
}
