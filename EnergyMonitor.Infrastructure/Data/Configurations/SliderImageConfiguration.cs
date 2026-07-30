using EnergyMonitor.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnergyMonitor.Infrastructure.Data.Configurations;

public class SliderImageConfiguration : IEntityTypeConfiguration<SliderImage>
{
    public void Configure(EntityTypeBuilder<SliderImage> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(200);
        builder.HasIndex(x => x.SortOrder);
    }
}
