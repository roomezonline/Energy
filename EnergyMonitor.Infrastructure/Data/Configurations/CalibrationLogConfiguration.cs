using EnergyMonitor.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnergyMonitor.Infrastructure.Data.Configurations;

public class CalibrationLogConfiguration : IEntityTypeConfiguration<CalibrationLog>
{
    public void Configure(EntityTypeBuilder<CalibrationLog> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DeviceId).HasMaxLength(100).IsRequired();
        builder.Property(x => x.FieldName).HasMaxLength(50).IsRequired();
        builder.Property(x => x.OldValue).HasColumnType("decimal(14,6)");
        builder.Property(x => x.NewValue).HasColumnType("decimal(14,6)");
        builder.Property(x => x.ChangedBy).HasMaxLength(100);
        builder.HasIndex(x => new { x.DeviceId, x.ChangedAt });
    }
}
