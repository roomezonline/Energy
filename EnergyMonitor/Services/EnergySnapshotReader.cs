using EnergyMonitor.Application.Interfaces;
using EnergyMonitor.Data;
using Microsoft.EntityFrameworkCore;

namespace EnergyMonitor.Services;

public class EnergySnapshotReader : IEnergySnapshotReader
{
    private readonly AppDbContext _db;

    public EnergySnapshotReader(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<EnergySnapshotRowDto>> GetRangeAsync(string deviceId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        return await _db.EnergyConsumptions
            .Where(c => c.DeviceId == deviceId && c.Timestamp >= fromUtc && c.Timestamp < toUtc)
            .OrderBy(c => c.Timestamp)
            .Select(c => new EnergySnapshotRowDto
            {
                Timestamp = c.Timestamp,
                DeltaA = c.DeltaA,
                DeltaB = c.DeltaB,
                DeltaC = c.DeltaC,
                PeakPowerA = c.PeakPowerA,
                PeakPowerB = c.PeakPowerB,
                PeakPowerC = c.PeakPowerC
            })
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<(decimal pfA, decimal pfB, decimal pfC)?> GetAveragePfAsync(string deviceId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        var avg = await _db.EnergySnapshots
            .Where(s => s.DeviceId == deviceId && s.Timestamp >= fromUtc && s.Timestamp < toUtc
                && s.PfA > 0 && s.PfB > 0 && s.PfC > 0)
            .GroupBy(_ => 1)
            .Select(g => new { pfA = g.Average(s => s.PfA), pfB = g.Average(s => s.PfB), pfC = g.Average(s => s.PfC) })
            .FirstOrDefaultAsync(ct);
        if (avg is null) return null;
        return ((decimal)avg.pfA, (decimal)avg.pfB, (decimal)avg.pfC);
    }
}
