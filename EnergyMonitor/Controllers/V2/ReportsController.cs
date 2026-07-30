using EnergyMonitor.Data;
using EnergyMonitor.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace EnergyMonitor.Controllers.V2;

[Authorize]
[ApiController]
[Route("api/v2/reports")]
public class ReportsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<ReportsController> _log;
    private readonly ICurrentUserService _currentUser;

    public ReportsController(AppDbContext db, ILogger<ReportsController> log, ICurrentUserService currentUser)
    {
        _db = db;
        _log = log;
        _currentUser = currentUser;
    }

    [HttpGet("{centerId}/daily")]
    public async Task<IActionResult> GetDaily(Guid centerId, [FromQuery] string? from, [FromQuery] string? to)
    {
        if (!_currentUser.CanAccessCenter(centerId)) return Forbid();
        var center = await _db.Centers.FindAsync(centerId);
        if (center is null) return NotFound();
        var device = await _db.Devices.Where(d => d.CenterId == centerId && d.IsActive)
            .OrderBy(d => d.DisplayName).FirstOrDefaultAsync();
        if (device is null) return NotFound(new { error = "مرکز فاقد دستگاه فعال است" });

        var iranTz = TimeZoneInfo.FindSystemTimeZoneById("Iran Standard Time");
        var pc = new PersianCalendar();
        var iranNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, iranTz);
        var fromDate = string.IsNullOrEmpty(from) ? new DateTime(iranNow.Year, iranNow.Month, 1) : ParsePersianDate(from, pc);
        var toDate = string.IsNullOrEmpty(to) ? iranNow : ParsePersianDate(to, pc).AddDays(1);
        var fromUtc = TimeZoneInfo.ConvertTimeToUtc(fromDate, iranTz);
        var toUtc = TimeZoneInfo.ConvertTimeToUtc(toDate, iranTz);

        var consumptions = await _db.EnergyConsumptions
            .Where(c => c.DeviceId == device.DeviceId && c.Timestamp >= fromUtc && c.Timestamp < toUtc)
            .OrderBy(c => c.Timestamp)
            .ToListAsync();

        var daily = new List<object>();
        var grouped = consumptions.GroupBy(c => TimeZoneInfo.ConvertTimeFromUtc(c.Timestamp, iranTz).Date);
        foreach (var day in grouped.OrderBy(g => g.Key))
        {
            var list = day.ToList();
            var dA = list.Sum(c => c.DeltaA);
            var dB = list.Sum(c => c.DeltaB);
            var dC = list.Sum(c => c.DeltaC);
            var total = dA + dB + dC;
            var pDate = $"{pc.GetYear(day.Key):D4}/{pc.GetMonth(day.Key):D2}/{pc.GetDayOfMonth(day.Key):D2}";
            daily.Add(new { date = pDate, phaseAKWh = Math.Round(dA, 4), phaseBKWh = Math.Round(dB, 4), phaseCKWh = Math.Round(dC, 4), totalKWh = Math.Round(total, 4) });
        }
        return Ok(daily);
    }

    [HttpGet("{centerId}/monthly")]
    public async Task<IActionResult> GetMonthly(Guid centerId, [FromQuery] int year, [FromQuery] int? compareTo)
    {
        if (!_currentUser.CanAccessCenter(centerId)) return Forbid();
        var center = await _db.Centers.FindAsync(centerId);
        if (center is null) return NotFound();
        var device = await _db.Devices.Where(d => d.CenterId == centerId && d.IsActive)
            .OrderBy(d => d.DisplayName).FirstOrDefaultAsync();
        if (device is null) return NotFound(new { error = "مرکز فاقد دستگاه فعال است" });

        var iranTz = TimeZoneInfo.FindSystemTimeZoneById("Iran Standard Time");
        var pc = new PersianCalendar();
        var result = new List<object>();
        for (int m = 1; m <= 12; m++)
        {
            var start = TimeZoneInfo.ConvertTimeToUtc(pc.ToDateTime(year, m, 1, 0, 0, 0, 0), iranTz);
            var end = m < 12 ? TimeZoneInfo.ConvertTimeToUtc(pc.ToDateTime(year, m + 1, 1, 0, 0, 0, 0), iranTz)
                : TimeZoneInfo.ConvertTimeToUtc(pc.ToDateTime(year + 1, 1, 1, 0, 0, 0, 0), iranTz);

            var total = await _db.EnergyConsumptions
                .Where(c => c.DeviceId == device.DeviceId && c.Timestamp >= start && c.Timestamp < end)
                .SumAsync(c => c.DeltaA + c.DeltaB + c.DeltaC);

            decimal? compareTotal = null;
            if (compareTo.HasValue)
            {
                var cStart = TimeZoneInfo.ConvertTimeToUtc(pc.ToDateTime(compareTo.Value, m, 1, 0, 0, 0, 0), iranTz);
                var cEnd = m < 12 ? TimeZoneInfo.ConvertTimeToUtc(pc.ToDateTime(compareTo.Value, m + 1, 1, 0, 0, 0, 0), iranTz)
                    : TimeZoneInfo.ConvertTimeToUtc(pc.ToDateTime(compareTo.Value + 1, 1, 1, 0, 0, 0, 0), iranTz);
                compareTotal = await _db.EnergyConsumptions
                    .Where(c => c.DeviceId == device.DeviceId && c.Timestamp >= cStart && c.Timestamp < cEnd)
                    .SumAsync(c => c.DeltaA + c.DeltaB + c.DeltaC);
            }
            result.Add(new { month = m, year, totalKWh = Math.Round(total, 4), compareToYear = compareTo, compareToKWh = compareTotal.HasValue ? (decimal?)Math.Round(compareTotal.Value, 4) : null });
        }
        return Ok(result);
    }

    [HttpGet("{centerId}/peak")]
    public async Task<IActionResult> GetPeak(Guid centerId, [FromQuery] string? from, [FromQuery] string? to)
    {
        if (!_currentUser.CanAccessCenter(centerId)) return Forbid();
        var center = await _db.Centers.FindAsync(centerId);
        if (center is null) return NotFound();
        var device = await _db.Devices.Where(d => d.CenterId == centerId && d.IsActive)
            .OrderBy(d => d.DisplayName).FirstOrDefaultAsync();
        if (device is null) return NotFound(new { error = "مرکز فاقد دستگاه فعال است" });

        var iranTz = TimeZoneInfo.FindSystemTimeZoneById("Iran Standard Time");
        var pc = new PersianCalendar();
        var iranNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, iranTz);
        var fromDate = string.IsNullOrEmpty(from) ? new DateTime(iranNow.Year, iranNow.Month, 1) : ParsePersianDate(from, pc);
        var toDate = string.IsNullOrEmpty(to) ? iranNow : ParsePersianDate(to, pc).AddDays(1);
        var fromUtc = TimeZoneInfo.ConvertTimeToUtc(fromDate, iranTz);
        var toUtc = TimeZoneInfo.ConvertTimeToUtc(toDate, iranTz);

        var peak = await _db.EnergyConsumptions
            .Where(c => c.DeviceId == device.DeviceId && c.Timestamp >= fromUtc && c.Timestamp < toUtc)
            .OrderByDescending(c => c.DeltaA + c.DeltaB + c.DeltaC)
            .FirstOrDefaultAsync();

        if (peak is null)
            return Ok(new { peakKWh = 0, peakDate = "", peakHour = -1 });

        var peakTotal = peak.DeltaA + peak.DeltaB + peak.DeltaC;
        var iranPt = TimeZoneInfo.ConvertTimeFromUtc(peak.Timestamp, iranTz);
        var pDate = $"{pc.GetYear(iranPt):D4}/{pc.GetMonth(iranPt):D2}/{pc.GetDayOfMonth(iranPt):D2}";
        return Ok(new { peakKWh = Math.Round(peakTotal, 4), peakDate = pDate, peakHour = iranPt.Hour });
    }

    [HttpGet("{centerId}/comparison")]
    public async Task<IActionResult> Compare(Guid centerId,
        [FromQuery] string from1, [FromQuery] string to1,
        [FromQuery] string from2, [FromQuery] string to2)
    {
        if (!_currentUser.CanAccessCenter(centerId)) return Forbid();
        var center = await _db.Centers.FindAsync(centerId);
        if (center is null) return NotFound();
        var device = await _db.Devices.Where(d => d.CenterId == centerId && d.IsActive)
            .OrderBy(d => d.DisplayName).FirstOrDefaultAsync();
        if (device is null) return NotFound(new { error = "مرکز فاقد دستگاه فعال است" });

        var iranTz = TimeZoneInfo.FindSystemTimeZoneById("Iran Standard Time");
        var pc = new PersianCalendar();

        decimal PeriodTotal(string fStr, string tStr)
        {
            var f = ParsePersianDate(fStr, pc);
            var t = ParsePersianDate(tStr, pc).AddDays(1);
            var fUtc = TimeZoneInfo.ConvertTimeToUtc(f, iranTz);
            var tUtc = TimeZoneInfo.ConvertTimeToUtc(t, iranTz);
            return _db.EnergyConsumptions
                .Where(x => x.DeviceId == device.DeviceId && x.Timestamp >= fUtc && x.Timestamp < tUtc)
                .Sum(x => x.DeltaA + x.DeltaB + x.DeltaC);
        }

        var total1 = PeriodTotal(from1, to1);
        var total2 = PeriodTotal(from2, to2);
        var delta = total1 - total2;
        var deltaPercent = total2 > 0 ? (delta / total2 * 100) : 0;
        return Ok(new
        {
            period1 = new { from = from1, to = to1, totalKWh = Math.Round(total1, 4) },
            period2 = new { from = from2, to = to2, totalKWh = Math.Round(total2, 4) },
            deltaKWh = Math.Round(delta, 4),
            deltaPercent = Math.Round(deltaPercent, 1)
        });
    }

    [HttpGet("{centerId}/export")]
    public async Task<IActionResult> ExportCsv(Guid centerId, [FromQuery] string? from, [FromQuery] string? to)
    {
        if (!_currentUser.CanAccessCenter(centerId)) return Forbid();
        var center = await _db.Centers.FindAsync(centerId);
        if (center is null) return NotFound();
        var device = await _db.Devices.Where(d => d.CenterId == centerId && d.IsActive)
            .OrderBy(d => d.DisplayName).FirstOrDefaultAsync();
        if (device is null) return NotFound();

        var iranTz = TimeZoneInfo.FindSystemTimeZoneById("Iran Standard Time");
        var pc = new PersianCalendar();
        var iranNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, iranTz);
        var fromDate = string.IsNullOrEmpty(from) ? new DateTime(iranNow.Year, iranNow.Month, 1) : ParsePersianDate(from, pc);
        var toDate = string.IsNullOrEmpty(to) ? iranNow : ParsePersianDate(to, pc).AddDays(1);
        var fromUtc = TimeZoneInfo.ConvertTimeToUtc(fromDate, iranTz);
        var toUtc = TimeZoneInfo.ConvertTimeToUtc(toDate, iranTz);

        var consumptions = await _db.EnergyConsumptions
            .Where(c => c.DeviceId == device.DeviceId && c.Timestamp >= fromUtc && c.Timestamp < toUtc)
            .OrderBy(c => c.Timestamp)
            .ToListAsync();

        var csv = new System.Text.StringBuilder();
        csv.AppendLine("تاریخ,فاز A (kWh),فاز B (kWh),فاز C (kWh),جمع کل (kWh)");
        var grouped = consumptions.GroupBy(c => TimeZoneInfo.ConvertTimeFromUtc(c.Timestamp, iranTz).Date);
        foreach (var day in grouped.OrderBy(g => g.Key))
        {
            var list = day.ToList();
            var dA = list.Sum(c => c.DeltaA);
            var dB = list.Sum(c => c.DeltaB);
            var dC = list.Sum(c => c.DeltaC);
            var pDate = $"{pc.GetYear(day.Key):D4}/{pc.GetMonth(day.Key):D2}/{pc.GetDayOfMonth(day.Key):D2}";
            csv.AppendLine($"{pDate},{dA:F4},{dB:F4},{dC:F4},{dA + dB + dC:F4}");
        }
        var bytes = System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        return File(bytes, "text/csv", $"consumption_{center.Name}_{from}_{to}.csv");
    }

    private static DateTime ParsePersianDate(string persianDate, PersianCalendar pc)
    {
        var parts = persianDate.Split('/');
        if (parts.Length != 3) throw new ArgumentException();
        return pc.ToDateTime(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]), 0, 0, 0, 0);
    }
}
