using EnergyMonitor.Domain.Entities;

namespace EnergyMonitor.Domain.Interfaces;

public interface ICalibrationLogRepository : IRepository<CalibrationLog>
{
    Task<List<CalibrationLog>> GetByDeviceAsync(string deviceId, CancellationToken ct = default);
}
