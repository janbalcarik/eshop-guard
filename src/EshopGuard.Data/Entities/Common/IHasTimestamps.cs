namespace EshopGuard.Data.Entities.Common;

/// <summary>Has <c>created_at</c>, filled when the row is added.</summary>
public interface IHasCreatedAt
{
    /// <summary>When the row was added (UTC).</summary>
    DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Has <c>created_at</c> and <c>updated_at</c>; <c>updated_at</c> changes on every update.</summary>
public interface IHasTimestamps : IHasCreatedAt
{
    /// <summary>When the row last changed (UTC).</summary>
    DateTimeOffset UpdatedAt { get; set; }
}
