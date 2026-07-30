using EnergyMonitor.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnergyMonitor.Infrastructure.Data.Configurations;

public class ConsumerTypeYearlyConfigConfiguration : IEntityTypeConfiguration<ConsumerTypeYearlyConfig>
{
    public void Configure(EntityTypeBuilder<ConsumerTypeYearlyConfig> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ConsumerTypeCode).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Year).IsRequired();

        // Consumption pattern for tiered billing
        builder.Property(x => x.ConsumptionPatternKWh).HasColumnType("decimal(10,2)");

        // ECA
        builder.Property(x => x.EcaCoefficient).HasColumnType("decimal(10,4)").IsRequired();

        // TOU multipliers
        builder.Property(x => x.TouOffPeakMultiplier).HasColumnType("decimal(5,3)");
        builder.Property(x => x.TouMidPeakMultiplier).HasColumnType("decimal(5,3)");
        builder.Property(x => x.TouPeakMultiplier).HasColumnType("decimal(5,3)");

        // Time slots
        builder.Property(x => x.SummerOffPeakStart).HasMaxLength(5);
        builder.Property(x => x.SummerOffPeakEnd).HasMaxLength(5);
        builder.Property(x => x.SummerMidPeakStart).HasMaxLength(5);
        builder.Property(x => x.SummerMidPeakEnd).HasMaxLength(5);
        builder.Property(x => x.SummerPeakStart).HasMaxLength(5);
        builder.Property(x => x.SummerPeakEnd).HasMaxLength(5);
        builder.Property(x => x.WinterOffPeakStart).HasMaxLength(5);
        builder.Property(x => x.WinterOffPeakEnd).HasMaxLength(5);
        builder.Property(x => x.WinterMidPeakStart).HasMaxLength(5);
        builder.Property(x => x.WinterMidPeakEnd).HasMaxLength(5);
        builder.Property(x => x.WinterPeakStart).HasMaxLength(5);
        builder.Property(x => x.WinterPeakEnd).HasMaxLength(5);

        // Financial fields
        builder.Property(x => x.MonthlyFixedFee).HasColumnType("decimal(14,2)");
        builder.Property(x => x.ReactivePenaltyThreshold).HasColumnType("decimal(5,3)");
        builder.Property(x => x.ReactiveBonusThreshold).HasColumnType("decimal(5,3)");
        builder.Property(x => x.ReactivePenaltyMultiplier).HasColumnType("decimal(5,2)");
        builder.Property(x => x.DemandRate).HasColumnType("decimal(14,2)");

        // Article 16
        builder.Property(x => x.Article16Percent).HasColumnType("decimal(5,2)");
        builder.Property(x => x.Article16GreenEnergyRate).HasColumnType("decimal(14,2)");

        // Penalty/discount coefficients
        builder.Property(x => x.PeakPenaltyCoefficient).HasColumnType("decimal(10,4)");
        builder.Property(x => x.PeakPenaltyNormalCoefficient).HasColumnType("decimal(10,4)");
        builder.Property(x => x.OffPeakDiscountCoefficient).HasColumnType("decimal(10,4)");
        builder.Property(x => x.OffPeakDiscountTwoRateCoefficient).HasColumnType("decimal(10,4)");
        builder.Property(x => x.OverloadViolationMultiplier).HasColumnType("decimal(5,2)");
        builder.Property(x => x.TaxPercent).HasColumnType("decimal(5,2)");
        builder.Property(x => x.TollPercent).HasColumnType("decimal(5,2)");

        builder.HasOne(x => x.ConsumerType).WithMany(x => x.YearlyConfigs).HasForeignKey(x => x.ConsumerTypeCode);
        builder.HasMany(x => x.TieredRates).WithOne(x => x.ConsumerTypeYearlyConfig).HasForeignKey(x => x.ConsumerTypeYearlyConfigId);
        builder.HasIndex(x => new { x.ConsumerTypeCode, x.Year });
    }
}
