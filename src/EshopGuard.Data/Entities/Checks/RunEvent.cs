using System.Text.Json;
using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Checks;

/// <summary>Progress event of a run shown to the user (monthly partitions, kept 90 days). Table <c>checks.run_events</c>.</summary>
public sealed class RunEvent : ITenantOwned, IHasCreatedAt
{
    public long Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid RunId { get; set; }

    public DateTimeOffset At { get; set; }

    public required string Level { get; set; }

    public required string Code { get; set; }

    public string? Message { get; set; }

    public JsonDocument? Data { get; set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }
}
