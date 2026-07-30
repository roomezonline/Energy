using System.Linq.Expressions;
using EnergyMonitor.Domain.Interfaces;
using EnergyMonitor.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EnergyMonitor.Infrastructure.Repositories;

public class Repository<T> : IRepository<T> where T : class
{
    protected readonly AppDbContext _db;
    protected readonly DbSet<T> _set;

    public Repository(AppDbContext db)
    {
        _db = db;
        _set = db.Set<T>();
    }

    public virtual async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _set.FindAsync([id], ct);

    public virtual async Task<List<T>> GetAllAsync(CancellationToken ct = default)
        => await _set.ToListAsync(ct);

    public virtual async Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        => await _set.Where(predicate).ToListAsync(ct);

    public virtual async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        => await _set.FirstOrDefaultAsync(predicate, ct);

    public virtual async Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        => await _set.AnyAsync(predicate, ct);

    public virtual async Task<int> CountAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        => await _set.CountAsync(predicate, ct);

    public virtual void Add(T entity) => _set.Add(entity);
    public virtual void AddRange(IEnumerable<T> entities) => _set.AddRange(entities);
    public virtual void Update(T entity) => _set.Update(entity);
    public virtual void Remove(T entity) => _set.Remove(entity);
    public virtual void RemoveRange(IEnumerable<T> entities) => _set.RemoveRange(entities);

    public virtual async Task<int> SaveChangesAsync(CancellationToken ct = default)
        => await _db.SaveChangesAsync(ct);
}
