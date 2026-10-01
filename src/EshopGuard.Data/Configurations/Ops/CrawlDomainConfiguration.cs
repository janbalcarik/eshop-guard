using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Ops;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EshopGuard.Data.Configurations.Ops;

internal sealed class CrawlDomainConfiguration : IEntityTypeConfiguration<CrawlDomain>
{
    public void Configure(EntityTypeBuilder<CrawlDomain> builder)
    {
        builder.ToTable("domains", Schemas.Ops);
        builder.HasKey(x => x.Domain);
    }
}
