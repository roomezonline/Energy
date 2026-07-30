using EnergyMonitor.Domain.Entities;

namespace EnergyMonitor.Domain.Interfaces;

public interface ISnapshotRepository : IRepository<EnergySnapshot>
{
    Task<EnergySnapshot?> GetLastBeforeAsync(string deviceId, DateTime timestamp, CancellationToken ct = default);
    Task<EnergySnapshot?> GetFirstEverAsync(string deviceId, CancellationToken ct = default);
    Task<List<EnergySnapshot>> GetRangeAsync(string deviceId, DateTime from, DateTime to, CancellationToken ct = default);
    Task<decimal> GetTotalEnergyDeltaAsync(string deviceId, DateTime from, DateTime to, CancellationToken ct = default);
}
