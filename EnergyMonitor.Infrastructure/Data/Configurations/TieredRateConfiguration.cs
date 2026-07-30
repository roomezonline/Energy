using EnergyMonitor.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnergyMonitor.Infrastructure.Data.Configurations;

public class TieredRateConfiguration : IEntityTypeConfiguration<TieredRate>
{
    public void Configure(EntityTypeBuilder<TieredRate> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PeriodType).HasMaxLength(20).IsRequired();
        builder.Property(x => x.TierFrom).HasColumnType("decimal(18,2)");
        builder.Property(x => x.TierTo).HasColumnType("decimal(18,2)");
        builder.Property(x => x.RatePerKWh).HasColumnType("decimal(18,4)");
        builder.HasOne(x => x.Tariff).WithMany(x => x.TieredRates).HasForeignKey(x => x.TariffId);
        builder.HasIndex(x => new { x.TariffId, x.PeriodType, x.SortOrder });
    }
}
