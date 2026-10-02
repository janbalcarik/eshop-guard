using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Billing;
using EshopGuard.Data.Entities.Shops;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EshopGuard.Data.Configurations.Billing;

internal sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("invoices", Schemas.Billing);
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.TenantId, x.Id });
        builder.HasEnum(x => x.Kind);
        builder.Property(x => x.AmountNet).HasPrecision(12, 2);
        builder.Property(x => x.VatAmount).HasPrecision(12, 2);
        builder.Property(x => x.AmountGross).HasPrecision(12, 2);
        builder.Property(x => x.Currency).HasColumnType("character(3)");
        builder.HasEnum(x => x.EinvoiceStatus);
        builder.HasEnum(x => x.Status);
        builder.HasEnum(x => x.SourceKind);
        builder.HasEnum(x => x.EmailStatus).HasDefaultValueSql("'not_required'");
        builder.HasIndex(x => x.SourceKey).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.IssuedAt }).IsDescending(false, true);
        builder.HasTenantForeignKey<Shop>(nameof(Invoice.ShopId));
        builder.HasTenantForeignKey<Payment>(nameof(Invoice.PaymentId));
        builder.HasTenantForeignKey<Invoice>(nameof(Invoice.CreditNoteFor));
    }
}
