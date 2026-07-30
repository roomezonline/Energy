using EnergyMonitor.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnergyMonitor.Infrastructure.Data.Configurations;

public class ConsumerTypeConfiguration : IEntityTypeConfiguration<ConsumerType>
{
    public void Configure(EntityTypeBuilder<ConsumerType> builder)
    {
        builder.HasKey(x => x.Code);
        builder.Property(x => x.Code).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Category).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.BillingModel).HasConversion<string>().HasMaxLength(20);
        builder.HasMany(x => x.YearlyConfigs).WithOne(x => x.ConsumerType).HasForeignKey(x => x.ConsumerTypeCode);
    }
}
