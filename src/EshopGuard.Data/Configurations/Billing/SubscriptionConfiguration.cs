using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Billing;
using EshopGuard.Data.Entities.Shops;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EshopGuard.Data.Configurations.Billing;

internal sealed class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> builder)
    {
        builder.ToTable("subscriptions", Schemas.Billing);
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.TenantId, x.Id });
        builder.HasEnum(x => x.Status);
        builder.HasEnum(x => x.Interval);
        builder.Property(x => x.UnitPrice).HasPrecision(12, 2);
        builder.Property(x => x.DiscountPercent).HasPrecision(5, 2);
        builder.Property(x => x.Version).IsRowVersion();
        builder.HasIndex(x => x.ShopId).IsUnique().HasFilter("status <> 'canceled'");
        builder.HasIndex(x => x.StripeSubscriptionId).IsUnique();
        builder.HasTenantForeignKey<Shop>(nameof(Subscription.ShopId));
        builder.HasOne<PriceList>().WithMany().HasForeignKey(x => x.PriceListId).OnDelete(DeleteBehavior.Restrict);
    }
}
