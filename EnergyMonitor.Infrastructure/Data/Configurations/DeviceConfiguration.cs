using EnergyMonitor.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnergyMonitor.Infrastructure.Data.Configurations;

public class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DeviceId).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => x.DeviceId).IsUnique();
        builder.Property(x => x.DisplayName).HasMaxLength(200);
        builder.Property(x => x.Location).HasMaxLength(500);
        builder.Property(x => x.ApiKeyHash).HasMaxLength(500);
        builder.HasOne(x => x.DeviceGroup).WithMany(x => x.Devices).HasForeignKey(x => x.DeviceGroupId);
    }
}
