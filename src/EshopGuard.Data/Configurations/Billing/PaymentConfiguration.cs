using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EshopGuard.Data.Configurations.Billing;

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments", Schemas.Billing);
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.TenantId, x.Id });
        builder.Property(x => x.AmountGross).HasPrecision(12, 2);
        builder.Property(x => x.Currency).HasColumnType("character(3)");
        builder.HasEnum(x => x.Status);
        builder.Property(x => x.RefundedAmount).HasPrecision(12, 2);
        builder.HasIndex(x => x.StripeInvoiceId).IsUnique();
        builder.HasIndex(x => x.StripeChargeId);
        builder.HasTenantForeignKey<Order>(nameof(Payment.OrderId));
        builder.HasTenantForeignKey<Subscription>(nameof(Payment.SubscriptionId));
    }
}
