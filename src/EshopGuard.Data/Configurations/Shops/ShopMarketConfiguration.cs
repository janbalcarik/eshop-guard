using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Checks;
using EshopGuard.Data.Entities.Iam;
using EshopGuard.Data.Entities.Shops;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EshopGuard.Data.Configurations.Shops;

internal sealed class ShopMarketConfiguration : IEntityTypeConfiguration<ShopMarket>
{
    public void Configure(EntityTypeBuilder<ShopMarket> builder)
    {
        builder.ToTable("shop_markets", Schemas.Shops);
        builder.HasKey(x => new { x.ShopId, x.CountryCode });
        builder.HasEnum(x => x.Status);
        builder.HasEnum(x => x.EvidenceLevel);
        builder.HasEnum(x => x.Source);
        builder.HasTenantForeignKey<Shop>(nameof(ShopMarket.ShopId));
        builder.HasTenantForeignKey<Run>(nameof(ShopMarket.DetectionRunId));
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.ConfirmedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
