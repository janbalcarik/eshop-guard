using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Checks;
using EshopGuard.Data.Entities.Content;
using EshopGuard.Data.Entities.Fixes;
using EshopGuard.Data.Entities.Iam;
using EshopGuard.Data.Entities.Shops;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EshopGuard.Data.Configurations.Fixes;

internal sealed class FixProposalConfiguration : IEntityTypeConfiguration<FixProposal>
{
    public void Configure(EntityTypeBuilder<FixProposal> builder)
    {
        builder.ToTable("fix_proposals", Schemas.Fixes);
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.TenantId, x.Id });
        builder.HasEnum(x => x.Field);
        builder.HasEnum(x => x.RecheckStatus);
        builder.HasEnum(x => x.Status);
        builder.Property(x => x.Version).IsRowVersion();
        builder.HasIndex(x => new { x.TenantId, x.ShopId, x.Status });
        builder.HasTenantForeignKey<Shop>(nameof(FixProposal.ShopId));
        builder.HasPartitionedTenantForeignKey<Page>(nameof(FixProposal.PageId));
        builder.HasPartitionedTenantForeignKey<PageVersion>(nameof(FixProposal.PageVersionId));
        builder.HasTenantForeignKey<FixGroup>(nameof(FixProposal.GroupId));
        builder.HasTenantForeignKey<Run>(nameof(FixProposal.CreatedRunId));
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.DecidedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
