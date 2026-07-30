using EnergyMonitor.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnergyMonitor.Infrastructure.Data.Configurations;

public class TariffOverrideConfiguration : IEntityTypeConfiguration<TariffOverride>
{
    public void Configure(EntityTypeBuilder<TariffOverride> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FieldName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.OverrideValue).HasColumnType("decimal(18,2)");
        builder.HasOne(x => x.Tariff).WithMany(x => x.Overrides).HasForeignKey(x => x.TariffId);
    }
}
