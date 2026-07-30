using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using EnergyMonitor.Data;
using EnergyMonitor.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnergyMonitor.Controllers;

[ApiController]
[Route("api/ingestion")]
public class IngestionController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<IngestionController> _log;
    private readonly AlarmService _alarmService;

    public IngestionController(AppDbContext db, ILogger<IngestionController> log, AlarmService alarmService)
    {
        _db = db;
        _log = log;
        _alarmService = alarmService;
    }

    [HttpPost("publish")]
    public async Task<IActionResult> Publish([FromBody] EnergyDataDto data)
    {
        if (data is null) return BadRequest(new { error = "Empty payload" });

        try
        {
            // ===== Fix 9: Use server time when Arduino timestamp invalid (RTC battery death / first sync) =====
            var now = DateTime.UtcNow;
            if (data.Timestamp < now.AddDays(-1) || data.Timestamp > now.AddHours(1))
            {
                _log.LogWarning("Ingestion: bad timestamp from {Device} ({Ts}), using server time",
                    data.DeviceId, data.Timestamp);
                data.Timestamp = now;
            }

            // ===== C3: Sanitize delta values =====
            const decimal maxDeltaPerPhase = 5000.0m;
            decimal dA = SanitizeDelta((decimal)(data.PhaseA?.Delta ?? 0), maxDeltaPerPhase, "A", data.DeviceId);
            decimal dB = SanitizeDelta((decimal)(data.PhaseB?.Delta ?? 0), maxDeltaPerPhase, "B", data.DeviceId);
            decimal dC = SanitizeDelta((decimal)(data.PhaseC?.Delta ?? 0), maxDeltaPerPhase, "C", data.DeviceId);

            var totalPower = (data.PhaseA?.Power ?? 0)
                           + (data.PhaseB?.Power ?? 0)
                           + (data.PhaseC?.Power ?? 0);
            var totalEnergy = (data.PhaseA?.Energy ?? 0)
                            + (data.PhaseB?.Energy ?? 0)
                            + (data.PhaseC?.Energy ?? 0);

            var cfg = await _db.DeviceConfigs
                .FirstOrDefaultAsync(c => c.DeviceId == data.DeviceId);

            bool savingEnabled = cfg?.IsSavingEnabled ?? true;

            // Load device + center (capture prevLastSeen BEFORE updating)
            var device = await _db.Devices.FirstOrDefaultAsync(d => d.DeviceId == data.DeviceId);
            DateTime? prevLastSeen = device?.LastSeenAt;
            Center? center = null;
            if (device is not null)
            {
                device.LastSeenAt = data.Timestamp;
                device.PhaseAConnected = data.PhaseA?.Connected ?? true;
                device.PhaseBConnected = data.PhaseB?.Connected ?? true;
                device.PhaseCConnected = data.PhaseC?.Connected ?? true;

                center = await _db.Centers.FirstOrDefaultAsync(c => c.Id == device.CenterId);
            }

            // Save snapshot + alarms only if saving enabled
            if (savingEnabled)
            {
                var persianTs = JalaliDateTimeHelper.ToPersianDateTimeStringFromUtc(data.Timestamp);

                var snap = new EnergySnapshot
                {
                    DeviceId = data.DeviceId,
                    Timestamp = data.Timestamp,
                    PersianTimestamp = persianTs,
                    VoltageA = (decimal)(data.PhaseA?.Voltage ?? 0),
                    CurrentA = (decimal)(data.PhaseA?.Current ?? 0),
                    PowerA = (decimal)(data.PhaseA?.Power ?? 0),
                    PfA = (decimal)(data.PhaseA?.Pf ?? 0),
                    EnergyA = (decimal)(data.PhaseA?.Energy ?? 0),
                    VoltageB = (decimal)(data.PhaseB?.Voltage ?? 0),
                    CurrentB = (decimal)(data.PhaseB?.Current ?? 0),
                    PowerB = (decimal)(data.PhaseB?.Power ?? 0),
                    PfB = (decimal)(data.PhaseB?.Pf ?? 0),
                    EnergyB = (decimal)(data.PhaseB?.Energy ?? 0),
                    VoltageC = (decimal)(data.PhaseC?.Voltage ?? 0),
                    CurrentC = (decimal)(data.PhaseC?.Current ?? 0),
                    PowerC = (decimal)(data.PhaseC?.Power ?? 0),
                    PfC = (decimal)(data.PhaseC?.Pf ?? 0),
                    EnergyC = (decimal)(data.PhaseC?.Energy ?? 0),
                    Frequency = (decimal)data.Frequency,
                    Temperature = (decimal)data.Temperature,
                    TotalPower = (decimal)totalPower
                };
                _db.EnergySnapshots.Add(snap);

                // ===== Fix 11: Hourly delta distribution for long outage catch-up (TOU billing accuracy) =====
                if (dA > 0 || dB > 0 || dC > 0)
                {
                    var tsKey = data.Timestamp.AddTicks(-(data.Timestamp.Ticks % TimeSpan.TicksPerSecond));

                    // Idempotency check
                    bool alreadyExists = await _db.EnergyConsumptions
                        .AnyAsync(e => e.DeviceId == data.DeviceId
                                    && e.Timestamp == tsKey
                                    && Math.Abs(e.DeltaA - dA) < 0.0001m
                                    && Math.Abs(e.DeltaB - dB) < 0.0001m
                                    && Math.Abs(e.DeltaC - dC) < 0.0001m);
                    if (alreadyExists)
                    {
                        _log.LogWarning("Ingestion duplicate skipped: {Device} at {Ts}", data.DeviceId, tsKey);
                    }
                    else
                    {
                        // Detect long outage gap — distribute deltas hourly for TOU accuracy
                        decimal gapHours = prevLastSeen.HasValue && prevLastSeen.Value < data.Timestamp
                            ? (decimal)(data.Timestamp - prevLastSeen.Value).TotalHours
                            : 0;

                        if (gapHours >= 1.0m && (dA + dB + dC) > 0.01m)
                        {
                            int hours = (int)Math.Floor((double)gapHours);
                            if (hours > 48) hours = 48; // cap at 48h to avoid massive insert
                            decimal hourlyA = dA / hours;
                            decimal hourlyB = dB / hours;
                            decimal hourlyC = dC / hours;

                            _log.LogInformation("Outage catch-up: {Device} gap={Hours:F1}h distributing deltas across {HoursCount} hourly buckets",
                                data.DeviceId, gapHours, hours);

                            for (int h = 0; h < hours; h++)
                            {
                                var bucketTs = prevLastSeen!.Value.AddHours(h).AddMinutes(30); // mid-hour
                                _db.EnergyConsumptions.Add(new EnergyConsumption
                                {
                                    DeviceId = data.DeviceId,
                                    Timestamp = bucketTs,
                                    PersianTimestamp = JalaliDateTimeHelper.ToPersianDateTimeStringFromUtc(bucketTs),
                                    DeltaA = hourlyA,
                                    DeltaB = hourlyB,
                                    DeltaC = hourlyC,
                                });
                            }
                        }
                        else
                        {
                            // Normal single record
                            _db.EnergyConsumptions.Add(new EnergyConsumption
                            {
                                DeviceId = data.DeviceId,
                                Timestamp = data.Timestamp,
                                PersianTimestamp = persianTs,
                                DeltaA = dA,
                                PeakCurrentA = (decimal)(data.PhaseAWin?.MxA ?? 0),
                                PeakPowerA = (decimal)(data.PhaseAWin?.MxW ?? 0),
                                DeltaB = dB,
                                PeakCurrentB = (decimal)(data.PhaseBWin?.MxA ?? 0),
                                PeakPowerB = (decimal)(data.PhaseBWin?.MxW ?? 0),
                                DeltaC = dC,
                                PeakCurrentC = (decimal)(data.PhaseCWin?.MxA ?? 0),
                                PeakPowerC = (decimal)(data.PhaseCWin?.MxW ?? 0)
                            });
                        }
                    }
                }

                if (cfg is not null && center is not null)
                    await _alarmService.ProcessAlarms(cfg, data, center.Id, device?.DeviceId, device?.PhaseCount ?? 3);
            }
            else if (cfg is not null && center is not null)
            {
                await _alarmService.ProcessAlarms(cfg, data, center.Id, device?.DeviceId, device?.PhaseCount ?? 3);
            }

            await _db.SaveChangesAsync();

            try
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write($"  [{DateTime.Now:HH:mm:ss}] ");
                Console.ForegroundColor = savingEnabled ? ConsoleColor.Green : ConsoleColor.DarkYellow;
                Console.Write($"{(savingEnabled ? "✓" : "○")} {data.DeviceId} ");
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.Write($"│ {totalPower:F1}W │ {totalEnergy:F2}kWh │ {data.Frequency:F0}Hz");
                if (!savingEnabled) Console.Write("  [display only]");
                Console.WriteLine();
            }
            finally
            {
                Console.ResetColor();
            }

            _log.LogInformation("Ingested: {Device} | {Power:F2}W | {Energy:F2}kWh",
                data.DeviceId, totalPower, totalEnergy);

            var utc = DateTime.UtcNow;
            var iranTz = TimeZoneInfo.FindSystemTimeZoneById("Iran Standard Time");
            var nowIran = TimeZoneInfo.ConvertTimeFromUtc(utc, iranTz);
            return Ok(new
            {
                ok = true,
                device = data.DeviceId,
                power = totalPower,
                energy = totalEnergy,
                serverTime = utc.ToString("o"),
                persianTime = JalaliDateTimeHelper.ToPersianDateTimeString(nowIran),
                phaseCount = device?.PhaseCount ?? 3,
                config = cfg is null ? null : new
                {
                    publishIntervalMs = cfg.PublishIntervalMs,
                    thresholds = new
                    {
                        overVoltage = cfg.OverVoltageThreshold,
                        underVoltage = cfg.UnderVoltageThreshold,
                        overCurrent = cfg.OverCurrentThreshold,
                        phaseImbalance = cfg.PhaseImbalanceThreshold,
                        lowPF = cfg.LowPFThreshold,
                        freqMin = cfg.FreqMinThreshold,
                        freqMax = cfg.FreqMaxThreshold,
                        highPower = cfg.HighPowerThreshold,
                        temperatureThreshold = cfg.TemperatureThreshold
                    }
                }
            });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Ingestion failed for {Device}", data.DeviceId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    private static decimal SanitizeDelta(decimal delta, decimal max, string phase, string deviceId)
    {
        if (delta < 0)
            return 0;
        if (delta > max)
        {
            return 0;
        }
        return delta;
    }
}

public class EnergyDataDto
{
    [Required(ErrorMessage = "DeviceId الزامی است")]
    public string DeviceId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public PhaseDataDto? PhaseA { get; set; }
    public PhaseDataDto? PhaseB { get; set; }
    public PhaseDataDto? PhaseC { get; set; }
    public double Frequency { get; set; }
    [JsonPropertyName("temp")]
    public double Temperature { get; set; }
    public WindowDataDto? PhaseAWin { get; set; }
    public WindowDataDto? PhaseBWin { get; set; }
    public WindowDataDto? PhaseCWin { get; set; }
    public OutageDataDto? Outage { get; set; }
}

public class PhaseDataDto
{
    public double Voltage { get; set; }
    public double Current { get; set; }
    public double Power { get; set; }
    public double Pf { get; set; }
    public double Energy { get; set; }
    [JsonPropertyName("delta")]
    public double Delta { get; set; }
    public bool Connected { get; set; } = true;
}

public class WindowDataDto
{
    public double MnV { get; set; }
    public double MxV { get; set; }
    public double MnA { get; set; }
    public double MxA { get; set; }
    public double MnW { get; set; }
    public double MxW { get; set; }
    public double AV { get; set; }
    public double AA { get; set; }
    public double AW { get; set; }
    public double DE { get; set; }
}

public class OutageDataDto
{
    public long Sd { get; set; }
    public double DA { get; set; }
    public double DB { get; set; }
    public double DC { get; set; }
    public double MaxA { get; set; }
    public double MaxB { get; set; }
    public double MaxC { get; set; }
    public double MwA { get; set; }
    public double MwB { get; set; }
    public double MwC { get; set; }
}
