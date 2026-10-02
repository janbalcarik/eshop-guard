using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Checks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EshopGuard.Data.Configurations.Checks;

internal sealed class RunUrlConfiguration : IEntityTypeConfiguration<RunUrl>
{
    public void Configure(EntityTypeBuilder<RunUrl> builder)
    {
        builder.ToTable("run_urls", Schemas.Checks);
        builder.HasKey(x => new { x.RunId, x.ScopeKey, x.UrlHash });
        builder.HasEnum(x => x.State);
        builder.HasIndex(x => new { x.RunId, x.State });
        builder.ToTable(t => t.HasCheckConstraint("ck_run_urls_queue",
            "queue IS NULL OR queue IN ('home', 'legal', 'product', 'other', 'sample_pair', 'sample_mandatory', 'sample_random')"));
        builder.HasTenantForeignKey<Run>(nameof(RunUrl.RunId));
    }
}

internal sealed class RunScopeConfiguration : IEntityTypeConfiguration<RunScope>
{
    public void Configure(EntityTypeBuilder<RunScope> builder)
    {
        builder.ToTable("run_scopes", Schemas.Checks);
        builder.HasKey(x => new { x.RunId, x.ScopeKey });
        builder.HasTenantForeignKey<Run>(nameof(RunScope.RunId));
    }
}
