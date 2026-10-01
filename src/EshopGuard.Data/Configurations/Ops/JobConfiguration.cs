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
        builder.HasIndex(x => x.LeaseUntil).HasFilter("state = 'running'");
        builder.HasIndex(x => x.RunId);

        // Queue (change 4): claim in the order (priority, id) separately for jobs without and with a concurrency key (not_before
        // is a filter: an index ordered by it would make the claim read and sort a whole priority), the first job per key, at most
        // one running job per key, tenant cap, cleanup.
        builder.HasIndex(x => x.ConcurrencyKey, "ux_jobs_concurrency_running").HasDatabaseName("ux_jobs_concurrency_running").IsUnique()
            .HasFilter("state = 'running' AND concurrency_key IS NOT NULL");
        builder.HasIndex(x => new { x.ResourceClass, x.Priority, x.Id }, "ix_jobs_queued_unkeyed").HasDatabaseName("ix_jobs_queued_unkeyed")
            .HasFilter("state = 'queued' AND concurrency_key IS NULL");
        builder.HasIndex(x => new { x.ResourceClass, x.Priority, x.Id }, "ix_jobs_queued_keyed").HasDatabaseName("ix_jobs_queued_keyed")
            .HasFilter("state = 'queued' AND concurrency_key IS NOT NULL");
        builder.HasIndex(x => new { x.ConcurrencyKey, x.Priority, x.Id }, "ix_jobs_queued_key_head").HasDatabaseName("ix_jobs_queued_key_head")
            .HasFilter("state = 'queued' AND concurrency_key IS NOT NULL");
        builder.HasIndex(x => new { x.TenantId, x.ResourceClass }, "ix_jobs_running_tenant").HasDatabaseName("ix_jobs_running_tenant")
            .HasFilter("state = 'running'");
        builder.HasIndex(x => x.FinishedAt, "ix_jobs_finished").HasDatabaseName("ix_jobs_finished")
            .HasFilter("state IN ('succeeded', 'canceled', 'failed')");

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_jobs_priority", "priority BETWEEN 0 AND 4");
            t.HasCheckConstraint("ck_jobs_attempts", "attempts >= 0 AND max_attempts >= 1");
            t.HasCheckConstraint("ck_jobs_lease", "(state = 'running') = (lease_owner IS NOT NULL AND lease_until IS NOT NULL)");
        });
    }
}
