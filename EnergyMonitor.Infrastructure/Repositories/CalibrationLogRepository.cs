using EnergyMonitor.Domain.Entities;
using EnergyMonitor.Domain.Interfaces;
using EnergyMonitor.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EnergyMonitor.Infrastructure.Repositories;

public class CalibrationLogRepository : Repository<CalibrationLog>, ICalibrationLogRepository
{
    public CalibrationLogRepository(AppDbContext db) : base(db) { }

    public async Task<List<CalibrationLog>> GetByDeviceAsync(string deviceId, CancellationToken ct = default)
        => await _db.CalibrationLogs
            .Where(c => c.DeviceId == deviceId)
            .OrderByDescending(c => c.ChangedAt)
            .ToListAsync(ct);
}
