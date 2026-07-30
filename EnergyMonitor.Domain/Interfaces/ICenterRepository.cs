using EnergyMonitor.Domain.Entities;

namespace EnergyMonitor.Domain.Interfaces;

public interface ICenterRepository : IRepository<Center>
{
    Task<Center?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<List<Center>> GetActiveCentersAsync(CancellationToken ct = default);
}
