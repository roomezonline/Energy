using EnergyMonitor.Domain.Enums;

namespace EnergyMonitor.Domain.Entities;

public class PhaseReading
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EnergySnapshotId { get; set; }
    public EnergySnapshot EnergySnapshot { get; set; } = null!;
    public Phase Phase { get; set; }
    public decimal Voltage { get; set; }
    public decimal Current { get; set; }
    public decimal Power { get; set; }
    public decimal Pf { get; set; }
    public decimal EnergyKWh { get; set; }
    public bool IsConnected { get; set; } = true;
}
