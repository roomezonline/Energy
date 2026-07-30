using EnergyMonitor.Domain.Entities;

namespace EnergyMonitor.Application.Interfaces;

public interface ITokenGenerator
{
    string GenerateToken(User user);
}
