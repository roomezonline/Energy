using EnergyMonitor.Application.Interfaces;
using EnergyMonitor.Domain.Entities;
using EnergyMonitor.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EnergyMonitor.Infrastructure.Repositories;

public class TariffRepository : ITariffRepository
{
    private readonly AppDbContext _db;

    public TariffRepository(AppDbContext db) => _db = db;

    public async Task<Tariff?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.Set<Tariff>()
            .Include(t => t.TieredRates)
            .Include(t => t.Overrides)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<List<TieredRate>> GetTieredRatesAsync(Guid tariffId, CancellationToken ct = default)
        => await _db.Set<TieredRate>()
            .Where(r => r.TariffId == tariffId)
            .OrderBy(r => r.PeriodType).ThenBy(r => r.SortOrder)
            .ToListAsync(ct);

    public async Task<List<TariffOverride>> GetOverridesAsync(Guid tariffId, CancellationToken ct = default)
        => await _db.Set<TariffOverride>()
            .Where(o => o.TariffId == tariffId)
            .ToListAsync(ct);

    public async Task<ConsumerType?> GetConsumerTypeAsync(string code, CancellationToken ct = default)
        => await _db.Set<ConsumerType>()
            .FirstOrDefaultAsync(c => c.Code == code, ct);

    public async Task<ConsumerTypeYearlyConfig?> GetConsumerTypeYearlyConfigAsync(string code, int year, CancellationToken ct = default)
        => await _db.Set<ConsumerTypeYearlyConfig>()
            .Include(c => c.TieredRates.OrderBy(t => t.SortOrder))
            .FirstOrDefaultAsync(c => c.ConsumerTypeCode == code && c.Year == year, ct);

    public async Task<YearlyBaseRate?> GetYearlyBaseRateAsync(int year, CancellationToken ct = default)
        => await _db.Set<YearlyBaseRate>()
            .FirstOrDefaultAsync(r => r.Year == year, ct);
}
