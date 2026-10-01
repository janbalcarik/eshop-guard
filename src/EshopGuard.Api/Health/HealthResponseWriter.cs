using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace EshopGuard.Api.Health;

/// <summary>
/// Body of <c>/health</c>: names, states and codes only. Descriptions and exceptions are never written
/// (they could carry host names or connection details).
/// </summary>
public static class HealthResponseWriter
{
    /// <summary>Code of a failed check that did not report its own (e.g. it threw).</summary>
    public const string UnknownFailureCode = "check.failed";

    /// <summary>Writes the report as JSON.</summary>
    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(report);
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.Headers.CacheControl = "no-store";
        var body = new
        {
            status = report.Status == HealthStatus.Healthy ? "ok" : "failed",
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status == HealthStatus.Healthy ? "ok" : "failed",
                code = e.Value.Status == HealthStatus.Healthy
                    ? null
                    : e.Value.Data.TryGetValue(DatabaseHealthCheck.CodeKey, out var code) && code is string text ? text : UnknownFailureCode,
            }),
        };
        return JsonSerializer.SerializeAsync(context.Response.Body, body, Options, context.RequestAborted);
    }

    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };
}
