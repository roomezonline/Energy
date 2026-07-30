using EnergyMonitor.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnergyMonitor.Infrastructure.Data.Configurations;

public class CenterConfiguration : IEntityTypeConfiguration<Center>
{
    public void Configure(EntityTypeBuilder<Center> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasOne(x => x.City).WithMany(x => x.Centers).HasForeignKey(x => x.CityId);
        builder.HasMany(x => x.Devices).WithOne(x => x.Center).HasForeignKey(x => x.CenterId);
        builder.HasMany(x => x.Users).WithOne(x => x.Center).HasForeignKey(x => x.CenterId);
        builder.HasMany(x => x.AlarmLogs).WithOne(x => x.Center).HasForeignKey(x => x.CenterId);
        builder.HasMany(x => x.EnergyLimits).WithOne(x => x.Center).HasForeignKey(x => x.CenterId);
        builder.HasMany(x => x.Invoices).WithOne(x => x.Center).HasForeignKey(x => x.CenterId);
        builder.HasMany(x => x.DeviceGroups).WithOne(x => x.Center).HasForeignKey(x => x.CenterId);
        builder.HasOne(x => x.Tariff).WithMany().HasForeignKey(x => x.TariffId).OnDelete(DeleteBehavior.SetNull);

        // New fields
        builder.Property(x => x.ConsumerTypeCode).HasMaxLength(20);
        builder.Property(x => x.ContractCapacityMW).HasColumnType("decimal(10,4)");
    }
}
