using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

namespace EshopGuard.Api.Http;

/// <summary>
/// Writes a stream of server-sent events (change 11, AD 13): <c>text/event-stream</c> with <c>Cache-Control: no-store</c> and
/// <c>X-Accel-Buffering: no</c> (the proxy must not buffer it), every event flushed at once; the data in the JSON of the API.
/// </summary>
public sealed class SseWriter
{
    private readonly HttpResponse response;
    private readonly JsonSerializerOptions json;

    private SseWriter(HttpResponse response, JsonSerializerOptions json)
    {
        this.response = response;
        this.json = json;
    }

    /// <summary>Starts the stream (headers sent); errors after this can only end it.</summary>
    public static async Task<SseWriter> StartAsync(HttpContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        var response = context.Response;
        response.StatusCode = StatusCodes.Status200OK;
        response.ContentType = "text/event-stream; charset=utf-8";
        response.Headers.CacheControl = "no-store";
        response.Headers["X-Accel-Buffering"] = "no";
        context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpResponseBodyFeature>()?.DisableBuffering();
        await response.Body.FlushAsync(ct).ConfigureAwait(false);
        return new SseWriter(response, context.RequestServices.GetRequiredService<IOptions<JsonOptions>>().Value.SerializerOptions);
    }

    /// <summary><c>id: …</c> (when given), <c>event: …</c>, <c>data: {json}</c>.</summary>
    public Task EventAsync(string name, object data, long? id, CancellationToken ct)
    {
        var text = new StringBuilder();
        if (id is { } value)
        {
            text.Append("id: ").Append(value.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('\n');
        }

        text.Append("event: ").Append(name).Append('\n');
        text.Append("data: ").Append(JsonSerializer.Serialize(data, data.GetType(), json)).Append("\n\n");
        return WriteAsync(text.ToString(), ct);
    }

    /// <summary>A comment that keeps the connection open (<c>: ping</c>).</summary>
    public Task CommentAsync(string comment, CancellationToken ct) => WriteAsync(": " + comment + "\n\n", ct);

    private async Task WriteAsync(string text, CancellationToken ct)
    {
        await response.WriteAsync(text, Encoding.UTF8, ct).ConfigureAwait(false);
        await response.Body.FlushAsync(ct).ConfigureAwait(false);
    }
}
