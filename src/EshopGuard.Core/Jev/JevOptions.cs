namespace EshopGuard.Core.Jev;

/// <summary>
/// Connection and throughput settings for the Jev API.
/// </summary>
public sealed class JevOptions
{
    /// <summary>API key sent as a bearer token. Never logged.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Full endpoint URL. Via OpenRouter use <c>https://openrouter.ai/api/v1/systemone</c>.</summary>
    public Uri BaseUrl { get; set; } = new("https://api.typesafe.ai/v1/systemone");

    /// <summary>Pinned model version, so that tuned thresholds do not drift.</summary>
    public string Model { get; set; } = "jev-1.13.0";

    /// <summary>Global request limit per minute (the API also limits tokens per second).</summary>
    public int RequestsPerMinute { get; set; } = 1200;

    /// <summary>Default number of parallel requests.</summary>
    public int Concurrency { get; set; } = 8;

    /// <summary>HTTP timeout of one request in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>Maximum number of attempts for retryable errors (429, 529, 5xx, timeouts).</summary>
    public int MaxRetries { get; set; } = 6;

    /// <summary>First backoff delay in milliseconds; each further attempt doubles it (at most 30 s) with random jitter.</summary>
    public int RetryBaseDelayMilliseconds { get; set; } = 500;

    /// <summary>Use the deterministic mock client instead of the API.</summary>
    public bool UseMock { get; set; }
}
