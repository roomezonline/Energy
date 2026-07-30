namespace EnergyMonitor.Data;

public class DeviceInfo
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DeviceId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? MacAddress { get; set; }
    public string? ApiKeyHash { get; set; }
    public Guid CenterId { get; set; }
    public Guid? DeviceGroupId { get; set; }
    public DeviceGroup? DeviceGroup { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Location { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public bool PhaseAConnected { get; set; } = true;
    public bool PhaseBConnected { get; set; } = true;
    public bool PhaseCConnected { get; set; } = true;
    public int PhaseCount { get; set; } = 3;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
