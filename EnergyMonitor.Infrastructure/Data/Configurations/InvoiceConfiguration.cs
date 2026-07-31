using EnergyMonitor.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnergyMonitor.Infrastructure.Data.Configurations;

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("InvoiceRecords");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.InvoiceNumber).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => x.InvoiceNumber).IsUnique();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.TotalKWh).HasColumnType("decimal(14,4)");
        builder.Property(x => x.EnergyCost).HasColumnType("decimal(18,2)");
        builder.Property(x => x.MonthlyFixedFeeTotal).HasColumnType("decimal(18,2)");
        builder.Property(x => x.ReactivePenalty).HasColumnType("decimal(18,2)");
        builder.Property(x => x.SubTotal).HasColumnType("decimal(18,2)");
        builder.Property(x => x.TaxAmount).HasColumnType("decimal(18,2)");
        builder.Property(x => x.GrandTotal).HasColumnType("decimal(18,2)");

        // New invoice cost components
        builder.Property(x => x.PeakPenalty).HasColumnType("decimal(18,2)");
        builder.Property(x => x.OffPeakDiscount).HasColumnType("decimal(18,2)");
        builder.Property(x => x.Article16Cost).HasColumnType("decimal(18,2)");
        builder.Property(x => x.DemandCost).HasColumnType("decimal(18,2)");
        builder.Property(x => x.TollAmount).HasColumnType("decimal(18,2)");

        builder.HasOne(x => x.TariffSnapshot).WithOne(x => x.Invoice).HasForeignKey<TariffSnapshot>(x => x.InvoiceId);
        builder.HasMany(x => x.Details).WithOne(x => x.Invoice).HasForeignKey(x => x.InvoiceId);
        builder.HasIndex(x => x.CenterId);
        builder.HasIndex(x => x.IdempotencyKey).IsUnique().HasFilter("[IdempotencyKey] IS NOT NULL");
    }
}
