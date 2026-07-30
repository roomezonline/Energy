namespace EnergyMonitor.Application.Interfaces;

public interface IEnergySnapshotReader
{
    Task<IReadOnlyList<EnergySnapshotRowDto>> GetRangeAsync(string deviceId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
    Task<(decimal pfA, decimal pfB, decimal pfC)?> GetAveragePfAsync(string deviceId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
}

public class EnergySnapshotRowDto
{
    public DateTime Timestamp { get; set; }
    public decimal DeltaA { get; set; }
    public decimal DeltaB { get; set; }
    public decimal DeltaC { get; set; }
    public decimal PeakPowerA { get; set; }
    public decimal PeakPowerB { get; set; }
    public decimal PeakPowerC { get; set; }
    public decimal TotalPower => PeakPowerA + PeakPowerB + PeakPowerC;
}
