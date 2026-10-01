using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Checks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EshopGuard.Data.Configurations.Checks;

internal sealed class RunEventConfiguration : IEntityTypeConfiguration<RunEvent>
{
    public void Configure(EntityTypeBuilder<RunEvent> builder)
    {
        builder.ToTable("run_events", Schemas.Checks);
        builder.HasKey(x => new { x.Id, x.At });
        builder.Property(x => x.Id).UseIdentityAlwaysColumn();
        builder.HasIndex(x => new { x.RunId, x.Id });
        builder.HasTenantForeignKey<Run>(nameof(RunEvent.RunId));
    }
}
