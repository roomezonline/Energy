namespace EnergyMonitor.Domain.Entities;

public class EnergyConsumption
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DeviceId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public decimal DeltaA { get; set; }
    public decimal PeakCurrentA { get; set; }
    public decimal PeakPowerA { get; set; }
    public decimal DeltaB { get; set; }
    public decimal PeakCurrentB { get; set; }
    public decimal PeakPowerB { get; set; }
    public decimal DeltaC { get; set; }
    public decimal PeakCurrentC { get; set; }
    public decimal PeakPowerC { get; set; }
    public bool IsBackfill { get; set; }
}
