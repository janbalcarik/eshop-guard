using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EshopGuard.Core.Crawl;

/// <summary>
/// Recording of the answers of a site for offline runs: <c>index.jsonl</c> with one line per request (URL, status,
/// headers, reference to the body) and the bodies in <c>bodies/{sha256}.bin</c>. A recording holds content of a foreign
/// site; it stays local (folder <c>snapshots/</c>, outside git).
/// </summary>
public static class PageRecording
{
    /// <summary>Name of the index file.</summary>
    public const string IndexFile = "index.jsonl";

    /// <summary>Folder of the bodies.</summary>
    public const string BodiesFolder = "bodies";

    internal static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>One recorded answer.</summary>
    internal sealed record Entry(
        string Url,
        int Status,
        string? MediaType = null,
        string? Charset = null,
        string? ETag = null,
        DateTimeOffset? LastModified = null,
        string? Location = null,
        double? RetryAfterSeconds = null,
        string? Error = null,
        string? Body = null);
}

/// <summary>
/// Passes every request to another fetcher and records the answer (<see cref="PageRecording"/>). The folder is emptied of
/// a previous index when the recorder is created; stored bodies are reused.
/// </summary>
public sealed class RecordingPageFetcher : IPageFetcher
{
    private readonly IPageFetcher _inner;
    private readonly string _index;
    private readonly string _bodies;
    private readonly Lock _gate = new();

    /// <summary>Records the answers of <paramref name="inner"/> into <paramref name="directory"/>.</summary>
    public RecordingPageFetcher(IPageFetcher inner, string directory)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _inner = inner;
        _bodies = Path.Combine(directory, PageRecording.BodiesFolder);
        Directory.CreateDirectory(_bodies);
        _index = Path.Combine(directory, PageRecording.IndexFile);
        File.WriteAllText(_index, "");
    }

    /// <inheritdoc />
    public Task<FetchResponse> FetchAsync(Uri url, CancellationToken ct) => FetchAsync(new FetchRequest(url), ct);

    /// <inheritdoc />
    public async Task<FetchResponse> FetchAsync(FetchRequest request, CancellationToken ct)
    {
        var url = request.Url;
        var response = await _inner.FetchAsync(request, ct);
        string? body = null;
        if (response.Body is { } bytes)
        {
            body = Convert.ToHexStringLower(SHA256.HashData(bytes));
            var path = Path.Combine(_bodies, body + ".bin");
            if (!File.Exists(path))
            {
                await File.WriteAllBytesAsync(path, bytes, ct);
            }
        }

        var entry = new PageRecording.Entry(
            url.AbsoluteUri, response.StatusCode, response.MediaType, response.Charset, response.ETag, response.LastModified,
            response.RedirectLocation?.AbsoluteUri, response.RetryAfter?.TotalSeconds, response.Error, body);
        var line = JsonSerializer.Serialize(entry, PageRecording.Json) + "\n";
        lock (_gate)
        {
            File.AppendAllText(_index, line, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        return response;
    }
}

/// <summary>
/// Answers from a recording (<see cref="PageRecording"/>) without network. Repeated requests of one URL get the recorded
/// answers in order, the last one again after that; a URL that is not in the recording gets 404 and a log record.
/// </summary>
public sealed class ReplayPageFetcher : IPageFetcher
{
    private readonly string _bodies;
    private readonly Dictionary<string, List<PageRecording.Entry>> _answers = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, int> _served = new(StringComparer.Ordinal);
    private readonly ILogger _logger;

    /// <summary>Reads the recording in <paramref name="directory"/>.</summary>
    public ReplayPageFetcher(string directory, ILogger<ReplayPageFetcher>? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        var index = Path.Combine(directory, PageRecording.IndexFile);
        if (!File.Exists(index))
        {
            throw new FileNotFoundException($"Recording not found: {index}", index);
        }

        _bodies = Path.Combine(directory, PageRecording.BodiesFolder);
        _logger = logger ?? NullLogger<ReplayPageFetcher>.Instance;
        foreach (var line in File.ReadLines(index))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var entry = JsonSerializer.Deserialize<PageRecording.Entry>(line, PageRecording.Json)
                ?? throw new InvalidDataException($"Invalid line in {index}.");
            if (!_answers.TryGetValue(entry.Url, out var list))
            {
                _answers[entry.Url] = list = [];
            }

            list.Add(entry);
        }
    }

    /// <summary>Number of recorded answers.</summary>
    public int Count => _answers.Values.Sum(l => l.Count);

    /// <inheritdoc />
    public async Task<FetchResponse> FetchAsync(Uri url, CancellationToken ct)
    {
        if (!_answers.TryGetValue(url.AbsoluteUri, out var list))
        {
            _logger.LogWarning("Replay: {Url} is not in the recording, answering 404", url);
            return new FetchResponse { Url = url, StatusCode = 404 };
        }

        var served = _served.AddOrUpdate(url.AbsoluteUri, 1, (_, n) => n + 1);
        var entry = list[Math.Min(served, list.Count) - 1];
        return new FetchResponse
        {
            Url = url,
            StatusCode = entry.Status,
            MediaType = entry.MediaType,
            Charset = entry.Charset,
            ETag = entry.ETag,
            LastModified = entry.LastModified,
            RedirectLocation = entry.Location is null ? null : new Uri(entry.Location),
            RetryAfter = entry.RetryAfterSeconds is { } seconds ? TimeSpan.FromSeconds(seconds) : null,
            Error = entry.Error,
            Body = entry.Body is null ? null : await File.ReadAllBytesAsync(Path.Combine(_bodies, entry.Body + ".bin"), ct),
        };
    }
}
