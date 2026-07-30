namespace EnergyMonitor.Application.Interfaces;

public class DailyConsumption
{
    public string PersianDate { get; set; } = string.Empty;
    public DateTime DateUtc { get; set; }
    public decimal KWhA { get; set; }
    public decimal KWhB { get; set; }
    public decimal KWhC { get; set; }
    public decimal TotalKWh { get; set; }
}

public class MonthlyConsumption
{
    public string PersianMonth { get; set; } = string.Empty;
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal TotalKWh { get; set; }
}

public interface IConsumptionService
{
    Task<List<DailyConsumption>> GetDailyAsync(string deviceId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
    Task<List<MonthlyConsumption>> GetMonthlyAsync(string deviceId, int fromYear, int fromMonth, int toYear, int toMonth, CancellationToken ct = default);
}
