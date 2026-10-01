namespace EshopGuard.Data.Entities.Common;

/// <summary>Deleted softly through <c>deleted_at</c>; default queries do not return deleted rows (EF filter <c>"SoftDelete"</c>).</summary>
public interface ISoftDeletable
{
    /// <summary>When the row was deleted, or <c>null</c>.</summary>
    DateTimeOffset? DeletedAt { get; set; }
}
