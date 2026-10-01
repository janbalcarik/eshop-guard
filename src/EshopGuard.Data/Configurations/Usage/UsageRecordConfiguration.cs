using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Usage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EshopGuard.Data.Configurations.Usage;

internal sealed class UsageRecordConfiguration : IEntityTypeConfiguration<UsageRecord>
{
    public void Configure(EntityTypeBuilder<UsageRecord> builder)
    {
        builder.ToTable("usage_records", Schemas.Usage);
        builder.HasKey(x => new { x.Id, x.OccurredAt });
        builder.Property(x => x.Id).UseIdentityAlwaysColumn();
        builder.HasEnum(x => x.Provider);
        builder.HasEnum(x => x.Operation);
        builder.Property(x => x.CostUsd).HasPrecision(14, 6);
        builder.HasIndex(x => new { x.TenantId, x.OccurredAt });
        builder.HasIndex(x => x.RunId);
    }
}
