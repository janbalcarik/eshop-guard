using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EshopGuard.Data.Configurations.Billing;

internal sealed class StripeEventConfiguration : IEntityTypeConfiguration<StripeEvent>
{
    public void Configure(EntityTypeBuilder<StripeEvent> builder)
    {
        builder.ToTable("stripe_events", Schemas.Billing);
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.TenantId);
    }
}
