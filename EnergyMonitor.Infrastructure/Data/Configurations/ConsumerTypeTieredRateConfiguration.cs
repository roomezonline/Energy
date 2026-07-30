using EnergyMonitor.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnergyMonitor.Infrastructure.Data.Configurations;

public class ConsumerTypeTieredRateConfiguration : IEntityTypeConfiguration<ConsumerTypeTieredRate>
{
    public void Configure(EntityTypeBuilder<ConsumerTypeTieredRate> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TierFrom).HasColumnType("decimal(18,2)");
        builder.Property(x => x.TierTo).HasColumnType("decimal(18,2)");
        builder.Property(x => x.Coefficient).HasColumnType("decimal(10,4)");
        builder.Property(x => x.RatePerKwh).HasColumnType("decimal(18,4)");
        builder.HasIndex(x => new { x.ConsumerTypeYearlyConfigId, x.SortOrder });
    }
}
