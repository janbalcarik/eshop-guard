using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Fixes;
using EshopGuard.Data.Entities.Iam;
using EshopGuard.Data.Entities.Shops;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EshopGuard.Data.Configurations.Fixes;

internal sealed class FixGroupConfiguration : IEntityTypeConfiguration<FixGroup>
{
    public void Configure(EntityTypeBuilder<FixGroup> builder)
    {
        builder.ToTable("fix_groups", Schemas.Fixes);
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.TenantId, x.Id });
        builder.HasEnum(x => x.Kind);
        builder.HasEnum(x => x.Status);
        builder.HasEnum(x => x.Mode);
        builder.HasEnum(x => x.RecheckStatus);
        builder.Property(x => x.Version).IsRowVersion();
        builder.HasIndex(x => new { x.TenantId, x.ShopId, x.Status });
        builder.HasTenantForeignKey<Shop>(nameof(FixGroup.ShopId));
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.ApprovedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
