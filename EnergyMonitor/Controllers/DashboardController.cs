using EnergyMonitor.Data;
using EnergyMonitor.Services;
using EnergyMonitor.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnergyMonitor.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IServiceScopeFactory _scopeFactory;

    public DashboardController(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    [HttpGet("fulldata/{deviceId}")]
    public async Task<IActionResult> GetFullData(string deviceId)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var device = await db.Devices.AsNoTracking().FirstOrDefaultAsync(d => d.DeviceId == deviceId);
        if (device is null)
            return Ok(new DashboardDataDto());

        var chartSnaps = await db.EnergySnapshots
            .Where(s => s.DeviceId == deviceId)
            .OrderByDescending(s => s.Timestamp)
            .Take(300)
            .OrderBy(s => s.Timestamp)
            .ToListAsync();
        var snap = chartSnaps.LastOrDefault();

        var allDevices = await db.Devices.AsNoTracking()
            .Where(d => d.IsActive)
            .OrderBy(d => d.DisplayName)
            .ToListAsync();

        var center = await db.Centers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == device.CenterId);

        var nowUtc = DateTime.UtcNow;
        var iranTz = TimeZoneInfo.FindSystemTimeZoneById("Iran Standard Time");
        var iranNow = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, iranTz);
        var todayStart = TimeZoneInfo.ConvertTimeToUtc(iranNow.Date, iranTz);
        var monthStart = TimeZoneInfo.ConvertTimeToUtc(new DateTime(iranNow.Year, iranNow.Month, 1), iranTz);

        // Consumption from EnergyConsumptions — sanitized SUM
        decimal SanitizeSum(decimal val) => val < 0 || val > 5000 ? 0 : val;

        var todayConsumptions = await db.EnergyConsumptions
            .Where(c => c.DeviceId == deviceId && c.Timestamp >= todayStart)
            .ToListAsync();
        var todayKWh = todayConsumptions.Sum(c => SanitizeSum(c.DeltaA) + SanitizeSum(c.DeltaB) + SanitizeSum(c.DeltaC));

        var monthConsumptions = await db.EnergyConsumptions
            .Where(c => c.DeviceId == deviceId && c.Timestamp >= monthStart)
            .ToListAsync();
        var monthKWh = monthConsumptions.Sum(c => SanitizeSum(c.DeltaA) + SanitizeSum(c.DeltaB) + SanitizeSum(c.DeltaC));

        // Peaks from EnergyConsumptions
        var todayPeak = await db.EnergyConsumptions.Where(e => e.DeviceId == deviceId && e.Timestamp >= todayStart)
            .GroupBy(e => 1)
            .Select(g => new
            {
                peakA = g.Max(e => e.PeakCurrentA),
                peakB = g.Max(e => e.PeakCurrentB),
                peakC = g.Max(e => e.PeakCurrentC),
                peakWA = g.Max(e => e.PeakPowerA),
                peakWB = g.Max(e => e.PeakPowerB),
                peakWC = g.Max(e => e.PeakPowerC)
            }).FirstOrDefaultAsync();

        var devCfg = await db.DeviceConfigs.AsNoTracking().FirstOrDefaultAsync(c => c.DeviceId == deviceId);

        decimal Sanitize(decimal val, decimal max) => val > max || val < 0 ? 0 : val;
        var peakCA = Sanitize(todayPeak?.peakA ?? 0, 100);
        var peakCB = Sanitize(todayPeak?.peakB ?? 0, 100);
        var peakCC = Sanitize(todayPeak?.peakC ?? 0, 100);
        var peakWA = Sanitize(todayPeak?.peakWA ?? 0, 20000);
        var peakWB = Sanitize(todayPeak?.peakWB ?? 0, 20000);
        var peakWC = Sanitize(todayPeak?.peakWC ?? 0, 20000);

        var resolvedThreshold = nowUtc.AddSeconds(-60);
        var allActiveAlarms = center != null
            ? await db.AlarmLogs.Where(a => a.DeviceId == deviceId && !a.IsResolved).ToListAsync()
            : new List<AlarmLog>();
        var recentResolved = center != null
            ? await db.AlarmLogs.Where(a => a.DeviceId == deviceId && a.IsResolved && a.ResolvedAt >= resolvedThreshold)
                .OrderByDescending(a => a.ResolvedAt).Take(5).ToListAsync()
            : new List<AlarmLog>();

        bool connected = snap != null && (DateTime.UtcNow - snap.Timestamp).TotalSeconds < 120;
        string lastUpdateText = "بدون داده";
        if (snap != null)
        {
            var diff = DateTime.UtcNow - snap.Timestamp;
            lastUpdateText = FormatTimeDiff(diff);
        }
        else if (device.LastSeenAt.HasValue)
        {
            var devDiff = DateTime.UtcNow - device.LastSeenAt.Value;
            if (devDiff.TotalMinutes < 2)
            {
                connected = true;
                lastUpdateText = FormatTimeDiff(devDiff);
            }
        }

        return Ok(new DashboardDataDto
        {
            Center = center != null ? new CenterInfoDto
            {
                Id = center.Id, Name = center.Name, Code = center.Code,
                ImageFileName = center.ImageFileName,
                TariffId = center.TariffId
            } : null,
            SelectedDevice = new DeviceInfoDto
            {
                DeviceId = device.DeviceId, DisplayName = device.DisplayName,
                MacAddress = device.MacAddress, Location = device.Location,
                CenterId = device.CenterId, IsActive = device.IsActive,
                LastSeenAt = device.LastSeenAt,
                PhaseAConnected = device.PhaseAConnected,
                PhaseBConnected = device.PhaseBConnected,
                PhaseCConnected = device.PhaseCConnected,
                PhaseCount = device.PhaseCount
            },
            Devices = allDevices.Select(d => new DeviceInfoDto
            {
                DeviceId = d.DeviceId, DisplayName = d.DisplayName,
                MacAddress = d.MacAddress, Location = d.Location,
                CenterId = d.CenterId, IsActive = d.IsActive,
                LastSeenAt = d.LastSeenAt,
                PhaseAConnected = d.PhaseAConnected,
                PhaseBConnected = d.PhaseBConnected,
                PhaseCConnected = d.PhaseCConnected,
                PhaseCount = d.PhaseCount
            }).ToList(),
            LatestSnapshot = snap != null ? new SnapshotDto
            {
                Timestamp = snap.Timestamp,
                VoltageA = snap.VoltageA, CurrentA = snap.CurrentA,
                PowerA = snap.PowerA, PfA = snap.PfA, EnergyA = snap.EnergyA,
                VoltageB = snap.VoltageB, CurrentB = snap.CurrentB,
                PowerB = snap.PowerB, PfB = snap.PfB, EnergyB = snap.EnergyB,
                VoltageC = snap.VoltageC, CurrentC = snap.CurrentC,
                PowerC = snap.PowerC, PfC = snap.PfC, EnergyC = snap.EnergyC,
                Frequency = snap.Frequency, Temperature = snap.Temperature,
                TotalPower = snap.TotalPower
            } : null,
            ChartSnapshots = chartSnaps.Select(s => new SnapshotDto
            {
                Timestamp = s.Timestamp,
                VoltageA = s.VoltageA, CurrentA = s.CurrentA, PowerA = s.PowerA, PfA = s.PfA,
                VoltageB = s.VoltageB, CurrentB = s.CurrentB, PowerB = s.PowerB, PfB = s.PfB,
                VoltageC = s.VoltageC, CurrentC = s.CurrentC, PowerC = s.PowerC, PfC = s.PfC
            }).ToList(),
            ActiveAlarms = allActiveAlarms.Select(a => new AlarmItemDto
            {
                Id = a.Id, Title = a.Title, Message = a.Message,
                Severity = a.Severity, Phase = a.Phase, Value = a.Value,
                OccurredAt = a.OccurredAt, IsResolved = a.IsResolved,
                DeviceId = a.DeviceId
            }).ToList(),
            RecentResolvedAlarms = recentResolved.Select(a => new AlarmItemDto
            {
                Id = a.Id, Title = a.Title, Message = a.Message,
                Severity = a.Severity, Phase = a.Phase, Value = a.Value,
                OccurredAt = a.OccurredAt, IsResolved = a.IsResolved,
                ResolvedAt = a.ResolvedAt, DeviceId = a.DeviceId
            }).ToList(),
            Consumption = new ConsumptionDto
            {
                TodayKWh = todayKWh, MonthKWh = monthKWh,
                PeakCurrentA = peakCA, PeakCurrentB = peakCB, PeakCurrentC = peakCC,
                PeakPowerA = peakWA, PeakPowerB = peakWB, PeakPowerC = peakWC,
                HasBackfill = false, LastBackfillTime = null, LastBackfillKWh = 0
            },
            DeviceConfig = new ConfigDto { AlarmSoundEnabled = devCfg?.AlarmSoundEnabled ?? true },
            Connected = connected,
            LastUpdateText = lastUpdateText
        });
    }

    private static string FormatTimeDiff(TimeSpan diff)
    {
        if (diff.TotalSeconds < 10) return "همین الان";
        var parts = new List<string>();
        int totalSeconds = (int)diff.TotalSeconds;
        int days = totalSeconds / 86400;
        int hours = (totalSeconds % 86400) / 3600;
        int minutes = (totalSeconds % 3600) / 60;
        int seconds = totalSeconds % 60;
        if (days > 0) parts.Add($"{days} روز");
        if (hours > 0) parts.Add($"{hours} ساعت");
        if (minutes > 0) parts.Add($"{minutes} دقیقه");
        if (seconds > 0 && days == 0) parts.Add($"{seconds} ثانیه");
        return parts.Count > 0 ? string.Join(" ", parts) : "همین الان";
    }
}
