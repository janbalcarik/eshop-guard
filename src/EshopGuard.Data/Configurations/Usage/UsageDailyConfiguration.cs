using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Usage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EshopGuard.Data.Configurations.Usage;

internal sealed class UsageDailyConfiguration : IEntityTypeConfiguration<UsageDaily>
{
    public void Configure(EntityTypeBuilder<UsageDaily> builder)
    {
        builder.ToTable("usage_daily", Schemas.Usage);
        builder.HasKey(x => new { x.Day, x.TenantId, x.ShopId, x.Provider, x.Operation });
        builder.HasEnum(x => x.Provider);
        builder.HasEnum(x => x.Operation);
        builder.Property(x => x.CostUsd).HasPrecision(14, 6);
    }
}
