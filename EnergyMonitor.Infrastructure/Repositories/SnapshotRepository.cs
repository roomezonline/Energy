using EnergyMonitor.Domain.Entities;
using EnergyMonitor.Domain.Interfaces;
using EnergyMonitor.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EnergyMonitor.Infrastructure.Repositories;

public class SnapshotRepository : Repository<EnergySnapshot>, ISnapshotRepository
{
    public SnapshotRepository(AppDbContext db) : base(db) { }

    public async Task<EnergySnapshot?> GetLastBeforeAsync(string deviceId, DateTime timestamp, CancellationToken ct = default)
    {
        return await _db.EnergySnapshots
            .Where(s => s.DeviceId == deviceId && s.Timestamp < timestamp)
            .OrderByDescending(s => s.Timestamp)
            .Include(s => s.PhaseReadings)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<EnergySnapshot?> GetFirstEverAsync(string deviceId, CancellationToken ct = default)
    {
        return await _db.EnergySnapshots
            .Where(s => s.DeviceId == deviceId)
            .OrderBy(s => s.Timestamp)
            .Include(s => s.PhaseReadings)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<List<EnergySnapshot>> GetRangeAsync(string deviceId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        return await _db.EnergySnapshots
            .Where(s => s.DeviceId == deviceId && s.Timestamp >= from && s.Timestamp < to)
            .OrderBy(s => s.Timestamp)
            .Include(s => s.PhaseReadings)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<decimal> GetTotalEnergyDeltaAsync(string deviceId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var first = await _db.EnergySnapshots
            .Where(s => s.DeviceId == deviceId && s.Timestamp >= from)
            .OrderBy(s => s.Timestamp)
            .Select(s => new { s.PhaseReadings })
            .FirstOrDefaultAsync(ct);

        var last = await _db.EnergySnapshots
            .Where(s => s.DeviceId == deviceId && s.Timestamp < to)
            .OrderByDescending(s => s.Timestamp)
            .Select(s => new { s.PhaseReadings })
            .FirstOrDefaultAsync(ct);

        if (first == null || last == null) return 0;

        decimal totalFirst = 0, totalLast = 0;
        foreach (var pr in first.PhaseReadings) totalFirst += pr.EnergyKWh;
        foreach (var pr in last.PhaseReadings) totalLast += pr.EnergyKWh;

        var d = totalLast - totalFirst;
        if (d < -0.001m) d += 4294967m;
        return d < 0 ? 0 : d;
    }
}
