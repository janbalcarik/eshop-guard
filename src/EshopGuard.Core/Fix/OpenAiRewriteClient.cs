using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using EshopGuard.Core.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EshopGuard.Core.Fix;

/// <summary>
/// Client of the OpenAI Responses API (<c>POST /v1/responses</c>) with strict structured output. The shared part goes
/// first as the system message and the page last, with a prompt cache key per prompt version, so the shared part is
/// billed at the cached price after the first page. <c>store: false</c>: OpenAI does not keep the texts. Backoff for
/// 408, 429 and 5xx (honouring <c>Retry-After</c>), fatal errors for 401, 403 and exhausted quota. The key is sent only
/// in the header and never logged.
/// </summary>
internal sealed class OpenAiRewriteClient : IRewriteClient
{
    public const string HttpClientName = "EshopGuard.OpenAI";

    private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MaxRetryAfter = TimeSpan.FromSeconds(60);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly RewriteOptions _options;
    private readonly ILogger<OpenAiRewriteClient> _logger;
    private readonly IRateLimiter? _limiter;

    /// <summary>A client; the <see cref="IRateLimiter"/> gives permits for its requests (the CLI has no limit here, concurrency is set by the rewrite settings).</summary>
    public OpenAiRewriteClient(IHttpClientFactory httpClientFactory, IOptions<RewriteOptions> options, ILogger<OpenAiRewriteClient> logger, IRateLimiter? limiter = null)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
        _limiter = limiter;
    }

    public async Task<RewriteResponse> RewriteAsync(RewriteRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new RewriteApiException("Chybí klíč API OpenAI. Nastavte OPENAI_API_KEY v .env nebo v proměnné prostředí.", 0, isFatal: true);
        }

        var body = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["model"] = _options.Model,
            ["reasoning"] = new Dictionary<string, object> { ["effort"] = _options.ReasoningEffort },
            ["max_output_tokens"] = _options.MaxOutputTokens,
            ["store"] = false,
            ["prompt_cache_key"] = "eshopguard-rewrite-" + request.PromptVersion,
            ["text"] = new Dictionary<string, object>
            {
                ["format"] = new Dictionary<string, object>
                {
                    ["type"] = "json_schema",
                    ["name"] = "rewrite",
                    ["strict"] = true,
                    ["schema"] = request.Schema,
                },
            },
            ["input"] = new[]
            {
                new Dictionary<string, object> { ["role"] = "system", ["content"] = request.SharedPart },
                new Dictionary<string, object> { ["role"] = "user", ["content"] = request.PagePart },
            },
        });

        var client = _httpClientFactory.CreateClient(HttpClientName);
        var maxAttempts = Math.Max(1, _options.MaxRetries);
        for (var attempt = 1; ; attempt++)
        {
            TimeSpan wait;
            string failure;
            using var lease = _limiter is null ? null : await _limiter.AcquireAsync(RateResource.OpenAi, 1, RequestPriority.P2, ct);
            try
            {
                using var message = new HttpRequestMessage(HttpMethod.Post, _options.BaseUrl)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json"),
                };
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
                using var response = await client.SendAsync(message, ct);
                var status = (int)response.StatusCode;
                var text = await response.Content.ReadAsStringAsync(ct);
                if (response.IsSuccessStatusCode)
                {
                    return Parse(text);
                }

                if (status is 401 or 403 || (status == 429 && text.Contains("insufficient_quota", StringComparison.Ordinal)))
                {
                    throw new RewriteApiException(FatalMessage(status), status, isFatal: true);
                }

                if (!IsRetryable(status) || attempt >= maxAttempts)
                {
                    _logger.LogWarning("OpenAI rejected the request (HTTP {Status}): {Response}", status, Shorten(text));
                    throw new RewriteApiException($"OpenAI vrátil HTTP {status}.", status, isFatal: false);
                }

                wait = RetryAfter(response) ?? Backoff(attempt);
                failure = $"HTTP {status}";
            }
            catch (HttpRequestException ex)
            {
                if (attempt >= maxAttempts)
                {
                    throw new RewriteApiException($"OpenAI je nedostupné: {ex.Message}", 0, isFatal: false, ex);
                }

                wait = Backoff(attempt);
                failure = ex.Message;
            }
            catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
            {
                if (attempt >= maxAttempts)
                {
                    throw new RewriteApiException("OpenAI neodpovědělo včas.", 0, isFatal: false, ex);
                }

                wait = Backoff(attempt);
                failure = "timeout";
            }

            _logger.LogInformation("OpenAI request failed ({Failure}), attempt {Attempt} of {Max}, retrying in {Delay} ms",
                failure, attempt, maxAttempts, (int)wait.TotalMilliseconds);
            await Task.Delay(wait, ct);
        }
    }

    private RewriteResponse Parse(string text)
    {
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(text);
        }
        catch (JsonException ex)
        {
            throw new RewriteApiException("Odpověď OpenAI není platný JSON.", 200, isFatal: false, ex);
        }

        var status = root?["status"]?.GetValue<string>();
        if (status != "completed")
        {
            var reason = root?["incomplete_details"]?["reason"]?.GetValue<string>();
            throw new RewriteApiException($"Odpověď OpenAI není dokončená (status {status}{(reason is null ? "" : ", " + reason)}).", 200, isFatal: false);
        }

        var parts = (root?["output"] as JsonArray ?? [])
            .Where(item => item?["type"]?.GetValue<string>() == "message")
            .SelectMany(item => item?["content"] as JsonArray ?? [])
            .ToList();
        if (parts.Any(p => p?["type"]?.GetValue<string>() == "refusal"))
        {
            throw new RewriteApiException("Model přepis odmítl.", 200, isFatal: false);
        }

        var json = string.Concat(parts
            .Where(p => p?["type"]?.GetValue<string>() == "output_text")
            .Select(p => p?["text"]?.GetValue<string>() ?? ""));
        if (json.Length == 0)
        {
            throw new RewriteApiException("Odpověď OpenAI neobsahuje text.", 200, isFatal: false);
        }

        var usage = root?["usage"];
        var result = new RewriteResponse
        {
            Json = json,
            Model = root?["model"]?.GetValue<string>(),
            InputTokens = usage?["input_tokens"]?.GetValue<long>() ?? 0,
            CachedTokens = usage?["input_tokens_details"]?["cached_tokens"]?.GetValue<long>() ?? 0,
            OutputTokens = usage?["output_tokens"]?.GetValue<long>() ?? 0,
            ReasoningTokens = usage?["output_tokens_details"]?["reasoning_tokens"]?.GetValue<long>() ?? 0,
        };
        _logger.LogDebug("OpenAI answered: model {Model}, {Input} input tokens ({Cached} cached), {Output} output tokens ({Reasoning} reasoning)",
            result.Model, result.InputTokens, result.CachedTokens, result.OutputTokens, result.ReasoningTokens);
        return result;
    }

    private TimeSpan Backoff(int attempt)
    {
        var exponential = TimeSpan.FromSeconds(2 * Math.Pow(2, attempt - 1));
        var capped = exponential > MaxBackoff ? MaxBackoff : exponential;
        return capped * (0.5 + Random.Shared.NextDouble() * 0.5);
    }

    private static TimeSpan? RetryAfter(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("retry-after-ms", out var values)
            && double.TryParse(values.FirstOrDefault(), NumberStyles.Float, CultureInfo.InvariantCulture, out var milliseconds))
        {
            return Clamp(TimeSpan.FromMilliseconds(milliseconds));
        }

        return response.Headers.RetryAfter?.Delta is { } delta ? Clamp(delta) : null;
    }

    private static TimeSpan Clamp(TimeSpan value) =>
        value < TimeSpan.Zero ? TimeSpan.Zero : value > MaxRetryAfter ? MaxRetryAfter : value;

    private static bool IsRetryable(int status) => status is 408 or 429 or >= 500;

    private static string Shorten(string text) => text.Length <= 500 ? text : text[..500];

    private static string FatalMessage(int status) => status switch
    {
        401 => "OpenAI odmítlo klíč API (HTTP 401). Zkontrolujte OPENAI_API_KEY.",
        403 => "OpenAI odmítlo přístup (HTTP 403), například k modelu.",
        _ => "OpenAI vrátilo HTTP 429 insufficient_quota: na účtu došel kredit.",
    };
}
