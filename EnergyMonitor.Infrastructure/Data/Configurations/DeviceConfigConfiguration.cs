using EnergyMonitor.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnergyMonitor.Infrastructure.Data.Configurations;

public class DeviceConfigConfiguration : IEntityTypeConfiguration<DeviceConfig>
{
    public void Configure(EntityTypeBuilder<DeviceConfig> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DeviceId).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => x.DeviceId).IsUnique();
        builder.Property(x => x.OverVoltageThreshold).HasColumnType("decimal(10,2)");
        builder.Property(x => x.UnderVoltageThreshold).HasColumnType("decimal(10,2)");
        builder.Property(x => x.OverCurrentThreshold).HasColumnType("decimal(10,2)");
        builder.Property(x => x.PhaseImbalanceThreshold).HasColumnType("decimal(10,2)");
        builder.Property(x => x.LowPFThreshold).HasColumnType("decimal(5,3)");
        builder.Property(x => x.FreqMinThreshold).HasColumnType("decimal(5,2)");
        builder.Property(x => x.FreqMaxThreshold).HasColumnType("decimal(5,2)");
        builder.Property(x => x.HighPowerThreshold).HasColumnType("decimal(12,2)");
    }
}
