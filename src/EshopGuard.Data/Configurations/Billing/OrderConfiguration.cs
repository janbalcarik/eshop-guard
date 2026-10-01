using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Billing;
using EshopGuard.Data.Entities.Checks;
using EshopGuard.Data.Entities.Iam;
using EshopGuard.Data.Entities.Shops;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EshopGuard.Data.Configurations.Billing;

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders", Schemas.Billing);
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.TenantId, x.Id });
        builder.HasEnum(x => x.Kind);
        builder.Property(x => x.AmountNet).HasPrecision(12, 2);
        builder.Property(x => x.DiscountAmount).HasPrecision(12, 2);
        builder.Property(x => x.VatRate).HasPrecision(5, 2);
        builder.Property(x => x.VatAmount).HasPrecision(12, 2);
        builder.Property(x => x.AmountGross).HasPrecision(12, 2);
        builder.Property(x => x.Currency).HasColumnType("character(3)");
        builder.HasEnum(x => x.Status);
        builder.HasIndex(x => x.StripeCheckoutSessionId).IsUnique();
        builder.HasTenantForeignKey<Shop>(nameof(Order.ShopId));
        builder.HasOne<PriceList>().WithMany().HasForeignKey(x => x.PriceListId).OnDelete(DeleteBehavior.Restrict);
        builder.HasTenantForeignKey<Run>(nameof(Order.RunId));
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
