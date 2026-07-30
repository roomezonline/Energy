namespace EnergyMonitor.Data;

public class DeviceGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public Guid CenterId { get; set; }
    public Center? Center { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<DeviceInfo> Devices { get; set; } = new List<DeviceInfo>();
}
