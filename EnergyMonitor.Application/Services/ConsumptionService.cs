using EnergyMonitor.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace EnergyMonitor.Application.Services;

public class ConsumptionService : IConsumptionService
{
    private readonly IEnergySnapshotReader _reader;
    private readonly ILogger<ConsumptionService> _log;

    private static readonly TimeZoneInfo IranTz =
        TimeZoneInfo.FindSystemTimeZoneById("Iran Standard Time");

    public ConsumptionService(IEnergySnapshotReader reader, ILogger<ConsumptionService> log)
    {
        _reader = reader;
        _log = log;
    }

    public async Task<List<DailyConsumption>> GetDailyAsync(string deviceId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        var records = await _reader.GetRangeAsync(deviceId, fromUtc, toUtc, ct);

        var daily = new Dictionary<DateTime, (decimal A, decimal B, decimal C)>();
        var pc = new System.Globalization.PersianCalendar();

        foreach (var r in records)
        {
            var iranDate = TimeZoneInfo.ConvertTimeFromUtc(r.Timestamp, IranTz).Date;
            if (!daily.TryGetValue(iranDate, out var cur))
                cur = (0, 0, 0);
            daily[iranDate] = (cur.A + (decimal)r.DeltaA, cur.B + (decimal)r.DeltaB, cur.C + (decimal)r.DeltaC);
        }

        return daily
            .OrderBy(d => d.Key)
            .Select(d => new DailyConsumption
            {
                DateUtc = d.Key,
                PersianDate = d.Key.ToString("yyyy/MM/dd"),
                KWhA = (decimal)Math.Round((double)d.Value.A, 4),
                KWhB = (decimal)Math.Round((double)d.Value.B, 4),
                KWhC = (decimal)Math.Round((double)d.Value.C, 4),
                TotalKWh = (decimal)Math.Round((double)(d.Value.A + d.Value.B + d.Value.C), 4)
            })
            .ToList();
    }

    public async Task<List<MonthlyConsumption>> GetMonthlyAsync(string deviceId, int fromYear, int fromMonth, int toYear, int toMonth, CancellationToken ct = default)
    {
        var pc = new System.Globalization.PersianCalendar();
        var fromDt = pc.ToDateTime(fromYear, fromMonth, 1, 0, 0, 0, 0);
        var toDt = pc.ToDateTime(toYear, toMonth, 1, 0, 0, 0, 0).AddMonths(1);

        var fromUtc = TimeZoneInfo.ConvertTimeToUtc(fromDt, IranTz);
        var toUtc = TimeZoneInfo.ConvertTimeToUtc(toDt, IranTz);

        var records = await _reader.GetRangeAsync(deviceId, fromUtc, toUtc, ct);

        var monthly = new Dictionary<(int Year, int Month), decimal>();

        foreach (var r in records)
        {
            var total = (decimal)(r.DeltaA + r.DeltaB + r.DeltaC);
            if (total <= 0) continue;
            var iran = TimeZoneInfo.ConvertTimeFromUtc(r.Timestamp, IranTz);
            var key = (pc.GetYear(iran), pc.GetMonth(iran));
            monthly.TryGetValue(key, out var cur);
            monthly[key] = cur + total;
        }

        return monthly
            .OrderBy(m => m.Key.Year)
            .ThenBy(m => m.Key.Month)
            .Select(m => new MonthlyConsumption
            {
                Year = m.Key.Year,
                Month = m.Key.Month,
                PersianMonth = $"{m.Key.Year:D4}/{m.Key.Month:D2}",
                TotalKWh = (decimal)Math.Round((double)m.Value, 4)
            })
            .ToList();
    }
}
