using EnergyMonitor.Domain.Entities;
using EnergyMonitor.Domain.Interfaces;
using EnergyMonitor.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EnergyMonitor.Infrastructure.Repositories;

public class CenterRepository : Repository<Center>, ICenterRepository
{
    public CenterRepository(AppDbContext db) : base(db) { }

    public async Task<Center?> GetByCodeAsync(string code, CancellationToken ct = default)
        => await _db.Centers.FirstOrDefaultAsync(c => c.Code == code, ct);

    public async Task<List<Center>> GetActiveCentersAsync(CancellationToken ct = default)
        => await _db.Centers.Where(c => c.IsActive).ToListAsync(ct);
}
