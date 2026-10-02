using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EshopGuard.Data.Configurations.Billing;

internal sealed class VolumeDiscountConfiguration : IEntityTypeConfiguration<VolumeDiscount>
{
    public void Configure(EntityTypeBuilder<VolumeDiscount> builder)
    {
        builder.ToTable("volume_discounts", Schemas.Billing);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Percent).HasPrecision(5, 2);
        builder.HasEnum(x => x.StripeMode);
        builder.HasIndex(x => new { x.PriceListId, x.FromShopNumber }).IsUnique();
        builder.HasOne<PriceList>().WithMany().HasForeignKey(x => x.PriceListId).OnDelete(DeleteBehavior.Restrict);
    }
}
