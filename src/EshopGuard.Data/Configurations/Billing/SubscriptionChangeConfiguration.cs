using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EshopGuard.Data.Configurations.Billing;

internal sealed class SubscriptionChangeConfiguration : IEntityTypeConfiguration<SubscriptionChange>
{
    public void Configure(EntityTypeBuilder<SubscriptionChange> builder)
    {
        builder.ToTable("subscription_changes", Schemas.Billing);
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.TenantId, x.Id });
        builder.HasEnum(x => x.Kind);
        builder.HasEnum(x => x.Status);
        builder.HasTenantForeignKey<Subscription>(nameof(SubscriptionChange.SubscriptionId));
    }
}
