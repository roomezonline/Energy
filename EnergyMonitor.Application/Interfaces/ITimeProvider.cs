namespace EnergyMonitor.Application.Interfaces;

public interface ITimeProvider
{
    DateTime UtcNow { get; }
    DateTime IranNow { get; }
}
