using EnergyMonitor.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnergyMonitor.Infrastructure.Data.Configurations;

public class TariffRateConfiguration : IEntityTypeConfiguration<TariffRate>
{
    public void Configure(EntityTypeBuilder<TariffRate> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Phase).HasMaxLength(5);
        builder.Property(x => x.PeriodType).HasMaxLength(20);
        builder.Property(x => x.RatePerKWh).HasColumnType("decimal(14,2)");
        builder.HasIndex(x => x.TariffId);
    }
}
