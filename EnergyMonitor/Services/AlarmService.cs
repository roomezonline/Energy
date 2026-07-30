using EnergyMonitor.Controllers;
using EnergyMonitor.Data;
using Microsoft.EntityFrameworkCore;

namespace EnergyMonitor.Services;

public class AlarmService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AlarmService> _log;

    public AlarmService(IServiceScopeFactory scopeFactory, ILogger<AlarmService> log)
    {
        _scopeFactory = scopeFactory;
        _log = log;
    }

    public async Task ProcessAlarms(DeviceConfig cfg, EnergyDataDto data, Guid centerId, string? deviceId = null, int phaseCount = 3)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;

        var activeAlarms = await db.AlarmLogs
            .Where(a => a.CenterId == centerId && !a.IsResolved)
            .ToListAsync();

        var triggered = EvaluateConditions(cfg, data, phaseCount);

        foreach (var alarm in activeAlarms)
        {
            if (!triggered.Contains((alarm.Title, alarm.Phase ?? "")))
            {
                if (alarm.Title == "قطع فاز")
                {
                    bool recentReconnect = await db.AlarmLogs.AnyAsync(a =>
                        a.CenterId == centerId && a.Title == "اتصال فاز"
                        && a.Phase == alarm.Phase && a.OccurredAt >= now.AddMinutes(-5));
                    if (!recentReconnect)
                    {
                        db.AlarmLogs.Add(new AlarmLog
                        {
                            CenterId = centerId,
                            DeviceId = deviceId,
                            Severity = "Info",
                            Title = "اتصال فاز",
                            Message = $"{alarm.Phase} مجدداً وصل شد",
                            Phase = alarm.Phase,
                            OccurredAt = now,
                            IsResolved = true,
                            ResolvedAt = now
                        });
                    }
                }

                alarm.IsResolved = true;
                alarm.ResolvedAt = now;
                _log.LogInformation("Resolved alarm: {Title}/{Phase}", alarm.Title, alarm.Phase);
            }
        }

        foreach (var (title, phase) in triggered)
        {
            if (!activeAlarms.Any(a => a.Title == title && a.Phase == phase))
            {
                var msg = GetMessage(title, phase);
                db.AlarmLogs.Add(new AlarmLog
                {
                    CenterId = centerId,
                    DeviceId = deviceId,
                    Severity = GetSeverity(title),
                    Title = title,
                    Message = msg,
                    Phase = phase,
                    OccurredAt = now
                });
                _log.LogWarning("New alarm: {Title} - {Msg}", title, msg);
            }
        }

        await db.SaveChangesAsync();
    }

    private HashSet<(string title, string phase)> EvaluateConditions(DeviceConfig cfg, EnergyDataDto data, int phaseCount = 3)
    {
        var set = new HashSet<(string, string)>();

        void Check(string phaseName, decimal? v, decimal? i, decimal? p, decimal? pf, bool connected)
        {
            if (!connected) set.Add(("قطع فاز", phaseName));
            if (v > cfg.OverVoltageThreshold) set.Add(("ولتاژ بالا", phaseName));
            if (v > 0 && v < cfg.UnderVoltageThreshold) set.Add(("ولتاژ پایین", phaseName));
            if (i > cfg.OverCurrentThreshold) set.Add(("جریان بالا", phaseName));
            if (p > cfg.HighPowerThreshold) set.Add(("توان بالا", phaseName));
            if (pf > 0 && pf < cfg.LowPFThreshold) set.Add(("ضریب توان پایین", phaseName));
        }

        var phases = new[] { ("فاز A", data.PhaseA), ("فاز B", data.PhaseB), ("فاز C", data.PhaseC) };
        for (int i = 0; i < phaseCount && i < phases.Length; i++)
        {
            var (name, phase) = phases[i];
            Check(name, (decimal?)phase?.Voltage, (decimal?)phase?.Current, (decimal?)phase?.Power, (decimal?)phase?.Pf, phase?.Connected ?? true);
        }

        var voltages = new decimal?[]
        {
            (data.PhaseA?.Connected ?? true) ? (decimal?)data.PhaseA?.Voltage : null,
            (data.PhaseB?.Connected ?? true) ? (decimal?)data.PhaseB?.Voltage : null,
            (data.PhaseC?.Connected ?? true) ? (decimal?)data.PhaseC?.Voltage : null
        }.Where(v => v.HasValue && v > 0).ToArray();

        if (voltages.Length >= 2)
        {
            var diff = voltages.Max()!.Value - voltages.Min()!.Value;
            if (diff > cfg.PhaseImbalanceThreshold)
                set.Add(("عدم تعادل ولتاژ", "عمومی"));
        }

        if (data.Frequency > 0 && ((decimal)data.Frequency < cfg.FreqMinThreshold || (decimal)data.Frequency > cfg.FreqMaxThreshold))
            set.Add(("فرکانس نامعتبر", "عمومی"));

        return set;
    }

    private static string GetMessage(string title, string phase)
    {
        return title switch
        {
            "قطع فاز" => $"{phase} قطع شده است",
            "ولتاژ بالا" => $"ولتاژ {phase} از حد مجاز فراتر رفت",
            "ولتاژ پایین" => $"ولتاژ {phase} کمتر از حد مجاز است",
            "جریان بالا" => $"جریان {phase} از حد مجاز فراتر رفت",
            "توان بالا" => $"توان {phase} از حد مجاز فراتر رفت",
            "ضریب توان پایین" => $"ضریب توان {phase} کمتر از حد مجاز است",
            "عدم تعادل ولتاژ" => "اختلاف ولتاژ فازها از حد مجاز فراتر رفت",
            "فرکانس نامعتبر" => "فرکانس شبکه از محدوده مجاز خارج شد",
            _ => $"{title}: {phase}"
        };
    }

    private static string GetSeverity(string title)
    {
        return title switch
        {
            "قطع فاز" or "ولتاژ بالا" or "ولتاژ پایین" or "فرکانس نامعتبر" => "Critical",
            _ => "Warning"
        };
    }
}
