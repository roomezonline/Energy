using EnergyMonitor.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnergyMonitor.Infrastructure.Data.Configurations;

public class EnergySnapshotConfiguration : IEntityTypeConfiguration<EnergySnapshot>
{
    public void Configure(EntityTypeBuilder<EnergySnapshot> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DeviceId).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => new { x.DeviceId, x.Timestamp }).IsUnique();
        builder.Property(x => x.Frequency).HasColumnType("decimal(6,3)");
        builder.Property(x => x.TotalPower).HasColumnType("decimal(12,2)");
        builder.Property(x => x.TotalEnergyKWh).HasColumnType("decimal(14,4)");
        builder.HasMany(x => x.PhaseReadings).WithOne(x => x.EnergySnapshot)
            .HasForeignKey(x => x.EnergySnapshotId).OnDelete(DeleteBehavior.Cascade);
    }
}
