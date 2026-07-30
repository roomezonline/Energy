namespace EnergyMonitor.Data;

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
    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<DeviceGroup> DeviceGroups { get; set; } = new List<DeviceGroup>();
}
