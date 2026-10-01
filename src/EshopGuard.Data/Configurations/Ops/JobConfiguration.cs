using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Ops;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EshopGuard.Data.Configurations.Ops;

internal sealed class JobConfiguration : IEntityTypeConfiguration<Job>
{
    public void Configure(EntityTypeBuilder<Job> builder)
    {
        builder.ToTable("jobs", Schemas.Ops);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityAlwaysColumn();
        builder.HasEnum(x => x.ResourceClass);
        builder.HasEnum(x => x.State);
        builder.Property(x => x.NotBefore).HasDefaultValueSql("now()");
        builder.HasIndex(x => x.DedupeKey).IsUnique();
        builder.HasIndex(x => new { x.ResourceClass, x.Priority, x.NotBefore, x.Id }).HasFilter("state = 'queued'");
        builder.HasIndex(x => x.LeaseUntil).HasFilter("state = 'running'");
        builder.HasIndex(x => x.RunId);
        builder.ToTable(t => t.HasCheckConstraint("ck_jobs_priority", "priority BETWEEN 0 AND 4"));
    }
}
