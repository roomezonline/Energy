using EnergyMonitor.Data;
using Microsoft.EntityFrameworkCore;

namespace EnergyMonitor.Services;

public class AlarmCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AlarmCleanupService> _log;

    public AlarmCleanupService(IServiceScopeFactory scopeFactory, ILogger<AlarmCleanupService> log)
    {
        _scopeFactory = scopeFactory;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Cleanup();
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Alarm cleanup failed");
            }
            await Task.Delay(15000, stoppingToken);
        }
    }

    private async Task Cleanup()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;

        var activeAlarms = await db.AlarmLogs
            .Where(a => !a.IsResolved && a.OccurredAt < now.AddSeconds(-30))
            .ToListAsync();

        if (activeAlarms.Count == 0) return;

        var byCenter = activeAlarms.GroupBy(a => a.CenterId);

        foreach (var group in byCenter)
        {
            var centerId = group.Key;
            var centerAlarms = group.ToList();

            var latestSnaps = await db.EnergySnapshots
                .Where(s => s.Timestamp >= now.AddHours(-1))
                .Where(s => db.Devices
                    .Where(d => d.CenterId == centerId)
                    .Select(d => d.DeviceId)
                    .Contains(s.DeviceId))
                .GroupBy(s => s.DeviceId)
                .Select(g => g.OrderByDescending(s => s.Timestamp).First())
                .ToListAsync();

            var latestSnap = latestSnaps.OrderByDescending(s => s.Timestamp).FirstOrDefault();
            if (latestSnap == null) continue;

            var device = await db.Devices
                .Where(d => d.CenterId == centerId)
                .OrderByDescending(d => d.LastSeenAt)
                .FirstOrDefaultAsync();

            if (device == null) continue;

            var cfg = await db.DeviceConfigs
                .FirstOrDefaultAsync(c => c.DeviceId == device.DeviceId);

            if (cfg == null) continue;

            foreach (var alarm in centerAlarms)
            {
                if (device.PhaseCount == 1 && alarm.Phase is "فاز B" or "فاز C")
                {
                    alarm.IsResolved = true;
                    alarm.ResolvedAt = now;
                    _log.LogInformation("Cleanup resolved alarm for non-existent phase: {Title}/{Phase}", alarm.Title, alarm.Phase);
                    continue;
                }
                bool stillTriggered = CheckCondition(alarm, latestSnap, device, cfg);
                if (!stillTriggered)
                {
                    alarm.IsResolved = true;
                    alarm.ResolvedAt = now;
                    _log.LogInformation("Cleanup resolved alarm: {Title}/{Phase}", alarm.Title, alarm.Phase);
                }
            }
        }

        await db.SaveChangesAsync();
    }

    private static bool CheckCondition(AlarmLog alarm, EnergySnapshot snap, DeviceInfo device, DeviceConfig cfg)
    {
        var (v, i, p, pf, connected) = alarm.Phase switch
        {
            "فاز A" => (snap.VoltageA, snap.CurrentA, snap.PowerA, snap.PfA, device.PhaseAConnected),
            "فاز B" => (snap.VoltageB, snap.CurrentB, snap.PowerB, snap.PfB, device.PhaseBConnected),
            "فاز C" => (snap.VoltageC, snap.CurrentC, snap.PowerC, snap.PfC, device.PhaseCConnected),
            _ => (0m, 0m, 0m, 1m, true)
        };

        return alarm.Title switch
        {
            "قطع فاز" => !connected,
            "ولتاژ بالا" => v > cfg.OverVoltageThreshold,
            "ولتاژ پایین" => v > 0 && v < cfg.UnderVoltageThreshold,
            "جریان بالا" => i > cfg.OverCurrentThreshold,
            "توان بالا" => p > cfg.HighPowerThreshold,
            "ضریب توان پایین" => pf > 0 && pf < cfg.LowPFThreshold,
            _ => false
        };
    }
}
