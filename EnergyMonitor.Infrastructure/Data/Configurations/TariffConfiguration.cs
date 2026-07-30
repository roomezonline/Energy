using EnergyMonitor.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EnergyMonitor.Infrastructure.Data.Configurations;

public class TariffConfiguration : IEntityTypeConfiguration<Tariff>
{
    public void Configure(EntityTypeBuilder<Tariff> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.OffPeakRate).HasColumnType("decimal(18,4)");
        builder.Property(x => x.MidPeakRate).HasColumnType("decimal(18,4)");
        builder.Property(x => x.PeakRate).HasColumnType("decimal(18,4)");
        builder.Property(x => x.MonthlyFixedFee).HasColumnType("decimal(18,2)");
        builder.Property(x => x.ReactivePenaltyThreshold).HasColumnType("decimal(5,3)");
        builder.Property(x => x.ReactiveBonusThreshold).HasColumnType("decimal(5,3)");
        builder.Property(x => x.ReactivePenaltyMultiplier).HasColumnType("decimal(5,2)");
        builder.Property(x => x.DemandRate).HasColumnType("decimal(18,2)");

        // New fields
        builder.Property(x => x.RateDerivationMode).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.ConsumerTypeCode).HasMaxLength(20);

        builder.HasMany(x => x.Rates).WithOne(x => x.Tariff).HasForeignKey(x => x.TariffId);
        builder.HasMany(x => x.Overrides).WithOne(x => x.Tariff).HasForeignKey(x => x.TariffId);
    }
}
