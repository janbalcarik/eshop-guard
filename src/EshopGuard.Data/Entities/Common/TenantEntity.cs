namespace EshopGuard.Data.Entities.Common;

/// <summary>Row of a tenant with a time-ordered <c>uuid</c> v7 identifier and timestamps.</summary>
public abstract class TenantEntity : ITenantOwned, IHasTimestamps
{
    /// <summary>Identifier (<c>uuid</c> v7, generated in .NET; the database default is <c>uuidv7()</c>).</summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Owning tenant; filled from the tenant context when left empty.</summary>
    public Guid TenantId { get; set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset UpdatedAt { get; set; }
}
