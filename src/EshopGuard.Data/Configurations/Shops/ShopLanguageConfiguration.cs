using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Checks;
using EshopGuard.Data.Entities.Shops;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EshopGuard.Data.Configurations.Shops;

internal sealed class ShopLanguageConfiguration : IEntityTypeConfiguration<ShopLanguage>
{
    public void Configure(EntityTypeBuilder<ShopLanguage> builder)
    {
        builder.ToTable("shop_languages", Schemas.Shops);
        builder.HasKey(x => new { x.ShopId, x.Language });
        builder.HasEnum(x => x.SwitchMethod);
        builder.HasEnum(x => x.Source);
        builder.HasEnum(x => x.Status);
        builder.HasTenantForeignKey<Shop>(nameof(ShopLanguage.ShopId));
        builder.HasTenantForeignKey<Run>(nameof(ShopLanguage.SampleRunId));
    }
}
