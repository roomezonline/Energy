namespace EnergyMonitor.Domain.Entities;

public class EnergySnapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DeviceId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public decimal Frequency { get; set; }
    public decimal TotalPower { get; set; }
    public bool OverVoltage { get; set; }
    public bool OverCurrent { get; set; }
    public bool PhaseImbalance { get; set; }
    public decimal TotalEnergyKWh { get; set; }
    public ICollection<PhaseReading> PhaseReadings { get; set; } = new List<PhaseReading>();
}
