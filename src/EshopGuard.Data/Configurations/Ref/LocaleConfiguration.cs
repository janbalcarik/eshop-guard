using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Ref;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EshopGuard.Data.Configurations.Ref;

internal sealed class LocaleConfiguration : IEntityTypeConfiguration<Locale>
{
    public void Configure(EntityTypeBuilder<Locale> builder)
    {
        builder.ToTable("locales", Schemas.Ref);
        builder.HasKey(x => x.Code);
        builder.HasOne<Locale>().WithMany().HasForeignKey(x => x.FallbackCode).HasPrincipalKey(x => x.Code).OnDelete(DeleteBehavior.Restrict);
    }
}
