using EnergyMonitor.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnergyMonitor.Infrastructure.Data.Configurations;

public class UserCenterConfiguration : IEntityTypeConfiguration<UserCenter>
{
    public void Configure(EntityTypeBuilder<UserCenter> builder)
    {
        builder.HasKey(x => new { x.UserId, x.CenterId });
        builder.HasOne(x => x.User).WithMany(x => x.UserCenters).HasForeignKey(x => x.UserId);
        builder.HasOne(x => x.Center).WithMany(x => x.UserCenters).HasForeignKey(x => x.CenterId);
    }
}
