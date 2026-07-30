using EnergyMonitor.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnergyMonitor.Infrastructure.Data.Configurations;

public class InvoiceDetailConfiguration : IEntityTypeConfiguration<InvoiceDetail>
{
    public void Configure(EntityTypeBuilder<InvoiceDetail> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Phase).HasMaxLength(5);
        builder.Property(x => x.PeriodType).HasMaxLength(20);
        builder.Property(x => x.KWh).HasColumnType("decimal(14,4)");
        builder.Property(x => x.RatePerKWh).HasColumnType("decimal(14,2)");
        builder.Property(x => x.Amount).HasColumnType("decimal(18,2)");
        builder.Property(x => x.Penalty).HasColumnType("decimal(18,2)");
        builder.HasIndex(x => x.InvoiceId);
    }
}
