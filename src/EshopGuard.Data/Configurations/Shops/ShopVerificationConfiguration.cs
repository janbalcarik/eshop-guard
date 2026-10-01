using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Shops;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EshopGuard.Data.Configurations.Shops;

internal sealed class ShopVerificationConfiguration : IEntityTypeConfiguration<ShopVerification>
{
    public void Configure(EntityTypeBuilder<ShopVerification> builder)
    {
        builder.ToTable("shop_verifications", Schemas.Shops);
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.TenantId, x.Id });
        builder.HasEnum(x => x.Method);
        builder.HasTenantForeignKey<Shop>(nameof(ShopVerification.ShopId));
    }
}
