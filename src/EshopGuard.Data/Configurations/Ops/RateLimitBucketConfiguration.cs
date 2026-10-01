using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Ops;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EshopGuard.Data.Configurations.Ops;

internal sealed class RateLimitBucketConfiguration : IEntityTypeConfiguration<RateLimitBucket>
{
    public void Configure(EntityTypeBuilder<RateLimitBucket> builder)
    {
        builder.ToTable("rate_limit_buckets", Schemas.Ops);
        builder.HasKey(x => x.Key);
    }
}
