using EnergyMonitor.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnergyMonitor.Infrastructure.Data.Configurations;

public class EnergyLimitConfiguration : IEntityTypeConfiguration<EnergyLimit>
{
    public void Configure(EntityTypeBuilder<EnergyLimit> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.LimitType).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.PeriodType).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.MaxValue).HasColumnType("decimal(14,4)");
        builder.Property(x => x.AlertThresholdPercent).HasColumnType("decimal(5,2)");
        builder.HasIndex(x => x.CenterId);
    }
}
