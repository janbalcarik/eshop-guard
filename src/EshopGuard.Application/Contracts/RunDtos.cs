using System.Text.Json;

namespace EshopGuard.Application.Contracts;

/// <summary>Batches of one step of a run done out of planned.</summary>
public sealed record RunStepDto(long Done, long Total);

/// <summary>Progress of a run: pages planned, fetched and processed, and the batches of the steps.</summary>
public sealed record RunProgressDto(long? PagesPlanned, long PagesFetched, long PagesProcessed, IReadOnlyDictionary<string, RunStepDto> Steps);

/// <summary>Position in the queue and the estimated end (change 8; both null when not known).</summary>
public sealed record RunQueueDto(int? Position, DateTimeOffset? EstimatedFinishAt);

/// <summary>
/// A run of an e-shop (change 11, AD 13): codes, times and numbers only; the internal estimate, amounts and names of services
/// never get here (design of change 8).
/// </summary>
public sealed record RunDto(
    Guid Id, Guid ShopId, string Kind, string Trigger, string Status, RunProgressDto Progress, IReadOnlyList<string> Jurisdictions, IReadOnlyList<string> Modules,
    DateTimeOffset CreatedAt, DateTimeOffset? StartedAt, DateTimeOffset? FinishedAt, string? ErrorCode, bool CancelRequested, RunQueueDto Queue);

/// <summary>An event of a run (<c>checks.run_events</c>): its code and data, never a text of a page.</summary>
public sealed record RunEventDto(long Id, DateTimeOffset At, string Level, string Code, JsonElement? Data);

/// <summary>The state of a run sent when it changes (<c>event: status</c>).</summary>
public sealed record RunStatusDto(string Status, string? ErrorCode, DateTimeOffset? FinishedAt, bool CancelRequested);

/// <summary>A change on an e-shop (<c>GET S/events</c>): the entity, its id and its state now.</summary>
public sealed record ShopChangeDto(string Entity, Guid Id, string? Status, string? RecheckStatus);
