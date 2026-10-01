namespace EshopGuard.Data.Entities.Common;

/// <summary>Row of a global table (no tenant, no RLS) with a <c>uuid</c> v7 identifier and timestamps.</summary>
public abstract class GlobalEntity : IHasTimestamps
{
    /// <summary>Identifier (<c>uuid</c> v7, generated in .NET; the database default is <c>uuidv7()</c>).</summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset UpdatedAt { get; set; }
}
