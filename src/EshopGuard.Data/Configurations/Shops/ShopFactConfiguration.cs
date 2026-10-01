using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Iam;
using EshopGuard.Data.Entities.Shops;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EshopGuard.Data.Configurations.Shops;

internal sealed class ShopFactConfiguration : IEntityTypeConfiguration<ShopFact>
{
    public void Configure(EntityTypeBuilder<ShopFact> builder)
    {
        builder.ToTable("shop_facts", Schemas.Shops);
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.TenantId, x.Id });
        builder.HasTenantForeignKey<Shop>(nameof(ShopFact.ShopId));
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
