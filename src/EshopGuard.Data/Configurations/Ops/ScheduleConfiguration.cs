using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Ops;
using EshopGuard.Data.Entities.Shops;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EshopGuard.Data.Configurations.Ops;

internal sealed class ScheduleConfiguration : IEntityTypeConfiguration<Schedule>
{
    public void Configure(EntityTypeBuilder<Schedule> builder)
    {
        builder.ToTable("schedules", Schemas.Ops);
        builder.HasKey(x => new { x.ShopId, x.Kind });
        builder.HasEnum(x => x.Kind);
        builder.HasIndex(x => x.NextRunAt);
        builder.HasTenantForeignKey<Shop>(nameof(Schedule.ShopId));
    }
}
