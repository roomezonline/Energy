using EnergyMonitor.Domain.Entities;

namespace EnergyMonitor.Domain.Interfaces;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default);
    Task<List<User>> GetByCenterAsync(Guid centerId, CancellationToken ct = default);
}
