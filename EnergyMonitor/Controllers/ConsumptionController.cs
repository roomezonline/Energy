using EnergyMonitor.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnergyMonitor.Controllers;

[Authorize]
[ApiController]
[Route("api/consumption")]
public class ConsumptionController : ControllerBase
{
    private readonly AppDbContext _db;

    public ConsumptionController(AppDbContext db) => _db = db;

    [HttpGet("{centerId}")]
    public async Task<IActionResult> GetCurrent(Guid centerId)
    {
        var center = await _db.Centers.FindAsync(centerId);
        if (center is null) return NotFound();

        var device = await _db.Devices.Where(d => d.CenterId == centerId && d.IsActive)
            .OrderBy(d => d.DisplayName).FirstOrDefaultAsync();
        if (device is null)
            return NotFound(new { error = "مرکز فاقد دستگاه فعال است" });

        var deviceId = device.DeviceId;
        var now = DateTime.UtcNow;
        var iranTz = TimeZoneInfo.FindSystemTimeZoneById("Iran Standard Time");
        var iranNow = TimeZoneInfo.ConvertTimeFromUtc(now, iranTz);
        var todayStart = TimeZoneInfo.ConvertTimeToUtc(iranNow.Date, iranTz);
        var monthStart = TimeZoneInfo.ConvertTimeToUtc(new DateTime(iranNow.Year, iranNow.Month, 1), iranTz);

        IQueryable<EnergyConsumption> consQ = _db.EnergyConsumptions.Where(e => e.DeviceId == deviceId);

        // Today — direct SUM
        var todayTotal = await consQ.Where(e => e.Timestamp >= todayStart)
            .SumAsync(e => e.DeltaA + e.DeltaB + e.DeltaC);

        // This month — direct SUM
        var monthTotal = await consQ.Where(e => e.Timestamp >= monthStart)
            .SumAsync(e => e.DeltaA + e.DeltaB + e.DeltaC);

        // Peak current/power from EnergyConsumptions
        var todayPeak = await consQ.Where(e => e.Timestamp >= todayStart)
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

        var limits = await _db.EnergyLimits
            .Where(l => l.CenterId == centerId && l.IsActive)
            .ToListAsync();

        return Ok(new
        {
            centerId,
            deviceId,
            centerName = center.Name,
            today = new
            {
                totalKWh = Math.Round(todayTotal, 4),
                peakCurrent = new
                {
                    phaseA = Math.Round(todayPeak?.peakA ?? 0, 2),
                    phaseB = Math.Round(todayPeak?.peakB ?? 0, 2),
                    phaseC = Math.Round(todayPeak?.peakC ?? 0, 2)
                },
                peakPower = new
                {
                    phaseA = Math.Round(todayPeak?.peakWA ?? 0, 1),
                    phaseB = Math.Round(todayPeak?.peakWB ?? 0, 1),
                    phaseC = Math.Round(todayPeak?.peakWC ?? 0, 1)
                }
            },
            month = new
            {
                totalKWh = Math.Round(monthTotal, 4)
            },
            limits = limits.Select(l => new
            {
                l.LimitType,
                l.PeriodType,
                l.MaxValue,
                l.AlertThresholdPercent,
                usagePercent = l.MaxValue > 0
                    ? (l.PeriodType == "Monthly"
                        ? Math.Round(monthTotal / l.MaxValue * 100, 1)
                        : Math.Round(todayTotal / l.MaxValue * 100, 1))
                    : 0
            })
        });
    }

    [HttpGet("{centerId}/history")]
    public async Task<IActionResult> GetHistory(Guid centerId, int months = 12)
    {
        var center = await _db.Centers.FindAsync(centerId);
        if (center is null) return NotFound();

        var device = await _db.Devices.Where(d => d.CenterId == centerId && d.IsActive)
            .OrderBy(d => d.DisplayName).FirstOrDefaultAsync();
        if (device is null)
            return NotFound(new { error = "مرکز فاقد دستگاه فعال است" });

        var deviceId = device.DeviceId;
        var iranTz = TimeZoneInfo.FindSystemTimeZoneById("Iran Standard Time");
        var iranNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, iranTz);

        var overallStart = TimeZoneInfo.ConvertTimeToUtc(
            new DateTime(iranNow.AddMonths(-months).Year, iranNow.AddMonths(-months).Month, 1), iranTz);
        var allCons = await _db.EnergyConsumptions
            .Where(e => e.DeviceId == deviceId && e.Timestamp >= overallStart)
            .ToListAsync();

        var monthlyData = new List<object>();
        for (int i = 0; i < months; i++)
        {
            var m = iranNow.AddMonths(-(months - 1 - i));
            var start = TimeZoneInfo.ConvertTimeToUtc(new DateTime(m.Year, m.Month, 1), iranTz);
            var end = start.AddMonths(1);

            var totalKWh = allCons.Where(e => e.Timestamp >= start && e.Timestamp < end)
                .Sum(e => e.DeltaA + e.DeltaB + e.DeltaC);

            monthlyData.Add(new
            {
                year = m.Year,
                month = m.Month,
                totalKWh = Math.Round(totalKWh, 4)
            });
        }

        return Ok(monthlyData);
    }
}
