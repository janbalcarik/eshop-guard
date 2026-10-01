namespace EshopGuard.Jobs.Workers;

/// <summary>Lease of a domain held by a job, with the politeness state left by the previous batch.</summary>
public sealed record DomainLease(string Domain, long JobId, DomainPolitenessState State);
