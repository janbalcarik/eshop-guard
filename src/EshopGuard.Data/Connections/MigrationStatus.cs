namespace EshopGuard.Data.Connections;

/// <summary>Comparison of migrations applied in the database with migrations known to the code.</summary>
public sealed record MigrationStatus(IReadOnlyList<string> Pending, IReadOnlyList<string> Ahead)
{
    /// <summary>True when the database lacks a migration the code knows.</summary>
    public bool HasPending => Pending.Count > 0;

    /// <summary>Compares the two lists (ordinal, order kept as in the input).</summary>
    public static MigrationStatus Evaluate(IReadOnlyCollection<string> applied, IReadOnlyCollection<string> known)
    {
        ArgumentNullException.ThrowIfNull(applied);
        ArgumentNullException.ThrowIfNull(known);
        var appliedSet = new HashSet<string>(applied, StringComparer.Ordinal);
        var knownSet = new HashSet<string>(known, StringComparer.Ordinal);
        return new MigrationStatus(
            known.Where(m => !appliedSet.Contains(m)).ToList(),
            applied.Where(m => !knownSet.Contains(m)).ToList());
    }
}
