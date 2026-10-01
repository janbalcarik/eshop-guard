using System.Text.Json;
using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Checks;

/// <summary>Run of an analysis (state machine of the architecture, part 6). Table <c>checks.runs</c>.</summary>
public sealed class Run : TenantEntity
{
    public Guid ShopId { get; set; }

    public RunKind Kind { get; set; }

    public RunTrigger Trigger { get; set; }

    public RunStatus Status { get; set; }

    public short Priority { get; set; }

    public Guid? OrderId { get; set; }

    public Guid? RequestedBy { get; set; }

    public string[] Jurisdictions { get; set; } = [];

    public string[] Modules { get; set; } = [];

    public Guid[] RuleSetIds { get; set; } = [];

    public JsonDocument? Estimate { get; set; }

    public JsonDocument? Progress { get; set; }

    public JsonDocument? Stats { get; set; }

    public bool CancelRequested { get; set; }

    public string? Error { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }
}
