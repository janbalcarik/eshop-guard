using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Checks;
using EshopGuard.Data.Entities.Iam;
using EshopGuard.Data.Entities.Shops;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EshopGuard.Data.Configurations.Shops;

internal sealed class ShopConfiguration : IEntityTypeConfiguration<Shop>
{
    public void Configure(EntityTypeBuilder<Shop> builder)
    {
        builder.ToTable("shops", Schemas.Shops);
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.TenantId, x.Id });
        builder.HasEnum(x => x.Platform);
        builder.HasEnum(x => x.SourceMode);
        builder.HasEnum(x => x.Status);
        builder.HasEnum(x => x.VerificationMethod);
        builder.HasIndex(x => new { x.TenantId, x.Domain, x.BasePath }).IsUnique().HasFilter("deleted_at IS NULL");
        builder.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasTenantForeignKey<Run>(nameof(Shop.LastFullRunId));
    }
}
