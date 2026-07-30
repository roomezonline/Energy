using EnergyMonitor.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EnergyMonitor.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> opts) : base(opts) { }

    public DbSet<Center> Centers => Set<Center>();
    public DbSet<User> Users => Set<User>();
    public DbSet<AlarmLog> AlarmLogs => Set<AlarmLog>();
    public DbSet<EnergySnapshot> EnergySnapshots => Set<EnergySnapshot>();
    public DbSet<DeviceConfig> DeviceConfigs => Set<DeviceConfig>();
    public DbSet<DeviceInfo> Devices => Set<DeviceInfo>();
    public DbSet<EnergyConsumption> EnergyConsumptions => Set<EnergyConsumption>();
    public DbSet<EnergyLimit> EnergyLimits => Set<EnergyLimit>();
    public DbSet<NewsArticle> NewsArticles => Set<NewsArticle>();
    public DbSet<SliderImage> SliderImages => Set<SliderImage>();
    public DbSet<Tariff> Tariffs => Set<Tariff>();
    public DbSet<TariffRate> TariffRates => Set<TariffRate>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceDetail> InvoiceDetails => Set<InvoiceDetail>();
    public DbSet<Region> Regions => Set<Region>();
    public DbSet<Province> Provinces => Set<Province>();
    public DbSet<City> Cities => Set<City>();
    public DbSet<DeviceGroup> DeviceGroups => Set<DeviceGroup>();
    public DbSet<TieredRate> TieredRates => Set<TieredRate>();
    public DbSet<UserCenter> UserCenters => Set<UserCenter>();

    // New entities for Iran tariff system
    public DbSet<ConsumerType> ConsumerTypes => Set<ConsumerType>();
    public DbSet<YearlyBaseRate> YearlyBaseRates => Set<YearlyBaseRate>();
    public DbSet<ConsumerTypeYearlyConfig> ConsumerTypeYearlyConfigs => Set<ConsumerTypeYearlyConfig>();
    public DbSet<ConsumerTypeTieredRate> ConsumerTypeTieredRates => Set<ConsumerTypeTieredRate>();
    public DbSet<TariffOverride> TariffOverrides => Set<TariffOverride>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Region>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Code).IsUnique();
        });
        model.Entity<Province>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Code).IsUnique();
            e.HasOne(x => x.Region).WithMany(x => x.Provinces).HasForeignKey(x => x.RegionId);
        });
        model.Entity<City>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Code).IsUnique();
            e.HasOne(x => x.Province).WithMany(x => x.Cities).HasForeignKey(x => x.ProvinceId);
        });
        model.Entity<DeviceGroup>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Center).WithMany(x => x.DeviceGroups).HasForeignKey(x => x.CenterId);
        });
        model.Entity<Center>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.City).WithMany(x => x.Centers).HasForeignKey(x => x.CityId);
        });
        model.Entity<User>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Username).IsUnique();
            e.HasOne(x => x.Center).WithMany(x => x.Users).HasForeignKey(x => x.CenterId);
            e.HasOne(x => x.Region).WithMany().HasForeignKey(x => x.RegionId);
        });
        model.Entity<AlarmLog>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.HasOne(x => x.Center).WithMany().HasForeignKey(x => x.CenterId);
        });
        model.Entity<EnergySnapshot>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.HasIndex(x => new { x.DeviceId, x.Timestamp }).IsUnique();
        });
        model.Entity<EnergyConsumption>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
        });
        model.Entity<Invoice>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.HasOne(x => x.Center).WithMany().HasForeignKey(x => x.CenterId);
            e.HasOne(x => x.Tariff).WithMany().HasForeignKey(x => x.TariffId);
            e.HasMany(x => x.Details).WithOne(x => x.Invoice).HasForeignKey(x => x.InvoiceId);
        });
        model.Entity<InvoiceDetail>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
        });
        model.Entity<DeviceConfig>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.DeviceId).IsUnique();
        });
        model.Entity<DeviceInfo>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.DeviceId).IsUnique();
            e.HasOne<Center>().WithMany().HasForeignKey(x => x.CenterId);
            e.HasOne(x => x.DeviceGroup).WithMany(x => x.Devices).HasForeignKey(x => x.DeviceGroupId);
        });
        model.Entity<EnergyLimit>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Center).WithMany().HasForeignKey(x => x.CenterId);
        });
        model.Entity<NewsArticle>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.SortOrder);
        });
        model.Entity<SliderImage>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.SortOrder);
        });
        model.Entity<Tariff>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.RateDerivationMode).HasConversion<string>().HasMaxLength(20);
        });
        model.Entity<TariffRate>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Tariff).WithMany(x => x.Rates).HasForeignKey(x => x.TariffId);
        });
        model.Entity<UserCenter>(e =>
        {
            e.HasKey(x => new { x.UserId, x.CenterId });
            e.HasOne(x => x.User).WithMany(x => x.UserCenters).HasForeignKey(x => x.UserId);
            e.HasOne(x => x.Center).WithMany(x => x.UserCenters).HasForeignKey(x => x.CenterId);
        });

        // New entity configurations (minimal — keep in sync with Infrastructure configs)
        model.Entity<ConsumerType>(e =>
        {
            e.HasKey(x => x.Code);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Category).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.BillingModel).HasConversion<string>().HasMaxLength(20);
        });
        model.Entity<YearlyBaseRate>(e =>
        {
            e.HasKey(x => x.Year);
            e.Property(x => x.BaseRatePerKwh).HasColumnType("decimal(14,2)").IsRequired();
        });
        model.Entity<ConsumerTypeYearlyConfig>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.ConsumerTypeCode).HasMaxLength(20).IsRequired();
        });
        model.Entity<ConsumerTypeTieredRate>(e =>
        {
            e.HasKey(x => x.Id);
        });
        model.Entity<TariffOverride>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.FieldName).HasMaxLength(100);
        });

        // Default precision for all decimal properties not explicitly configured
        foreach (var entity in model.Model.GetEntityTypes())
        {
            foreach (var prop in entity.GetProperties())
            {
                if ((prop.ClrType == typeof(decimal) || prop.ClrType == typeof(decimal?))
                    && prop.GetPrecision() is null && prop.GetColumnType() is null)
                {
                    prop.SetPrecision(18);
                    prop.SetScale(4);
                }
            }
        }
    }
}
