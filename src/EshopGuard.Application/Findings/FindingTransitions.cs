using System.Text.Json.Nodes;
using EshopGuard.Application.Audit;
using EshopGuard.Data.Entities.Checks;

namespace EshopGuard.Application.Findings;

/// <summary>
/// Moves a finding to another state through <see cref="FindingStatusMachine"/> and writes the audit
/// <c>finding.status_changed</c> (from, to, reason code, who); never a text. The caller saves the change in its transaction.
/// </summary>
public sealed class FindingTransitions(SecurityAuditWriter audit, TimeProvider time)
{
    public async Task ApplyAsync(Finding finding, FindingStatus to, Guid? userId, CancellationToken ct, string? reasonCode = null)
    {
        ArgumentNullException.ThrowIfNull(finding);
        if (finding.Status == to)
        {
            return;
        }

        FindingStatusMachine.Ensure(finding.Status, to);
        var from = finding.Status;
        finding.Status = to;
        finding.UpdatedAt = time.GetUtcNow();
        if (to == FindingStatus.Resolved)
        {
            finding.ResolvedAt = time.GetUtcNow();
        }

        var data = new JsonObject { ["from"] = FindingStatusMachine.Text(from), ["to"] = FindingStatusMachine.Text(to) };
        if (reasonCode is not null)
        {
            data["reasonCode"] = reasonCode;
        }

        await audit.WriteAsync(new AuditEvent(AuditActions.FindingStatusChanged, finding.TenantId, userId, "finding", finding.Id.ToString("D"), data), ct)
            .ConfigureAwait(false);
    }

    /// <summary>True when the finding may move to the state (a step that would not be allowed is skipped by the caller).</summary>
    public static bool Can(Finding finding, FindingStatus to) => finding is not null && (finding.Status == to || FindingStatusMachine.IsAllowed(finding.Status, to));
}
