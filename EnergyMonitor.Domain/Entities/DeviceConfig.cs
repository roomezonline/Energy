namespace EnergyMonitor.Domain.Entities;

public class DeviceConfig
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DeviceId { get; set; } = string.Empty;
    public decimal OverVoltageThreshold { get; set; } = 253;
    public decimal UnderVoltageThreshold { get; set; } = 207;
    public decimal OverCurrentThreshold { get; set; } = 20;
    public decimal PhaseImbalanceThreshold { get; set; } = 15;
    public decimal LowPFThreshold { get; set; } = 0.80m;
    public decimal FreqMinThreshold { get; set; } = 49.5m;
    public decimal FreqMaxThreshold { get; set; } = 50.5m;
    public decimal HighPowerThreshold { get; set; } = 5000;
    public int PublishIntervalMs { get; set; } = 15000;
    public bool IsSavingEnabled { get; set; } = true;
    public bool AlarmSoundEnabled { get; set; } = true;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
