using System.Text.Json;
using EshopGuard.Data.Entities.Ops;

namespace EshopGuard.Jobs.Queue;

/// <summary>
/// A job taken by a worker. <paramref name="Attempt"/> (= <c>attempts</c> after the claim) and <paramref name="LeaseOwner"/>
/// are the fencing token: heartbeat, completion, failure and deferral apply only while both still match.
/// </summary>
public sealed record ClaimedJob(
    long Id,
    string Kind,
    JobResourceClass ResourceClass,
    JobPriority Priority,
    Guid? TenantId,
    Guid? ShopId,
    Guid? RunId,
    JsonDocument Payload,
    int Attempt,
    int MaxAttempts,
    string? ConcurrencyKey,
    string LeaseOwner);
