using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Fixes;
using EshopGuard.Data.Entities.Iam;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EshopGuard.Data.Configurations.Fixes;

internal sealed class RewriteCacheEntryConfiguration : IEntityTypeConfiguration<RewriteCacheEntry>
{
    public void Configure(EntityTypeBuilder<RewriteCacheEntry> builder)
    {
        builder.ToTable("rewrite_cache", Schemas.Fixes);
        builder.HasKey(x => new { x.TenantId, x.Key });
        builder.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}
