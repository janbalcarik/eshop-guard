using System.Text.Json;
using System.Text.Json.Serialization;

namespace EshopGuard.Jobs.Shops;

/// <summary>
/// The last recognition of the platform in <c>shop.shops.detection</c>: the state, the id of the running job, the result as
/// codes (platform, confidence, signals), the final address, another domain the home page redirects to, the code of a
/// failure, the time, and whether the platform was set by the user (then the recognition does not overwrite it).
/// </summary>
public sealed record ShopDetectionState
{
    public const string Pending = "pending";
    public const string Done = "done";
    public const string Failed = "failed";
    public const string SourceDetected = "detected";
    public const string SourceUser = "user";

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string Status { get; init; } = Pending;

    public long? JobId { get; init; }

    public string? Platform { get; init; }

    public string? Confidence { get; init; }

    public IReadOnlyList<string> Signals { get; init; } = [];

    public string? FinalUrl { get; init; }

    public string? RedirectedTo { get; init; }

    public string? FailureCode { get; init; }

    public DateTimeOffset? RequestedAt { get; init; }

    public DateTimeOffset? DetectedAt { get; init; }

    public string PlatformSource { get; init; } = SourceDetected;

    public static ShopDetectionState? Read(JsonDocument? document) =>
        document is null ? null : document.RootElement.Deserialize<ShopDetectionState>(Json);

    public string ToJson() => JsonSerializer.Serialize(this, Json);
}
