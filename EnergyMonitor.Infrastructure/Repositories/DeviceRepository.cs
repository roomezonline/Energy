using EnergyMonitor.Domain.Entities;
using EnergyMonitor.Domain.Interfaces;
using EnergyMonitor.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EnergyMonitor.Infrastructure.Repositories;

public class DeviceRepository : Repository<Device>, IDeviceRepository
{
    public DeviceRepository(AppDbContext db) : base(db) { }

    public async Task<Device?> GetByDeviceIdAsync(string deviceId, CancellationToken ct = default)
        => await _db.Devices.FirstOrDefaultAsync(d => d.DeviceId == deviceId, ct);

    public async Task<List<Device>> GetByCenterAsync(Guid centerId, CancellationToken ct = default)
        => await _db.Devices.Where(d => d.CenterId == centerId).ToListAsync(ct);
}
