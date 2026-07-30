using EnergyMonitor.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnergyMonitor.Infrastructure.Data.Configurations;

public class AlarmLogConfiguration : IEntityTypeConfiguration<AlarmLog>
{
    public void Configure(EntityTypeBuilder<AlarmLog> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Severity).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Phase).HasMaxLength(5);
        builder.Property(x => x.Value).HasColumnType("decimal(14,4)");
        builder.Property(x => x.DeviceId).HasMaxLength(450);
        builder.HasIndex(x => x.CenterId);
        builder.HasIndex(x => x.DeviceId);
        builder.HasIndex(x => x.OccurredAt);
    }
}
