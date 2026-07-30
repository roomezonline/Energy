using EnergyMonitor.Domain.Entities;
using EnergyMonitor.Domain.Interfaces;
using EnergyMonitor.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EnergyMonitor.Infrastructure.Repositories;

public class UserRepository : Repository<User>, IUserRepository
{
    public UserRepository(AppDbContext db) : base(db) { }

    public override async Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.Users.Include(u => u.UserCenters).FirstOrDefaultAsync(u => u.Id == id, ct);

    public override async Task<List<User>> GetAllAsync(CancellationToken ct = default)
        => await _db.Users.Include(u => u.UserCenters).ToListAsync(ct);

    public async Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default)
        => await _db.Users.Include(u => u.UserCenters).FirstOrDefaultAsync(u => u.Username == username, ct);

    public async Task<List<User>> GetByCenterAsync(Guid centerId, CancellationToken ct = default)
        => await _db.Users.Where(u => u.CenterId == centerId || u.UserCenters.Any(uc => uc.CenterId == centerId)).ToListAsync(ct);
}
