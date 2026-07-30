using EnergyMonitor.Domain.Entities;
using EnergyMonitor.Domain.Enums;
using EnergyMonitor.Infrastructure.Data.Configurations;
using Microsoft.EntityFrameworkCore;

namespace EnergyMonitor.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> opts) : base(opts) { }

    public DbSet<Center> Centers => Set<Center>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<DeviceConfig> DeviceConfigs => Set<DeviceConfig>();
    public DbSet<EnergySnapshot> EnergySnapshots => Set<EnergySnapshot>();
    public DbSet<PhaseReading> PhaseReadings => Set<PhaseReading>();
    public DbSet<EnergyConsumption> EnergyConsumptions => Set<EnergyConsumption>();
    public DbSet<EnergyLimit> EnergyLimits => Set<EnergyLimit>();
    public DbSet<AlarmLog> AlarmLogs => Set<AlarmLog>();
    public DbSet<Tariff> Tariffs => Set<Tariff>();
    public DbSet<TariffRate> TariffRates => Set<TariffRate>();
    public DbSet<TariffSnapshot> TariffSnapshots => Set<TariffSnapshot>();
    public DbSet<TariffOverride> TariffOverrides => Set<TariffOverride>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceDetail> InvoiceDetails => Set<InvoiceDetail>();
    public DbSet<CalibrationLog> CalibrationLogs => Set<CalibrationLog>();
    public DbSet<NewsArticle> NewsArticles => Set<NewsArticle>();
    public DbSet<SliderImage> SliderImages => Set<SliderImage>();
    public DbSet<Region> Regions => Set<Region>();
    public DbSet<Province> Provinces => Set<Province>();
    public DbSet<City> Cities => Set<City>();
    public DbSet<DeviceGroup> DeviceGroups => Set<DeviceGroup>();
    public DbSet<UserCenter> UserCenters => Set<UserCenter>();
    public DbSet<TieredRate> TieredRates => Set<TieredRate>();

    // New entities for Iran tariff system
    public DbSet<ConsumerType> ConsumerTypes => Set<ConsumerType>();
    public DbSet<YearlyBaseRate> YearlyBaseRates => Set<YearlyBaseRate>();
    public DbSet<ConsumerTypeYearlyConfig> ConsumerTypeYearlyConfigs => Set<ConsumerTypeYearlyConfig>();
    public DbSet<ConsumerTypeTieredRate> ConsumerTypeTieredRates => Set<ConsumerTypeTieredRate>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.ApplyConfiguration(new CenterConfiguration());
        model.ApplyConfiguration(new UserConfiguration());
        model.ApplyConfiguration(new DeviceConfiguration());
        model.ApplyConfiguration(new DeviceConfigConfiguration());
        model.ApplyConfiguration(new EnergySnapshotConfiguration());
        model.ApplyConfiguration(new PhaseReadingConfiguration());
        model.ApplyConfiguration(new EnergyConsumptionConfiguration());
        model.ApplyConfiguration(new EnergyLimitConfiguration());
        model.ApplyConfiguration(new AlarmLogConfiguration());
        model.ApplyConfiguration(new TariffConfiguration());
        model.ApplyConfiguration(new TariffRateConfiguration());
        model.ApplyConfiguration(new TariffSnapshotConfiguration());
        model.ApplyConfiguration(new TariffOverrideConfiguration());
        model.ApplyConfiguration(new InvoiceConfiguration());
        model.ApplyConfiguration(new InvoiceDetailConfiguration());
        model.ApplyConfiguration(new CalibrationLogConfiguration());
        model.ApplyConfiguration(new NewsArticleConfiguration());
        model.ApplyConfiguration(new SliderImageConfiguration());
        model.ApplyConfiguration(new RegionConfiguration());
        model.ApplyConfiguration(new ProvinceConfiguration());
        model.ApplyConfiguration(new CityConfiguration());
        model.ApplyConfiguration(new DeviceGroupConfiguration());
        model.ApplyConfiguration(new TieredRateConfiguration());
        model.ApplyConfiguration(new UserCenterConfiguration());

        // New entity configurations
        model.ApplyConfiguration(new ConsumerTypeConfiguration());
        model.ApplyConfiguration(new YearlyBaseRateConfiguration());
        model.ApplyConfiguration(new ConsumerTypeYearlyConfigConfiguration());
        model.ApplyConfiguration(new ConsumerTypeTieredRateConfiguration());

        // Default precision for all decimal properties not explicitly configured
        foreach (var entity in model.Model.GetEntityTypes())
        {
            foreach (var prop in entity.GetProperties())
            {
                if ((prop.ClrType == typeof(decimal) || prop.ClrType == typeof(decimal?)) && prop.GetPrecision() is null)
                {
                    prop.SetPrecision(18);
                    prop.SetScale(4);
                }
            }
        }
    }
}
