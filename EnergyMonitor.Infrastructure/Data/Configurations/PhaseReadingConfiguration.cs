using EnergyMonitor.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnergyMonitor.Infrastructure.Data.Configurations;

public class PhaseReadingConfiguration : IEntityTypeConfiguration<PhaseReading>
{
    public void Configure(EntityTypeBuilder<PhaseReading> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Phase).HasConversion<string>().HasMaxLength(1);
        builder.Property(x => x.Voltage).HasColumnType("decimal(8,2)");
        builder.Property(x => x.Current).HasColumnType("decimal(10,3)");
        builder.Property(x => x.Power).HasColumnType("decimal(12,2)");
        builder.Property(x => x.Pf).HasColumnType("decimal(5,3)");
        builder.Property(x => x.EnergyKWh).HasColumnType("decimal(14,4)");
        builder.HasIndex(x => new { x.EnergySnapshotId, x.Phase }).IsUnique();
    }
}
