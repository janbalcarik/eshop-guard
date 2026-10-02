using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Checks;
using EshopGuard.Data.Entities.Content;
using EshopGuard.Data.Entities.Shops;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EshopGuard.Data.Configurations.Checks;

internal sealed class FindingConfiguration : IEntityTypeConfiguration<Finding>
{
    public void Configure(EntityTypeBuilder<Finding> builder)
    {
        builder.ToTable("findings", Schemas.Checks);
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.TenantId, x.Id });
        builder.HasEnum(x => x.Checkability);
        builder.HasEnum(x => x.Band);
        builder.HasEnum(x => x.Scope);
        builder.HasEnum(x => x.Status);
        builder.HasIndex(x => new { x.ShopId, x.RuleId, x.SegmentHash }).IsUnique().HasFilter("scope = 'segment'");
        builder.HasIndex(x => new { x.ShopId, x.RuleId, x.PageId }).IsUnique().HasFilter("scope = 'page'");
        builder.HasIndex(x => new { x.ShopId, x.RuleId }).IsUnique().HasFilter("scope = 'site'");
        builder.HasIndex(x => new { x.TenantId, x.ShopId, x.Status });
        builder.HasTenantForeignKey<Shop>(nameof(Finding.ShopId));
        builder.HasOne<RuleSet>().WithMany().HasForeignKey(x => x.RuleSetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasPartitionedTenantForeignKey<Page>(nameof(Finding.PageId));
        builder.HasTenantForeignKey<Run>(nameof(Finding.FirstRunId));
        builder.HasTenantForeignKey<Run>(nameof(Finding.LastSeenRunId));
        builder.HasTenantForeignKey<Run>(nameof(Finding.ResolvedRunId));
    }
}
