namespace EshopGuard.Jobs.Queue;

/// <summary>Priority of a job: P0 interactive (quick check) … P4 maintenance. Lower runs first.</summary>
public enum JobPriority
{
    /// <summary>Interactive: the user waits (quick check, preview).</summary>
    P0 = 0,

    /// <summary>Paid or user-started analysis.</summary>
    P1 = 1,

    /// <summary>Regular background work.</summary>
    P2 = 2,

    /// <summary>Monitoring and bulk work.</summary>
    P3 = 3,

    /// <summary>Maintenance.</summary>
    P4 = 4,
}
