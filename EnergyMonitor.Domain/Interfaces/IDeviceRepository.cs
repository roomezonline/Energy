using EnergyMonitor.Domain.Entities;

namespace EnergyMonitor.Domain.Interfaces;

public interface IDeviceRepository : IRepository<Device>
{
    Task<Device?> GetByDeviceIdAsync(string deviceId, CancellationToken ct = default);
    Task<List<Device>> GetByCenterAsync(Guid centerId, CancellationToken ct = default);
}
