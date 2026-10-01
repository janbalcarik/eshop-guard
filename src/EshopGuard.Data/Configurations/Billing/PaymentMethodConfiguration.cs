using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Billing;
using EshopGuard.Data.Entities.Iam;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EshopGuard.Data.Configurations.Billing;

internal sealed class PaymentMethodConfiguration : IEntityTypeConfiguration<PaymentMethod>
{
    public void Configure(EntityTypeBuilder<PaymentMethod> builder)
    {
        builder.ToTable("payment_methods", Schemas.Billing);
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.TenantId, x.Id });
        builder.HasIndex(x => x.StripePaymentMethodId).IsUnique();
        builder.HasIndex(x => x.TenantId).IsUnique().HasFilter("is_default AND detached_at IS NULL");
        builder.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}
