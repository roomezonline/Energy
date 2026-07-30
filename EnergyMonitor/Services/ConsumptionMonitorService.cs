using EnergyMonitor.Data;
using Microsoft.EntityFrameworkCore;

namespace EnergyMonitor.Services;

public class ConsumptionMonitorService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ConsumptionMonitorService> _log;

    public ConsumptionMonitorService(IServiceScopeFactory scopeFactory, ILogger<ConsumptionMonitorService> log)
    {
        _scopeFactory = scopeFactory;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await CheckLimits(); }
            catch (Exception ex) { _log.LogWarning(ex, "ConsumptionMonitor failed"); }
            await Task.Delay(600000, stoppingToken); // every 10 minutes
        }
    }

    private async Task CheckLimits()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;
        var iranTz = TimeZoneInfo.FindSystemTimeZoneById("Iran Standard Time");
        var iranNow = TimeZoneInfo.ConvertTimeFromUtc(now, iranTz);

        var limits = await db.EnergyLimits.Where(l => l.IsActive).ToListAsync();
        if (limits.Count == 0) return;

        var centerIds = limits.Select(l => l.CenterId).Distinct();

        foreach (var centerId in centerIds)
        {
            var centerLimits = limits.Where(l => l.CenterId == centerId).ToList();
            var device = await db.Devices.Where(d => d.CenterId == centerId && d.IsActive)
                .OrderBy(d => d.DisplayName).FirstOrDefaultAsync();
            if (device is null) continue;

            var deviceId = device.DeviceId;
            var latestSnap = await db.EnergySnapshots
                .Where(s => s.DeviceId == deviceId)
                .OrderByDescending(s => s.Timestamp)
                .FirstOrDefaultAsync();
            if (latestSnap is null) continue;

            foreach (var limit in centerLimits)
            {
                var consumption = await GetConsumption(db, deviceId, limit, iranTz, iranNow);
                await EvaluateLimit(db, centerId, limit, consumption, now);
            }
        }

        await db.SaveChangesAsync();
    }

    private async Task<decimal> GetConsumption(AppDbContext db, string deviceId, EnergyLimit limit,
        TimeZoneInfo iranTz, DateTime iranNow)
    {
        DateTime fromUtc;
        switch (limit.PeriodType)
        {
            case "Daily":
                var todayStart = TimeZoneInfo.ConvertTimeToUtc(iranNow.Date, iranTz);
                fromUtc = todayStart;
                break;
            case "Weekly":
                var weekStart = iranNow.Date.AddDays(-(int)iranNow.DayOfWeek);
                fromUtc = TimeZoneInfo.ConvertTimeToUtc(weekStart, iranTz);
                break;
            case "Monthly":
                var monthStart = new DateTime(iranNow.Year, iranNow.Month, 1);
                fromUtc = TimeZoneInfo.ConvertTimeToUtc(monthStart, iranTz);
                break;
            case "Bimonthly":
                var biMonthStart = new DateTime(iranNow.Year, (iranNow.Month - 1) / 2 * 2 + 1, 1);
                fromUtc = TimeZoneInfo.ConvertTimeToUtc(biMonthStart, iranTz);
                break;
            default:
                return 0;
        }

        // Filter at DB level (efficient SUM, NaN/Infinity naturally excluded by range check)
        return await db.EnergyConsumptions
            .Where(c => c.DeviceId == deviceId && c.Timestamp >= fromUtc
                && c.DeltaA >= 0 && c.DeltaA <= 5000
                && c.DeltaB >= 0 && c.DeltaB <= 5000
                && c.DeltaC >= 0 && c.DeltaC <= 5000)
            .SumAsync(c => c.DeltaA + c.DeltaB + c.DeltaC);
    }

    private async Task EvaluateLimit(AppDbContext db, Guid centerId, EnergyLimit limit, decimal consumption, DateTime now)
    {
        if (limit.MaxValue <= 0) return;

        var usagePercent = consumption / limit.MaxValue * 100;
        var alertThreshold = limit.AlertThresholdPercent;

        // Check for existing unresolved alarms for this limit
        var existingAlarm = await db.AlarmLogs
            .Where(a => a.CenterId == centerId && !a.IsResolved
                && a.Title == "مصرف انرژی" && a.Phase == limit.PeriodType)
            .FirstOrDefaultAsync();

        if (usagePercent >= 100)
        {
            if (existingAlarm is null)
            {
                db.AlarmLogs.Add(new AlarmLog
                {
                    CenterId = centerId,
                    Severity = "Critical",
                    Title = "مصرف انرژی",
                    Message = $"مصرف {limit.PeriodType} از حد مجاز فراتر رفت: {consumption:F1} از {limit.MaxValue} kWh",
                    Phase = limit.PeriodType,
                    Value = consumption,
                    OccurredAt = now
                });
                _log.LogWarning("Consumption limit exceeded: {CenterId} {Period} {Consumption:F1}/{MaxValue}",
                    centerId, limit.PeriodType, consumption, limit.MaxValue);
            }
            else if (Math.Abs(existingAlarm.Value.GetValueOrDefault() - consumption) > 0.1m)
            {
                existingAlarm.Value = consumption;
                existingAlarm.Message = $"مصرف {limit.PeriodType} از حد مجاز فراتر رفت: {consumption:F1} از {limit.MaxValue} kWh";
            }
        }
        else if (usagePercent >= alertThreshold)
        {
            if (existingAlarm is null)
            {
                db.AlarmLogs.Add(new AlarmLog
                {
                    CenterId = centerId,
                    Severity = "Warning",
                    Title = "مصرف انرژی",
                    Phase = limit.PeriodType,
                    Message = $"مصرف {limit.PeriodType} به {usagePercent:F0}% حد مجاز رسید: {consumption:F1} از {limit.MaxValue} kWh",
                    Value = consumption,
                    OccurredAt = now
                });
                _log.LogInformation("Consumption approaching limit: {CenterId} {Period} {UsagePercent:F0}%",
                    centerId, limit.PeriodType, usagePercent);
            }
        }
        else if (existingAlarm is not null && usagePercent < 90)
        {
            existingAlarm.IsResolved = true;
            existingAlarm.ResolvedAt = now;
            _log.LogInformation("Consumption alarm resolved: {CenterId} {Period} {UsagePercent:F0}%",
                centerId, limit.PeriodType, usagePercent);
        }
    }
}
