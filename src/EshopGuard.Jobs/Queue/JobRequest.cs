using System.Text.Json;
using EshopGuard.Data.Entities.Ops;

namespace EshopGuard.Jobs.Queue;

/// <summary>
/// A job to enqueue. <paramref name="Payload"/> holds identifiers and parameters only, never page texts (at most
/// <see cref="MaxPayloadBytes"/>). <paramref name="DedupeKey"/>: a second job with the same key is not created;
/// <paramref name="ConcurrencyKey"/>: at most one job with the key runs at a time (e.g. <see cref="Workers.JobKeys.Domain"/>).
/// </summary>
public sealed record JobRequest(
    string Kind,
    JobResourceClass ResourceClass,
    JobPriority Priority,
    JsonDocument Payload,
    Guid? TenantId = null,
    Guid? ShopId = null,
    Guid? RunId = null,
    string? DedupeKey = null,
    string? ConcurrencyKey = null,
    int? MaxAttempts = null,
    DateTimeOffset? NotBefore = null)
{
    /// <summary>Largest payload (UTF-8 JSON), 64 kB.</summary>
    public const int MaxPayloadBytes = 64 * 1024;

    /// <summary>Empty JSON object as payload.</summary>
    public static JsonDocument EmptyPayload() => JsonDocument.Parse("{}");
}
