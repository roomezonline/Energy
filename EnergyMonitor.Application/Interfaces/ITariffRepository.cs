using EnergyMonitor.Domain.Entities;

namespace EnergyMonitor.Application.Interfaces;

public interface ITariffRepository
{
    Task<Tariff?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<TieredRate>> GetTieredRatesAsync(Guid tariffId, CancellationToken ct = default);
    Task<List<TariffOverride>> GetOverridesAsync(Guid tariffId, CancellationToken ct = default);

    // New: consumer type and yearly config
    Task<ConsumerType?> GetConsumerTypeAsync(string code, CancellationToken ct = default);
    Task<ConsumerTypeYearlyConfig?> GetConsumerTypeYearlyConfigAsync(string code, int year, CancellationToken ct = default);
    Task<YearlyBaseRate?> GetYearlyBaseRateAsync(int year, CancellationToken ct = default);
    Task<YearlyBaseRate?> GetLatestYearlyBaseRateAsync(int upToYear, CancellationToken ct = default);
}
