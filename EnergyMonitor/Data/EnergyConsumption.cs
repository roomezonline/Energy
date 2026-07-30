namespace EnergyMonitor.Data;

public class EnergyConsumption
{
    public int Id { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string PersianTimestamp { get; set; } = "";

    public decimal DeltaA { get; set; }
    public decimal PeakCurrentA { get; set; }
    public decimal PeakPowerA { get; set; }

    public decimal DeltaB { get; set; }
    public decimal PeakCurrentB { get; set; }
    public decimal PeakPowerB { get; set; }

    public decimal DeltaC { get; set; }
    public decimal PeakCurrentC { get; set; }
    public decimal PeakPowerC { get; set; }
}
