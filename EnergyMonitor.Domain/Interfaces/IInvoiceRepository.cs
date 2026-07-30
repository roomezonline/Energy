using EnergyMonitor.Domain.Entities;

namespace EnergyMonitor.Domain.Interfaces;

public interface IInvoiceRepository : IRepository<Invoice>
{
    Task<Invoice?> GetByIdempotencyKeyAsync(Guid key, CancellationToken ct = default);
    Task<List<Invoice>> GetByCenterAsync(Guid centerId, CancellationToken ct = default);
    Task<string> GetNextInvoiceNumberAsync(CancellationToken ct = default);
}
