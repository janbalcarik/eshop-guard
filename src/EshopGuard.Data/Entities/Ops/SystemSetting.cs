using System.Text.Json;
using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Ops;

/// <summary>System setting (Jev and OpenAI limits, feature switches). Table <c>ops.system_settings</c>.</summary>
public sealed class SystemSetting : IHasTimestamps
{
    public required string Key { get; set; }

    public required JsonDocument Value { get; set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset UpdatedAt { get; set; }
}
