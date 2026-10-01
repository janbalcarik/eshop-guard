using System.Text.Json;
using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Checks;

/// <summary>Question to the customer about a finding or the whole e-shop. Table <c>checks.questions</c>.</summary>
public sealed class Question : TenantEntity
{
    public Guid ShopId { get; set; }

    public Guid? FindingId { get; set; }

    public QuestionScope Scope { get; set; }

    public required string Code { get; set; }

    public JsonDocument? Params { get; set; }

    public QuestionStatus Status { get; set; }

    public string? Answer { get; set; }

    public Guid? AnsweredBy { get; set; }

    public DateTimeOffset? AnsweredAt { get; set; }

    public Guid? EvidenceId { get; set; }
}
