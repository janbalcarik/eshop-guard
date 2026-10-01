using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EshopGuard.Data.Configurations.Conventions;

/// <summary>
/// Composite foreign keys that keep references inside one tenant: a row of tenant A cannot point to a row of tenant B,
/// not even with a role that bypasses RLS.
/// </summary>
public static class TenantKeyExtensions
{
    /// <summary>
    /// (<c>tenant_id</c>, <paramref name="property"/>) → (<c>tenant_id</c>, <c>id</c>) of <typeparamref name="TPrincipal"/>,
    /// <c>ON DELETE RESTRICT</c>. Optional when <paramref name="property"/> is nullable.
    /// </summary>
    public static ReferenceCollectionBuilder HasTenantForeignKey<TPrincipal>(this EntityTypeBuilder builder, string property)
        where TPrincipal : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.HasOne(typeof(TPrincipal)).WithMany()
            .HasForeignKey("TenantId", property)
            .HasPrincipalKey("TenantId", "Id")
            .OnDelete(DeleteBehavior.Restrict);
    }

    /// <summary>
    /// (<c>tenant_id</c>, <c>shop_id</c>, <paramref name="property"/>) → (<c>tenant_id</c>, <c>shop_id</c>, <c>id</c>) of a table
    /// partitioned by <c>shop_id</c> (its unique keys must contain the partition key), <c>ON DELETE RESTRICT</c>.
    /// </summary>
    public static ReferenceCollectionBuilder HasPartitionedTenantForeignKey<TPrincipal>(this EntityTypeBuilder builder, string property)
        where TPrincipal : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.HasOne(typeof(TPrincipal)).WithMany()
            .HasForeignKey("TenantId", "ShopId", property)
            .HasPrincipalKey("TenantId", "ShopId", "Id")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
