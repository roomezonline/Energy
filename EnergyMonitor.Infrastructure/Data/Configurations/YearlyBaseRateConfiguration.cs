using EnergyMonitor.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnergyMonitor.Infrastructure.Data.Configurations;

public class YearlyBaseRateConfiguration : IEntityTypeConfiguration<YearlyBaseRate>
{
    public void Configure(EntityTypeBuilder<YearlyBaseRate> builder)
    {
        builder.HasKey(x => x.Year);
        builder.Property(x => x.BaseRatePerKwh).HasColumnType("decimal(14,2)").IsRequired();
        builder.Property(x => x.SupplyCostPerKwh).HasColumnType("decimal(14,2)").IsRequired();
        builder.Property(x => x.Currency).HasMaxLength(10).IsRequired();
        builder.Property(x => x.SourceDocument).HasMaxLength(500);
    }
}
