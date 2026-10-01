using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Checks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EshopGuard.Data.Configurations.Checks;

internal sealed class RuleSetConfiguration : IEntityTypeConfiguration<RuleSet>
{
    public void Configure(EntityTypeBuilder<RuleSet> builder)
    {
        builder.ToTable("rule_sets", Schemas.Checks);
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.Module, x.Version }).IsUnique();
    }
}
