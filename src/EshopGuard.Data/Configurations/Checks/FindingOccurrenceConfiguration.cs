using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Checks;
using EshopGuard.Data.Entities.Content;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EshopGuard.Data.Configurations.Checks;

internal sealed class FindingOccurrenceConfiguration : IEntityTypeConfiguration<FindingOccurrence>
{
    public void Configure(EntityTypeBuilder<FindingOccurrence> builder)
    {
        builder.ToTable("finding_occurrences", Schemas.Checks);
        builder.HasKey(x => new { x.FindingId, x.PageId });
        builder.HasIndex(x => new { x.ShopId, x.PageId });
        builder.HasTenantForeignKey<Finding>(nameof(FindingOccurrence.FindingId));
        builder.HasPartitionedTenantForeignKey<Page>(nameof(FindingOccurrence.PageId));
    }
}
