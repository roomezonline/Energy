using EnergyMonitor.Domain.Entities;
using EnergyMonitor.Domain.Interfaces;
using EnergyMonitor.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EnergyMonitor.Infrastructure.Repositories;

public class InvoiceRepository : Repository<Invoice>, IInvoiceRepository
{
    public InvoiceRepository(AppDbContext db) : base(db) { }

    public async Task<Invoice?> GetByIdempotencyKeyAsync(Guid key, CancellationToken ct = default)
    {
        return await _db.Invoices
            .Include(i => i.Details)
            .Include(i => i.TariffSnapshot)
            .FirstOrDefaultAsync(i => i.IdempotencyKey == key, ct);
    }

    public async Task<List<Invoice>> GetByCenterAsync(Guid centerId, CancellationToken ct = default)
    {
        return await _db.Invoices
            .Where(i => i.CenterId == centerId)
            .OrderByDescending(i => i.CreatedAt)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<string> GetNextInvoiceNumberAsync(CancellationToken ct = default)
    {
        var count = await _db.Invoices.CountAsync(ct);
        var now = DateTime.UtcNow;
        return $"INV-{now:yyyyMMdd}-{(count + 1):D4}";
    }
}
