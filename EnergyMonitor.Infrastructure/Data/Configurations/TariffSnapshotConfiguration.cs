using EnergyMonitor.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnergyMonitor.Infrastructure.Data.Configurations;

public class TariffSnapshotConfiguration : IEntityTypeConfiguration<TariffSnapshot>
{
    public void Configure(EntityTypeBuilder<TariffSnapshot> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TariffName).HasMaxLength(200);
        builder.Property(x => x.OffPeakRate).HasColumnType("decimal(14,2)");
        builder.Property(x => x.MidPeakRate).HasColumnType("decimal(14,2)");
        builder.Property(x => x.PeakRate).HasColumnType("decimal(14,2)");
        builder.Property(x => x.MonthlyFixedFee).HasColumnType("decimal(14,2)");
        builder.Property(x => x.ReactivePenaltyThreshold).HasColumnType("decimal(5,3)");
        builder.Property(x => x.ReactivePenaltyMultiplier).HasColumnType("decimal(5,2)");

        // New snapshot fields
        builder.Property(x => x.ConsumerTypeCode).HasMaxLength(20);
        builder.Property(x => x.ConsumerTypeName).HasMaxLength(200);
        builder.Property(x => x.BaseEcaRate).HasColumnType("decimal(14,2)");
        builder.Property(x => x.EcaCoefficient).HasColumnType("decimal(10,4)");
        builder.Property(x => x.TouOffPeakMultiplier).HasColumnType("decimal(5,3)");
        builder.Property(x => x.TouMidPeakMultiplier).HasColumnType("decimal(5,3)");
        builder.Property(x => x.TouPeakMultiplier).HasColumnType("decimal(5,3)");
        builder.Property(x => x.EffectiveOffPeakRate).HasColumnType("decimal(14,2)");
        builder.Property(x => x.EffectiveMidPeakRate).HasColumnType("decimal(14,2)");
        builder.Property(x => x.EffectivePeakRate).HasColumnType("decimal(14,2)");
        builder.Property(x => x.PeakPenaltyAmount).HasColumnType("decimal(18,2)");
        builder.Property(x => x.OffPeakDiscountAmount).HasColumnType("decimal(18,2)");
        builder.Property(x => x.Article16Amount).HasColumnType("decimal(18,2)");
        builder.Property(x => x.DemandCost).HasColumnType("decimal(18,2)");
        builder.Property(x => x.TotalPenaltyBeforeTax).HasColumnType("decimal(18,2)");
    }
}
