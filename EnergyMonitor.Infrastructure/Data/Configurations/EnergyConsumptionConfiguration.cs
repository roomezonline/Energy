using EnergyMonitor.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnergyMonitor.Infrastructure.Data.Configurations;

public class EnergyConsumptionConfiguration : IEntityTypeConfiguration<EnergyConsumption>
{
    public void Configure(EntityTypeBuilder<EnergyConsumption> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DeviceId).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => new { x.DeviceId, x.Timestamp }).IsUnique();
        builder.Property(x => x.DeltaA).HasColumnType("decimal(14,4)");
        builder.Property(x => x.DeltaB).HasColumnType("decimal(14,4)");
        builder.Property(x => x.DeltaC).HasColumnType("decimal(14,4)");
        builder.Property(x => x.PeakCurrentA).HasColumnType("decimal(10,3)");
        builder.Property(x => x.PeakCurrentB).HasColumnType("decimal(10,3)");
        builder.Property(x => x.PeakCurrentC).HasColumnType("decimal(10,3)");
        builder.Property(x => x.PeakPowerA).HasColumnType("decimal(12,2)");
        builder.Property(x => x.PeakPowerB).HasColumnType("decimal(12,2)");
        builder.Property(x => x.PeakPowerC).HasColumnType("decimal(12,2)");
    }
}
