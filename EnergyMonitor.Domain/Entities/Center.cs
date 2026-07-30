namespace EnergyMonitor.Domain.Entities;

public class Center
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public Guid? CityId { get; set; }
    public City? City { get; set; }
    public bool IsActive { get; set; } = true;
    public string? ImageFileName { get; set; }
    public Guid? TariffId { get; set; }
    public Tariff? Tariff { get; set; }

    // Consumer type override at center level
    public string? ConsumerTypeCode { get; set; }
    public decimal? ContractCapacityMW { get; set; }
    public decimal? ConsumptionPatternKWh { get; set; }

    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<UserCenter> UserCenters { get; set; } = new List<UserCenter>();
    public ICollection<Device> Devices { get; set; } = new List<Device>();
    public ICollection<DeviceGroup> DeviceGroups { get; set; } = new List<DeviceGroup>();
    public ICollection<AlarmLog> AlarmLogs { get; set; } = new List<AlarmLog>();
    public ICollection<EnergyLimit> EnergyLimits { get; set; } = new List<EnergyLimit>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
