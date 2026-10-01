using System.Text.Json;
using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Fixes;

/// <summary>PDF protocol of checks for a period. Table <c>fixes.protocols</c>.</summary>
public sealed class Protocol : TenantEntity
{
    public Guid ShopId { get; set; }

    public required string Number { get; set; }

    public DateOnly PeriodFrom { get; set; }

    public DateOnly PeriodTo { get; set; }

    public required string Locale { get; set; }

    public Guid? GeneratedBy { get; set; }

    public string? PdfBlobKey { get; set; }

    public JsonDocument? Summary { get; set; }

    public Guid[] RuleSetIds { get; set; } = [];
}
