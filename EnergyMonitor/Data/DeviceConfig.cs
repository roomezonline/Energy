namespace EnergyMonitor.Data;

public class DeviceConfig
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DeviceId { get; set; } = string.Empty;

    public decimal OverVoltageThreshold { get; set; } = 253.0M;
    public decimal UnderVoltageThreshold { get; set; } = 207.0M;
    public decimal OverCurrentThreshold { get; set; } = 20.0M;
    public decimal PhaseImbalanceThreshold { get; set; } = 15.0M;
    public decimal LowPFThreshold { get; set; } = 0.80M;
    public decimal FreqMinThreshold { get; set; } = 49.5M;
    public decimal FreqMaxThreshold { get; set; } = 50.5M;
    public decimal HighPowerThreshold { get; set; } = 5000.0M;
    public decimal TemperatureThreshold { get; set; } = 40.0M;

    public bool AlarmSoundEnabled { get; set; } = true;

    public int PublishIntervalMs { get; set; } = 15000;
    public bool IsSavingEnabled { get; set; } = true;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
