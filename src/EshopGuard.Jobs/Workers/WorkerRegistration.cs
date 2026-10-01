using EshopGuard.Data.Entities.Ops;

namespace EshopGuard.Jobs.Workers;

/// <summary>A worker instance in <c>ops.workers</c>: id, build version and slots per resource class.</summary>
public sealed record WorkerRegistration(string Id, string? Version, IReadOnlyDictionary<JobResourceClass, int> Slots);
