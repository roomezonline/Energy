using EnergyMonitor.Application.Interfaces;

namespace EnergyMonitor.Infrastructure.Services;

public class IranTimeProvider : ITimeProvider
{
    private static readonly TimeZoneInfo IranTz =
        TimeZoneInfo.FindSystemTimeZoneById("Iran Standard Time");

    public DateTime UtcNow => DateTime.UtcNow;

    public DateTime IranNow =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, IranTz);
}
